/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.VaxEngine.Core.Pipeline;

/// <summary>
/// Everything GeneratePatientForecast.Execute produces, plus per-dose evaluation detail for the
/// winning series per antigen - added for OpenCdsi.VaxEngine.Conformance.Tests, which needs to check
/// individual administered-dose outcomes (Valid/NotValid/Reason per §6, not just the merged
/// §9 vaccine-group-level forecast). The original Execute(...) overload below is unchanged and
/// still returns just VaccineGroupForecasts - every existing caller (OpenCdsi.VaxEngine.Api,
/// OpenCdsi.VaxEngine.Demo) that doesn't need per-dose detail is unaffected.
/// </summary>
public sealed class PatientForecastResult
{
    public required IReadOnlyList<VaccineGroupForecastResult> VaccineGroupForecasts { get; init; }

    /// <summary>
    /// Keyed by antigen name (matches AntigenSeries.Antigen) - the WINNING (§8-selected best)
    /// patient series' own dose-by-dose evaluation detail. An antigen with no relevant series at
    /// all simply has no entry here.
    ///
    /// SIMPLIFICATION, flagged: §8.8 can legitimately select more than one "best" series for the
    /// same antigen at once (a real, documented scenario elsewhere in this project - e.g. two
    /// equivalent HepB series groups both independently Complete). This dictionary holds only
    /// one SeriesHistoryResult per antigen, so in that rare case whichever series is processed
    /// last wins - a real, deliberate simplification for a genuinely rare edge case, not an
    /// oversight, matching how other multi-winner scenarios have been handled elsewhere in this
    /// project (see EvaluatePatientSeriesHistory's own "only contribute once per antigen" note).
    /// </summary>
    public required IReadOnlyDictionary<string, SeriesHistoryResult> DoseDetailsByAntigen { get; init; }
}

/// <summary>
/// The complete, end-to-end pipeline: raw administered doses in, merged vaccine group forecasts
/// out. Wires together everything this project has built - §4.2/§5.1 (organize history, find
/// relevant series), §4.4/§6 (evaluate immunization history per series, via
/// EvaluatePatientSeriesHistory), §7 (forecast per series, via GeneratePatientSeriesForecast),
/// §8 (select best patient series per series group, then per antigen, via
/// SelectPrioritizedPatientSeriesForGroup and DetermineBestPatientSeriesForAntigen), and §9
/// (merge into vaccine group forecasts, via MergeVaccineGroupForecast) - into one call.
///
/// Immunity and contraindication reference data are loaded per antigen FILE (not part of
/// AntigenSeries itself), so they're supplied here as dictionaries keyed by antigen name
/// (matching AntigenSeries.Antigen) rather than re-loaded internally.
///
/// §6.2's "Completed Series" condition is now resolved for real, via two evaluation passes -
/// see ResolveCompletedSeriesGroups for why that converges correctly for every real instance in
/// the dataset. This is why `resolveCompletedSeries` is no longer part of this function's public
/// signature at all: callers shouldn't need to know this internal mechanism exists.
///
/// REMAINING KNOWN GAPS, consistent with every other round this project has flagged rather than
/// guessed past: §7.5's `latestConflictEndDate`/`latestInadvertentAdministrationDate` remain
/// unset (null) for every series - the forward-looking calculations they'd need don't exist yet.
/// §9.3's multi-antigen `anyContainedIsPriorityForecast`/`latestAdministeredDateOfGroupVaccineTypes`
/// default to false/null for the same reason. Neither gap affects the (much more common)
/// single-antigen vaccine groups or non-priority multi-antigen cases at all.
/// </summary>
public static class GeneratePatientForecast
{
    /// <summary>The original, stable public entry point - unchanged. Still just merged vaccine group forecasts, for callers that don't need per-dose detail.</summary>
    public static IReadOnlyList<VaccineGroupForecastResult> Execute(
        Patient patient,
        IReadOnlyList<VaccineDoseAdministered> administeredDoses,
        IReadOnlyList<AntigenSeries> allSeries,
        ScheduleSupportingData schedule,
        IReadOnlyList<VaccineGroupInfo> vaccineGroups,
        IReadOnlyDictionary<string, AntigenImmunityData> immunityByAntigen,
        IReadOnlyDictionary<string, AntigenContraindicationData> contraindicationsByAntigen,
        DateOnly assessmentDate) =>
        ExecuteWithDoseDetail(
            patient, administeredDoses, allSeries, schedule, vaccineGroups,
            immunityByAntigen, contraindicationsByAntigen, assessmentDate).VaccineGroupForecasts;

