/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Immutable;
using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.VaxEngine.Core.Pipeline;

/// <summary>
/// §4.4's Figure 4-5 high-level loop: run EvaluateSeriesHistory (§4.4's per-series inner loop)
/// across EVERY relevant patient series for a patient, not just one in isolation. This is the
/// entry point that ties OrganizeImmunizationHistory -> CreateRelevantPatientSeries -> (per
/// series) EvaluateSeriesHistory together into one real "evaluate this patient" call, and its
/// output (specifically each series' CurrentTargetDoseNumber) is exactly what §7 Forecast needs
/// to know what to forecast next.
///
/// KEY DESIGN POINT, directly grounded in §4.4's own text: "An administered dose that is 'valid'
/// for one relevant patient series may be 'not valid' for a different relevant patient series
/// for the same patient." Each series is evaluated completely independently against the SAME
/// raw antigen-administered records - series never share evaluation state with each other, even
/// two series for the same antigen. Only Vaccine Conflict (§6.7) crosses series boundaries, and
/// only for genuinely DIFFERENT antigens (see below).
///
/// SIMPLIFICATION, flagged: when a patient has multiple relevant series for the SAME antigen
/// (a real, documented scenario per §5.1's equivalent series groups), cross-antigen Vaccine
/// Conflict resolution for OTHER antigens' series only sees evaluated-dose history from
/// whichever same-antigen series happened to run first in this pass, not all of them. The
/// underlying administered fact (this CVX was given on this date) is patient-truth regardless
/// of series, but the evaluation STATUS attached to it (which affects CALCDTCONFLICT-2's
/// end-interval branch) can genuinely differ per series - picking one is a reasonable but real
/// simplification, not a spec-mandated resolution.
///
/// §6.2's "Completed Series" condition: `resolveCompletedSeries` takes the ANTIGEN alongside the
/// condition's own `seriesGroups` value, because `seriesGroups` (real data: always "1" or "2")
/// is only meaningful WITHIN one antigen's own file - the same string means something entirely
/// different for a different antigen. This function builds the antigen-scoped closure each
/// individual series' own evaluation actually needs (still a plain `Func&lt;string?, bool&gt;` by
/// the time it reaches EvaluateSeriesHistory/EvaluateConditionalSkip, which don't need to know
/// about antigen-scoping at all) - the caller is responsible for the resolver's actual logic,
/// typically a two-pass approach (see GeneratePatientForecast) since a series in one group often
/// needs to know about ANOTHER group's completion status within the same antigen.
/// </summary>
public static class EvaluatePatientSeriesHistory
{
    /// <param name="assessmentDate">Opt-in, threaded straight through to EvaluateSeriesHistory's own same-named parameter - see its class doc comment. Null (the default) preserves the exact prior behavior.</param>
    public static IReadOnlyDictionary<AntigenSeries, SeriesHistoryResult> Execute(
        Patient patient,
        IReadOnlyList<AntigenSeries> relevantSeries,
        IReadOnlyList<VaccineDoseAdministered> allDosesAdministered,
        IReadOnlyDictionary<string, CvxMapEntry> cvxToAntigen,
        IReadOnlyDictionary<string, IReadOnlyList<VaccineConflictRule>> conflictsByImpactedCvx,
        Func<string, string?, bool> resolveCompletedSeries,
        DateOnly? assessmentDate = null)
    {
        var antigenRecords = OrganizeImmunizationHistory.Execute(patient, allDosesAdministered, cvxToAntigen);

        // Sorted for determinism, not because order is spec-mandated - the spec doesn't say
        // what order relevant series should be evaluated in. The order is nonetheless
        // observable: each series only sees cross-antigen history from series folded before it,
        // and callers (GeneratePatientForecast) take the first result per antigen by enumeration
        // order. Hence an explicit left fold rather than an order-free map.
        var orderedSeries = relevantSeries
            .OrderBy(s => s.Antigen, StringComparer.Ordinal)
            .ThenBy(s => s.SeriesName, StringComparer.Ordinal);

        var final = orderedSeries.Aggregate(
            PatientEvaluationState.Empty,
            (state, series) => Step(state, series, patient, antigenRecords, conflictsByImpactedCvx, resolveCompletedSeries, assessmentDate));

        // Materialized with indexer assignment, in fold order, so a series instance appearing
        // twice keeps its first position but its last result - exactly as the prior imperative
        // loop behaved.
        var results = new Dictionary<AntigenSeries, SeriesHistoryResult>();
        foreach (var (series, result) in final.Results)
        {
            results[series] = result;
        }

        return results;
    }

    private sealed record PatientEvaluationState(
        ImmutableList<KeyValuePair<AntigenSeries, SeriesHistoryResult>> Results,
        ImmutableList<EvaluatedAntigenDose> PatientWideHistory)
    {
        public static readonly PatientEvaluationState Empty = new([], []);
    }

    private static PatientEvaluationState Step(
        PatientEvaluationState state,
        AntigenSeries series,
        Patient patient,
        IReadOnlyList<AntigenAdministered> antigenRecords,
        IReadOnlyDictionary<string, IReadOnlyList<VaccineConflictRule>> conflictsByImpactedCvx,
        Func<string, string?, bool> resolveCompletedSeries,
        DateOnly? assessmentDate)
    {
        var thisAntigenRecords = antigenRecords
            .Where(r => r.Antigen == series.Antigen)
            .OrderBy(r => r.DateAdministered)
            .ToArray();

        var otherAntigensHistory = state.PatientWideHistory
            .Where(d => d.Antigen != series.Antigen)
            .ToArray();

        var seriesResult = EvaluateSeriesHistory.Execute(
            patient, series, thisAntigenRecords, otherAntigensHistory,
            conflictsByImpactedCvx, groups => resolveCompletedSeries(series.Antigen, groups), assessmentDate);

        // Only contribute this antigen's history once (see the SIMPLIFICATION note above).
        // Deliberately keyed on "no doses for this antigen yet", not "first series for this
        // antigen": if the first same-antigen series yields no evaluated doses, a later one
        // still contributes. Preserved as-is; this refactor must not change behavior.
        var antigenAlreadyContributed = state.PatientWideHistory.Any(d => d.Antigen == series.Antigen);

        return state with
        {
            Results = state.Results.Add(new(series, seriesResult)),
            PatientWideHistory = antigenAlreadyContributed
                ? state.PatientWideHistory
                : state.PatientWideHistory.AddRange(seriesResult.AllEvaluatedDoses),
        };
    }
}
