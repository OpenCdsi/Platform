/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.VaxEngine.Core.Pipeline;

/// <summary>
/// §5.1 Select Relevant Patient Series: determines which of the antigen series defined by the
/// supporting data are appropriate to evaluate/forecast for this patient (Table 5-5).
///
///   - Standard / Evaluation Only series: relevant for every patient of the matching gender.
///   - Risk series: relevant only if the gender matches AND at least one indication
///     unambiguously applies to the patient (Table 5-4).
///
/// Note this runs over the FULL antigen series catalog, not just antigens the patient has
/// existing doses for — a patient with zero HepB doses still needs the HepB series created so
/// dose 1 can be forecast. AntigenAdministered records (from OrganizeImmunizationHistory) are
/// evaluated against these series in the next pipeline stage (Chapter 6), not this one.
/// </summary>
public static class CreateRelevantPatientSeries
{
    private static readonly DateOnly IndicationBeginAgeDefault = new(1900, 1, 1);
    private static readonly DateOnly IndicationEndAgeDefault = new(2999, 12, 31);

    public static RelevantPatientSeriesResult Execute(
        Patient patient,
        IReadOnlyList<AntigenSeries> allSeries,
        DateOnly assessmentDate)
    {
        var decisions = allSeries
            .Select(series => (Series: series, Relevance: DecideRelevance(patient, series, assessmentDate)))
            .ToArray();

        return new RelevantPatientSeriesResult
        {
            RelevantSeries = decisions.Where(d => d.Relevance.IsRelevant).Select(d => d.Series).ToArray(),
            UnresolvedIndications = decisions
                .SelectMany(d => d.Relevance.InconclusiveIndications.Select(indication => new UnresolvedIndicationNotification
                {
                    SeriesName = d.Series.SeriesName,
                    Antigen = d.Series.Antigen,
                    ObservationCode = indication.ObservationCode,
                    Description = indication.Description
                }))
                .ToArray()
        };
    }

    /// <summary>
    /// One series' §5.1 outcome. InconclusiveIndications is non-empty only for a Risk series
    /// where nothing applied, since those are the only ones surfaced for clinician review.
    /// </summary>
    private sealed record SeriesRelevance(bool IsRelevant, IReadOnlyList<Indication> InconclusiveIndications)
    {
        public static readonly SeriesRelevance Relevant = new(true, []);
        public static readonly SeriesRelevance NotRelevant = new(false, []);
    }

    private static SeriesRelevance DecideRelevance(Patient patient, AntigenSeries series, DateOnly assessmentDate)
    {
        if (!series.AppliesToGender(patient.Gender))
        {
            return SeriesRelevance.NotRelevant;
        }

        if (series.SeriesType is SeriesType.Standard or SeriesType.EvaluationOnly)
        {
            return SeriesRelevance.Relevant;
        }

        // Risk series: Table 5-4 per indication, then "at least one applies" for the series.
        // Lazy on purpose: Any stops at the first applying indication (Table 5-5 only needs one),
        // so indications after it are never evaluated.
        var outcomes = series.Indications.Select(indication => (Indication: indication, Outcome: EvaluateIndication(patient, indication, assessmentDate)));

        if (outcomes.Any(o => o.Outcome == IndicationOutcome.Applies))
        {
            return SeriesRelevance.Relevant;
        }

        // Nothing applied, so every indication was evaluated above; re-enumerating the pure
        // evaluation yields the same outcomes. An empty list means every indication resolved
        // definitively to "No" - series is simply not relevant, no notification.
        return new SeriesRelevance(false, outcomes
            .Where(o => o.Outcome == IndicationOutcome.Inconclusive)
            .Select(o => o.Indication)
            .ToArray());
    }

    private enum IndicationOutcome { Applies, DoesNotApply, Inconclusive }

    private static IndicationOutcome EvaluateIndication(Patient patient, Indication indication, DateOnly assessmentDate)
    {
        var beginDate = indication.BeginAge?.AddTo(patient.DateOfBirth) ?? IndicationBeginAgeDefault;
        var endDate = indication.EndAge?.AddTo(patient.DateOfBirth) ?? IndicationEndAgeDefault;
        var ageMatches = assessmentDate >= beginDate && assessmentDate < endDate;

        // Table 5-4, Rule 4: age window failing means "does not apply" regardless of the observation match.
        if (!ageMatches)
        {
            return IndicationOutcome.DoesNotApply;
        }

        var observationState = ResolveObservationState(patient, indication.ObservationCode);
        return observationState switch
        {
            ObservationState.Present => IndicationOutcome.Applies,       // Rule 1
            ObservationState.Absent => IndicationOutcome.DoesNotApply,   // Rule 2
            ObservationState.Unknown => IndicationOutcome.Inconclusive,  // Rule 3
            _ => IndicationOutcome.DoesNotApply
        };
    }

    private enum ObservationState { Present, Absent, Unknown }

    private static ObservationState ResolveObservationState(Patient patient, string? observationCode)
    {
        if (observationCode is null)
        {
            return ObservationState.Absent;
        }
        if (patient.ActiveObservations.Any(o => o.Code == observationCode))
        {
            return ObservationState.Present;
        }
        if (patient.UnresolvedObservationCodes.Contains(observationCode))
        {
            return ObservationState.Unknown;
        }
        return ObservationState.Absent;
    }
}