    /// <summary>Same computation as Execute, plus per-antigen dose-by-dose detail for the winning series - see PatientForecastResult.</summary>
    public static PatientForecastResult ExecuteWithDoseDetail(
        Patient patient,
        IReadOnlyList<VaccineDoseAdministered> administeredDoses,
        IReadOnlyList<AntigenSeries> allSeries,
        ScheduleSupportingData schedule,
        IReadOnlyList<VaccineGroupInfo> vaccineGroups,
        IReadOnlyDictionary<string, AntigenImmunityData> immunityByAntigen,
        IReadOnlyDictionary<string, AntigenContraindicationData> contraindicationsByAntigen,
        DateOnly assessmentDate)
    {
        // §5.1: which series even apply to this patient (gender, etc.)
        var relevantSeries = CreateRelevantPatientSeries.Execute(patient, allSeries, assessmentDate).RelevantSeries;

        var (historyBySeries, resolveCompletedSeries) = EvaluateTwoPass(patient, relevantSeries, administeredDoses, schedule, assessmentDate);

        var patientWideHistoryByAntigen = FirstHistoryPerAntigen(historyBySeries);

        var membersByAntigen = ForecastEachSeries(
            patient, historyBySeries, patientWideHistoryByAntigen, schedule, immunityByAntigen,
            contraindicationsByAntigen, resolveCompletedSeries, assessmentDate);

        // §8: best patient series per antigen (across that antigen's own series groups), paired
        // with the forecast already computed for it above.
        var winnersByAntigen = membersByAntigen
            .Select(members => (Members: members, BestSeries: DetermineBestPatientSeriesForAntigen.Execute(members, patient.DateOfBirth, assessmentDate)))
            .ToArray();

        var doseDetailsByAntigen = winnersByAntigen
            .Select(w => ChooseRepresentativeMember(w.Members, w.BestSeries))
            .OfType<SeriesGroupMember>()
            .ToDictionary(m => m.Series.Antigen, m => m.SeriesHistory);

        return new PatientForecastResult
        {
            VaccineGroupForecasts = MergeVaccineGroups(winnersByAntigen, allSeries, vaccineGroups, patientWideHistoryByAntigen),
            DoseDetailsByAntigen = doseDetailsByAntigen
        };
    }

    /// <summary>
    /// Pass 1 evaluates assuming no series group is complete yet, purely to discover which ones
    /// actually are (SeriesHistoryResult.SeriesComplete) - §6.2's Completed Series condition
    /// needs this before it can be resolved for real. Pass 2 is authoritative: §4.2/§4.4/§6 with
    /// the real Completed Series resolver, plus the real cross-antigen vaccine conflict
    /// resolution EvaluatePatientSeriesHistory already wires in.
    /// </summary>
    private static (IReadOnlyDictionary<AntigenSeries, SeriesHistoryResult> HistoryBySeries, Func<string, string?, bool> ResolveCompletedSeries) EvaluateTwoPass(
        Patient patient,
        IReadOnlyList<AntigenSeries> relevantSeries,
        IReadOnlyList<VaccineDoseAdministered> administeredDoses,
        ScheduleSupportingData schedule,
        DateOnly assessmentDate)
    {
        var firstPassHistory = EvaluatePatientSeriesHistory.Execute(
            patient, relevantSeries, administeredDoses, schedule.CvxToAntigen, schedule.ConflictsByImpactedCvx,
            resolveCompletedSeries: (_, _) => false, assessmentDate);
        var resolveCompletedSeries = ResolveCompletedSeriesGroups.Build(firstPassHistory);

        var historyBySeries = EvaluatePatientSeriesHistory.Execute(
            patient, relevantSeries, administeredDoses, schedule.CvxToAntigen, schedule.ConflictsByImpactedCvx, resolveCompletedSeries, assessmentDate);

        return (historyBySeries, resolveCompletedSeries);
    }

