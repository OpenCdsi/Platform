/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.Pipeline;

namespace OpenCdsi.VaxEngine.Contracts;

/// <summary>
/// Maps GeneratePatientForecast.ExecuteWithDoseDetail's per-antigen dose detail to the
/// EvaluationResponseDto wire shape - kept as one small, testable static class rather than
/// inlined into endpoint handlers, same reasoning as RequestMapping/ResponseMapping.
///
/// COLLAPSE RULE: the engine evaluates each administered dose once per associated antigen. This
/// collapses that to one row per physical dose (keyed by DoseId):
///   - Within an antigen, the same DoseId can appear against more than one target dose (the
///     two-pointer walk advances the target pointer on a Skip without consuming the administered
///     dose). Prefer the non-Skipped record - the Skipped one carries no grade. Same heuristic
///     ConformanceTests uses.
///   - Across antigens, the headline Status is the MOST PERMISSIVE graded status
///     (Valid &gt; SubStandard &gt; NotValid &gt; Extraneous). Skipped per-antigen entries carry no
///     grade, so they never affect the headline and never count as a conflict.
///   - No graded status at all -> "NotEvaluated".
/// </summary>
public static class EvaluationResponseMapping
{
    public static EvaluationResponseDto ToResponse(
        string patientId,
        DateOnly assessmentDate,
        IReadOnlyList<VaccineDoseAdministered> administeredDoses,
        PatientForecastResult result)
    {
        // (DoseId -> list of that dose's per-antigen outcomes). Built by walking every tracked
        // antigen's dose-by-dose detail.
        var perAntigenByDoseId = new Dictionary<string, List<AntigenDoseEvaluationDto>>();
        foreach (var (antigen, history) in result.DoseDetailsByAntigen)
        {
            foreach (var group in history.DoseResults.GroupBy(r => r.AdministeredDose.SourceDose.DoseId))
            {
                var record = group.FirstOrDefault(r => r.Result.TargetDoseStatus != TargetDoseStatus.Skipped)
                    ?? group.First();

                var status = record.Result.TargetDoseStatus == TargetDoseStatus.Skipped
                    ? "Skipped"
                    : record.Result.EvaluationStatus?.ToString() ?? "Skipped";

                if (!perAntigenByDoseId.TryGetValue(group.Key, out var list))
                {
                    list = new List<AntigenDoseEvaluationDto>();
                    perAntigenByDoseId[group.Key] = list;
                }
                list.Add(new AntigenDoseEvaluationDto
                {
                    Antigen = antigen,
                    Status = status,
                    TargetDoseNumber = record.Result.TargetDoseStatus == TargetDoseStatus.Skipped ? null : record.TargetDoseNumber,
                    Reason = record.Result.Reason
                });
            }
        }

        // One row per requested physical dose, in request order.
        var evaluatedDoses = administeredDoses
            .Select(dose => CollapseDose(dose, perAntigenByDoseId.GetValueOrDefault(dose.DoseId)))
            .ToArray();

        // Scoped to antigens that actually had one of this patient's administered doses evaluated
        // against them - this endpoint is about the doses that were given, not a full cross-antigen
        // standing (that's what /forecast is for). An antigen the patient hasn't started would
        // otherwise show up here as noise, one row per untouched series.
        var antigenSummaries = result.DoseDetailsByAntigen
            .Where(kv => kv.Value.DoseResults.Count > 0)
            .Select(kv => new AntigenEvaluationSummaryDto
            {
                Antigen = kv.Key,
                SeriesComplete = kv.Value.SeriesComplete,
                CurrentTargetDoseNumber = kv.Value.CurrentTargetDoseNumber
            })
            .OrderBy(s => s.Antigen, StringComparer.Ordinal)
            .ToArray();

        return new EvaluationResponseDto
        {
            PatientId = patientId,
            AssessmentDate = assessmentDate,
            EvaluatedDoses = evaluatedDoses,
            AntigenSummaries = antigenSummaries
        };
    }

    private static EvaluatedDoseDto CollapseDose(VaccineDoseAdministered dose, IReadOnlyList<AntigenDoseEvaluationDto>? perAntigen)
    {
        perAntigen = (perAntigen ?? Array.Empty<AntigenDoseEvaluationDto>())
            .OrderBy(a => a.Antigen, StringComparer.Ordinal)
            .ToArray();

        var graded = perAntigen
            .Select(a => (Entry: a, Status: ParseGraded(a.Status)))
            .Where(x => x.Status is not null)
            .Select(x => (x.Entry, Status: x.Status!.Value))
            .ToArray();

        if (graded.Length == 0)
        {
            return new EvaluatedDoseDto
            {
                DoseId = dose.DoseId,
                Cvx = dose.Cvx,
                DateAdministered = dose.DateAdministered,
                Status = "NotEvaluated",
                HasAntigenConflict = false,
                ConflictText = null,
                PerAntigen = perAntigen
            };
        }

        var headline = graded.MaxBy(x => Permissiveness(x.Status)).Status;
        var distinctStatuses = graded.Select(x => x.Status).Distinct().ToArray();
        var hasConflict = distinctStatuses.Length > 1;

        return new EvaluatedDoseDto
        {
            DoseId = dose.DoseId,
            Cvx = dose.Cvx,
            DateAdministered = dose.DateAdministered,
            Status = headline.ToString(),
            HasAntigenConflict = hasConflict,
            ConflictText = hasConflict ? BuildConflictText(graded, headline) : null,
            PerAntigen = perAntigen
        };
    }

    /// <summary>Names the antigens whose graded status isn't the headline, e.g. "Pertussis: Not Valid (Too young)".</summary>
    private static string BuildConflictText(
        IReadOnlyList<(AntigenDoseEvaluationDto Entry, EvaluationStatus Status)> graded,
        EvaluationStatus headline)
    {
        var deviating = graded
            .Where(x => x.Status != headline)
            .OrderByDescending(x => Permissiveness(x.Status))
            .Select(x => x.Entry.Reason is { Length: > 0 } reason
                ? $"{x.Entry.Antigen}: {FriendlyStatus(x.Status)} ({reason})"
                : $"{x.Entry.Antigen}: {FriendlyStatus(x.Status)}");

        return string.Join("; ", deviating);
    }

    private static EvaluationStatus? ParseGraded(string status) => status switch
    {
        nameof(EvaluationStatus.Valid) => EvaluationStatus.Valid,
        nameof(EvaluationStatus.NotValid) => EvaluationStatus.NotValid,
        nameof(EvaluationStatus.Extraneous) => EvaluationStatus.Extraneous,
        nameof(EvaluationStatus.SubStandard) => EvaluationStatus.SubStandard,
        _ => null // "Skipped"
    };

    // Valid is the most permissive/general outcome, Extraneous the least - see the class doc comment.
    private static int Permissiveness(EvaluationStatus status) => status switch
    {
        EvaluationStatus.Valid => 3,
        EvaluationStatus.SubStandard => 2,
        EvaluationStatus.NotValid => 1,
        EvaluationStatus.Extraneous => 0,
        _ => -1
    };

    private static string FriendlyStatus(EvaluationStatus status) => status switch
    {
        EvaluationStatus.Valid => "Valid",
        EvaluationStatus.NotValid => "Not Valid",
        EvaluationStatus.Extraneous => "Extraneous",
        EvaluationStatus.SubStandard => "Sub-standard",
        _ => status.ToString()
    };
}