    /// <summary>
    /// Patient-wide evaluated-dose history, one antigen's worth per antigen, needed for
    /// cross-antigen forecast conflict resolution (CALCDTCONFLICT-3) the same way
    /// EvaluatePatientSeriesHistory itself needs it for §6.7.
    ///
    /// NOT quite the same rule as EvaluatePatientSeriesHistory's own "only contribute once per
    /// antigen": this takes the FIRST series per antigen even when that series has no evaluated
    /// doses, whereas that one waits for a series that has some. They can disagree when an
    /// antigen's first series is empty. Preserved as-is; the refactor must not change behavior.
    /// Enumeration order (first appearance) is observable: it orders the concatenated
    /// cross-antigen doses handed to each series' forecast.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<EvaluatedAntigenDose>> FirstHistoryPerAntigen(
        IReadOnlyDictionary<AntigenSeries, SeriesHistoryResult> historyBySeries) =>
        historyBySeries
            .GroupBy(kv => kv.Key.Antigen)
            .ToDictionary(g => g.Key, g => g.First().Value.AllEvaluatedDoses);

    /// <summary>
    /// §7: forecast each series individually, grouped by antigen (first-appearance order, members
    /// in series order) for §8. Forecasts are computed in the same series order as before, so a
    /// missing-data error still names the first offending antigen.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<SeriesGroupMember>> ForecastEachSeries(
        Patient patient,
        IReadOnlyDictionary<AntigenSeries, SeriesHistoryResult> historyBySeries,
        IReadOnlyDictionary<string, IReadOnlyList<EvaluatedAntigenDose>> patientWideHistoryByAntigen,
        ScheduleSupportingData schedule,
        IReadOnlyDictionary<string, AntigenImmunityData> immunityByAntigen,
        IReadOnlyDictionary<string, AntigenContraindicationData> contraindicationsByAntigen,
        Func<string, string?, bool> resolveCompletedSeries,
        DateOnly assessmentDate)
    {
        SeriesGroupMember Forecast(AntigenSeries series, SeriesHistoryResult history)
        {
            if (!immunityByAntigen.TryGetValue(series.Antigen, out var immunity) ||
                !contraindicationsByAntigen.TryGetValue(series.Antigen, out var contraindications))
            {
                throw new InvalidOperationException($"No immunity/contraindication data supplied for antigen '{series.Antigen}'.");
            }

            var priorDosesAllAntigens = patientWideHistoryByAntigen
                .Where(kv => kv.Key != series.Antigen)
                .SelectMany(kv => kv.Value)
                .Select(EvaluateDoseAgainstTargetDose.MapToPriorDoseForSkipOrConflict)
                .ToArray();

            var forecast = GeneratePatientSeriesForecast.Execute(
                patient, series, history, assessmentDate, immunity, contraindications,
                priorDosesAllAntigens, schedule.ConflictsByImpactedCvx,
                groups => resolveCompletedSeries(series.Antigen, groups));

            return new SeriesGroupMember(series, history, forecast);
        }

        // Materialized before grouping so every forecast (and any throw) happens in series order.
        return historyBySeries
            .Select(kv => Forecast(kv.Key, kv.Value))
            .ToArray()
            .GroupBy(m => m.Series.Antigen)
            .Select(g => (IReadOnlyList<SeriesGroupMember>)g.ToArray())
            .ToArray();
    }

    /// <summary>Which one winning series' per-dose detail represents this antigen in DoseDetailsByAntigen; null if §8 picked none.</summary>
    private static SeriesGroupMember? ChooseRepresentativeMember(
        IReadOnlyList<SeriesGroupMember> members, IReadOnlyList<AntigenSeries> bestSeries)
    {
        // REAL BUG, FOUND AND FIXED - found via real corpus case 2013-0576 (Pneumococcal
        // Dose 1 PCV15 at 18 months): DetermineBestPatientSeriesForAntigen can legitimately
        // return MULTIPLE winning series for one antigen (its own doc comment: e.g. a
        // Standard-group series and a Risk-group series both surviving §8.8's cross-
        // referencing, since they're "not always substitutes for one another"). Confirmed via
        // a real diagnostic (PneumococcalInvestigationTests, replicating this exact pipeline
        // flow directly): for this patient, BOTH "Pneumococcal start at 12 months series"
        // (Standard, correctly satisfies Dose 1) AND "Pneumococcal 50+ 1-dose PCV series"
        // (Risk, for patients 50+ years old - nonsensically "Too young" for an 18-month-old,
        // yet still legitimately returned as a §8.8 winner) both won.
        //
        // doseDetailsByAntigen only has room for ONE entry per antigen (a Dictionary key),
        // and the previous code assigned it inside the same foreach loop that populates the
        // vaccine-group merge list below - a last-write-wins assignment, entirely dependent
        // on IReadOnlyList<AntigenSeries>'s own, unspecified iteration order from
        // DetermineBestPatientSeriesForAntigen. For this patient, the irrelevant Risk series
        // happened to come second, silently overwriting the correct Standard series's detail
        // with a nonsensical "Too young" result - which is exactly what the real conformance
        // corpus caught (2013-0576 and, very plausibly, a meaningful fraction of
        // Pneumococcal's other 59 real corpus failures, since Pneumococcal has an unusually
        // large number of age-bracketed Risk series - see MenB for another antigen with a
        // real Standard/Risk split that could show the same pattern).
        //
        // Fixed by choosing the doseDetailsByAntigen representative EXPLICITLY and separately
        // from the merge-eligible list below, preferring SeriesType.Standard over Risk or
        // EvaluationOnly when more than one winner exists - not because Risk series are never
        // correct (a patient with a real, documented risk indication legitimately needs one),
        // but because within this project's real, non-risk-condition test corpus, a Risk
        // series winning ALONGSIDE a Standard series for the same antigen is exactly the
        // "not always substitutes for one another" scenario DetermineBestPatientSeriesForAntigen's
        // own doc comment describes - both are valid §8.8 survivors, but the Standard one is
        // the meaningful, general-population representative for per-dose conformance
        // reporting purposes. Falls back to whichever series exists if none is Standard (e.g.
        // a patient who legitimately only has Risk-series winners) - unaffected, unchanged
        // behavior from before this fix.
        //
        // EXTENDED - found via real corpus case 2024-0056 (RSV, 75-year-old with one Arexvy
        // dose): the SeriesType preference alone isn't enough when the multi-winner scenario
        // involves TWO Standard-type series from different groups, not Standard-vs-Risk. RSV
        // has "RSV 1-dose series" (group 1, no age restriction at all) and "RSV 75 years+
        // 1-dose series" (group 3, real minAgeToStart is 50 years) - BOTH SeriesType.Standard,
        // both defaultSeries=Yes, both legitimately relevant and both winning §8.8 for a
        // 75-year-old. Confirmed via a real diagnostic (RsvInvestigationTests, same pipeline-
        // tracing pattern as the Pneumococcal one): "RSV 1-dose series" comes back
        // PatientSeriesStatus.AgedOut (the dose flagged "Inadvertent Administration" - this
        // series genuinely isn't meant for this patient), while "RSV 75 years+ 1-dose series"
        // correctly comes back Complete, matching the real corpus exactly. Deliberately
        // narrow, not a general ranking of all six PatientSeriesStatus values: among series
        // tied on the SeriesType preference above, prefer whichever is NOT AgedOut - AgedOut
        // specifically means "this series doesn't really apply to this patient anymore," the
        // same underlying reason a Risk series lost to a Standard one above.
        //
        // CONFIRMED INCOMPLETE ON ITS OWN, by a real run against 2024-0056 directly: this
        // correctly fixes doseDetailsByAntigen's own per-dose Valid/NotValid conformance
        // detail, one of the corpus's own two real mismatches for this case - but the
        // OTHER mismatch, the group's own seriesStatus (AgedOut instead of Complete), comes
        // from a completely different, untouched computation - MergeVaccineGroupForecast
        // reading bestSeriesByVaccineGroup, which still contains both winners unchanged. See
        // SingleAntigenVaccineGroup.Status's own doc comment for that second, necessary fix.
        var representativeSeries = bestSeries
            .Select(s => (Series: s, Member: members.First(m => m.Series == s)))
            .OrderBy(x => x.Series.SeriesType == SeriesType.Standard ? 0 : 1)
            .ThenBy(x => x.Member.Forecast.Status == PatientSeriesStatus.AgedOut ? 1 : 0)
            .Select(x => x.Series)
            .FirstOrDefault();

        return representativeSeries is null ? null : members.First(m => m.Series == representativeSeries);
    }

    /// <summary>
    /// §9: merge each vaccine group's contained best-series forecasts into one result. Groups
    /// appear in first-appearance order across antigens then winners, which is the order of the
    /// returned list.
    /// </summary>
    private static IReadOnlyList<VaccineGroupForecastResult> MergeVaccineGroups(
        IReadOnlyList<(IReadOnlyList<SeriesGroupMember> Members, IReadOnlyList<AntigenSeries> BestSeries)> winnersByAntigen,
        IReadOnlyList<AntigenSeries> allSeries,
        IReadOnlyList<VaccineGroupInfo> vaccineGroups,
        IReadOnlyDictionary<string, IReadOnlyList<EvaluatedAntigenDose>> patientWideHistoryByAntigen)
    {
        var bestSeriesByVaccineGroup = winnersByAntigen
            .SelectMany(w => w.BestSeries.Select(series => (Series: series, w.Members.First(m => m.Series == series).Forecast)))
            // no vaccine group classifies this antigen - nothing to merge into
            .Where(x => x.Series.VaccineGroup is not null)
            .GroupBy(x => x.Series.VaccineGroup!);

        return bestSeriesByVaccineGroup
            .Select(group =>
            {
                var vaccineGroupName = group.Key;
                var contained = group.ToArray();

                var antigensInGroup = allSeries.Where(s => s.VaccineGroup == vaccineGroupName).Select(s => s.Antigen).Distinct().ToArray();
                var type = VaccineGroupClassification.Classify(antigensInGroup);
                var administerFull = vaccineGroups.FirstOrDefault(v => v.Name == vaccineGroupName)?.AdministerFullVaccineGroup ?? false;

                // §9.3 MULTIANTVG-1's two remaining inputs, only meaningful for multi-antigen groups
                // (MMR, DTaP/Tdap/Td in real data) - harmless to compute for single-antigen groups
                // too, since SingleAntigenVaccineGroup.EarliestDate never reads them.
                var anyContainedIsPriorityForecast = contained.Any(x => x.Forecast.IsPriorityForecast);
                var latestAdministeredDateOfGroupVaccineTypes = antigensInGroup
                    .Where(patientWideHistoryByAntigen.ContainsKey)
                    .SelectMany(antigen => patientWideHistoryByAntigen[antigen])
                    .Select(d => (DateOnly?)d.DateAdministered)
                    .Max();

                return MergeVaccineGroupForecast.Execute(
                    vaccineGroupName, type, administerFull, contained,
                    anyContainedIsPriorityForecast, latestAdministeredDateOfGroupVaccineTypes);
            })
            .ToArray();
    }
}
