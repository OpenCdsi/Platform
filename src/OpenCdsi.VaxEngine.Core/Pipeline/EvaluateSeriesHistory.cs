/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Immutable;
using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.VaxEngine.Core.Pipeline;

/// <summary>One administered record's outcome against whichever target dose it was (or wasn't) evaluated against.</summary>
public sealed record DoseEvaluationRecord(AntigenAdministered AdministeredDose, int? TargetDoseNumber, TargetDoseEvaluationResult Result);

public sealed class SeriesHistoryResult
{
    public required IReadOnlyList<DoseEvaluationRecord> DoseResults { get; init; }
    public required IReadOnlyList<EvaluatedAntigenDose> AllEvaluatedDoses { get; init; }

    /// <summary>Null if every target dose was satisfied/skipped (series complete) - otherwise the dose number that still needs to be forecast.</summary>
    public required int? CurrentTargetDoseNumber { get; init; }
    public bool SeriesComplete => CurrentTargetDoseNumber is null;
}

/// <summary>
/// §4.4 Evaluate and Forecast All Relevant Patient Series - specifically the "Evaluate
/// Immunization History" sub-process (Figure 4-6), implementing its exact 7-step two-pointer
/// algorithm over target doses and antigen-administered records.
///
/// RECURRING DOSE (§4.4 step 5) is now implemented. The spec's own text: on satisfying a target
/// dose flagged recurring, "initialize a new target dose identical to the current target dose...
/// immediately following the current target dose" and move to THAT clone next, rather than
/// advancing to whatever's genuinely next in the series. This codebase achieves the identical
/// logical effect without ever mutating or growing the target-dose array: when a recurring
/// target dose is Satisfied, `targetIdx` simply doesn't advance - the SAME target dose (with its
/// own already-general interval/age rules, typically `fromPrevious`) gets re-evaluated against
/// the NEXT administered record, using the just-updated `targetDoseSatisfiedDates` entry as the
/// new reference point. A "clone inserted after the original" and "the same slot re-used
/// in-place" are observationally identical here, since nothing else in the array shifts either
/// way. Confirmed against real data before implementing: every one of the 29 real recurring
/// doses (Td boosters, annual COVID, occupational rabies exposure, etc.) is the LAST target dose
/// in its series, so a genuinely recurring series is now correctly NEVER "complete" -
/// `CurrentTargetDoseNumber` stays pinned on the recurring dose indefinitely, exactly matching
/// the real-world fact that Td boosters, for instance, never stop being due every ~10 years.
///
/// ONE THING STILL NOT SPEC-GROUNDED, FLAGGED AS AN INFERENCE (the §4.4 algorithm only discusses
/// "Satisfied" vs "Not Satisfied" - it predates/doesn't address Table 6-11's "Skipped" status):
/// a Skipped target dose advances the target-dose pointer WITHOUT consuming the
/// administered-dose pointer - the administered record remains available to be tried against
/// the next target dose, since Skipped means "this target dose didn't need this dose at all,"
/// not "this dose satisfied it." This applies even to a recurring dose that gets Skipped - the
/// spec's own step 5 text only triggers recurrence checking after step 4a (Satisfied), so a
/// Skipped recurring dose is treated the same as any other Skipped dose (advance, don't clone).
///
/// A SECOND, REAL GAP FOUND AND FIXED - not spec text this time, but the real CDC supporting data
/// itself: the §4.4 algorithm's own literal text ("if the antigen administered collection is
/// empty, the evaluation process... ends") means Conditional Skip (§6.2) can only ever be
/// evaluated from WITHIN this loop, since Table 6-6's decision only runs when there's an
/// administered record to evaluate a target dose against. But the real Pertussis/Diphtheria/
/// Tetanus data has standalone, dose-count-independent Evaluation-context Age conditions on
/// Doses 1-6 (confirmed by reading the real XML directly, `&lt;set&gt;`-by-`&lt;set&gt;`, after an earlier,
/// less careful reading wrongly merged two separate OR'd sets into one AND'd condition) - Doses
/// 1-3/5 skip at Age >= 7 years, Dose 4 at Age >= 4 years, Dose 6 unconditionally at Age >= 7
/// years too. Dose 7's own age window (`minAge: 7 years`, confirmed identical on both the
/// standard and "start at 12 months" series) is exactly the age-anchored recommendation a real
/// catch-up patient needs - but a genuinely zero-dose (or partially-vaccinated-then-exhausted)
/// patient's `CurrentTargetDoseNumber` could never reach it, since the loop above only advances
/// the pointer in step with an administered record actually being consumed.
///
/// Fixed with a second pass, opt-in via the new `assessmentDate` parameter (defaulting to null,
/// so every existing caller that doesn't pass one keeps the exact prior behavior - this project's
/// established additive-change pattern, same as `GeneratePatientForecast.ExecuteWithDoseDetail`):
/// after the main loop settles, wherever it landed, keep advancing past any remaining target
/// dose whose Evaluation-context Conditional Skip is satisfied using the patient's CURRENT age
/// (the assessment date as reference, since there's no administered dose to anchor to - the
/// closest real analogue to "if this patient walked in today, would this target dose even apply
/// to them"). This handles the zero-dose case AND the "ran out of administered records partway
/// through a still-skippable stretch" case the same way, since both share the identical
/// structural gap.
///
/// A THIRD GAP was investigated (real conformance cases 2020-0004/2020-0005: adult patients
/// starting/continuing DTaP/Tdap/Td catch-up with exactly one prior valid dose, still forecasting
/// "today" instead of the corpus's expected date) and an explicit Dose-7 "auto-satisfy" ASSUMPTION
/// was implemented, then REVERTED after real dotnet test execution disproved the trace it was
/// built on: the main loop's OWN pre-existing per-dose Age skip (documented in the second gap
/// above) already advances a single adult-administered dose straight through Doses 1-6 to Dose 7
/// without any fast-forward pass involved at all, so the auto-satisfy logic never even applied to
/// the cases it was designed for - and its presence correlated with a net increase in conformance
/// failures elsewhere (255 -> 262) that wasn't confirmed safe. 2020-0004/2020-0005 remain
/// genuinely unresolved; the real explanation is now believed to live in how Diphtheria or
/// Tetanus behave differently from Pertussis for the same dose, or in the multi-antigen merge -
/// not in anything this class does per-antigen. Flagged here rather than silently dropped, so a
/// future attempt at this doesn't have to rediscover the same dead end.
/// </summary>
public static class EvaluateSeriesHistory
{
    /// <param name="antigenAdministeredRecords">This antigen's own records only, ascending date order (as produced by OrganizeImmunizationHistory).</param>
    /// <param name="priorEvaluatedDosesFromOtherAntigens">Patient-wide history already evaluated from OTHER antigens/series, needed only for cross-antigen Vaccine Conflict (§6.7). Pass empty if evaluating in isolation.</param>
    /// <param name="assessmentDate">Opt-in: when supplied, an additional pass after the main loop advances past any remaining target dose whose Evaluation-context Conditional Skip is satisfied using the patient's current age at this date - see the class doc comment's second gap. Null (the default) preserves the exact prior behavior for callers that don't need this.</param>
    public static SeriesHistoryResult Execute(
        Patient patient,
        AntigenSeries series,
        IReadOnlyList<AntigenAdministered> antigenAdministeredRecords,
        IReadOnlyList<EvaluatedAntigenDose> priorEvaluatedDosesFromOtherAntigens,
        IReadOnlyDictionary<string, IReadOnlyList<VaccineConflictRule>> conflictsByImpactedCvx,
        Func<string?, bool> resolveCompletedSeries,
        DateOnly? assessmentDate = null)
    {
        var targetDoses = series.SeriesDoses.OrderBy(d => d.DoseNumber).ToArray();

        var walked = WalkTargetDoses(
            SeriesWalkState.Initial, targetDoses, antigenAdministeredRecords, patient,
            priorEvaluatedDosesFromOtherAntigens, conflictsByImpactedCvx, resolveCompletedSeries);

        var settled = MarkRemainingExtraneous(walked, targetDoses, antigenAdministeredRecords);

        var positioned = assessmentDate is DateOnly today
            ? FastForwardSkippable(settled, targetDoses, patient, today, resolveCompletedSeries)
            : settled;

        return new SeriesHistoryResult
        {
            DoseResults = positioned.DoseResults,
            AllEvaluatedDoses = positioned.EvaluatedThisAntigen,
            CurrentTargetDoseNumber = positioned.TargetIdx < targetDoses.Length ? targetDoses[positioned.TargetIdx].DoseNumber : null
        };
    }

    /// <summary>
    /// Everything §4.4's two-pointer walk carries between steps. Immutable so each step's
    /// inputs to EvaluateDoseAgainstTargetDose are a snapshot, never a collection a later step
    /// could change underneath it.
    /// </summary>
    private sealed record SeriesWalkState(
        int TargetIdx,
        int AdminIdx,
        ImmutableList<EvaluatedAntigenDose> EvaluatedThisAntigen,
        ImmutableDictionary<int, DateOnly> TargetDoseSatisfiedDates,
        ImmutableList<DoseEvaluationRecord> DoseResults)
    {
        public static readonly SeriesWalkState Initial = new(0, 0, [], ImmutableDictionary<int, DateOnly>.Empty, []);
    }

    /// <summary>§4.4 steps 1-7: iterate WalkStep until either pointer runs off its collection.</summary>
    private static SeriesWalkState WalkTargetDoses(
        SeriesWalkState state,
        SeriesDose[] targetDoses,
        IReadOnlyList<AntigenAdministered> records,
        Patient patient,
        IReadOnlyList<EvaluatedAntigenDose> priorEvaluatedDosesFromOtherAntigens,
        IReadOnlyDictionary<string, IReadOnlyList<VaccineConflictRule>> conflictsByImpactedCvx,
        Func<string?, bool> resolveCompletedSeries)
    {
        // A loop over immutable states rather than recursion: C# has no guaranteed tail calls,
        // and the step count grows with dose history length.
        while (state.TargetIdx < targetDoses.Length && state.AdminIdx < records.Count)
        {
            state = WalkStep(state, targetDoses[state.TargetIdx], records[state.AdminIdx], patient,
                priorEvaluatedDosesFromOtherAntigens, conflictsByImpactedCvx, resolveCompletedSeries);
        }

        return state;
    }

    private static SeriesWalkState WalkStep(
        SeriesWalkState state,
        SeriesDose targetDose,
        AntigenAdministered adminRecord,
        Patient patient,
        IReadOnlyList<EvaluatedAntigenDose> priorEvaluatedDosesFromOtherAntigens,
        IReadOnlyDictionary<string, IReadOnlyList<VaccineConflictRule>> conflictsByImpactedCvx,
        Func<string?, bool> resolveCompletedSeries)
    {
        var priorAllAntigens = priorEvaluatedDosesFromOtherAntigens.Concat(state.EvaluatedThisAntigen).ToArray();

        var result = EvaluateDoseAgainstTargetDose.Execute(
            patient, adminRecord.SourceDose, targetDose,
            state.EvaluatedThisAntigen, priorAllAntigens, state.TargetDoseSatisfiedDates,
            conflictsByImpactedCvx, resolveCompletedSeries);

        var recorded = state with
        {
            DoseResults = state.DoseResults.Add(new DoseEvaluationRecord(adminRecord, targetDose.DoseNumber, result))
        };

        return result.TargetDoseStatus switch
        {
            TargetDoseStatus.Satisfied => recorded with
            {
                EvaluatedThisAntigen = recorded.EvaluatedThisAntigen.Add(new EvaluatedAntigenDose(
                    adminRecord.Antigen, adminRecord.Cvx, adminRecord.DateAdministered, result.EvaluationStatus, targetDose.DoseNumber)),
                // SetItem overwrites, so a recurring dose's reference date re-anchors to its latest occurrence.
                TargetDoseSatisfiedDates = recorded.TargetDoseSatisfiedDates.SetItem(targetDose.DoseNumber, adminRecord.DateAdministered),
                AdminIdx = recorded.AdminIdx + 1, // step 7 - this record is consumed either way
                // step 5/6: a recurring target dose stays in place (re-evaluated against the next
                // administered record, using the reference date just updated above) instead of
                // advancing to a genuinely different target dose - see class doc comment.
                TargetIdx = targetDose.IsRecurringDose ? recorded.TargetIdx : recorded.TargetIdx + 1,
            },
            // INFERENCE - see class doc comment. AdminIdx deliberately NOT advanced - the record
            // remains for the next target dose.
            TargetDoseStatus.Skipped => recorded with { TargetIdx = recorded.TargetIdx + 1 },
            // NotSatisfied: step 7 - target dose stays the same, try the next administered record.
            _ => recorded with
            {
                EvaluatedThisAntigen = recorded.EvaluatedThisAntigen.Add(new EvaluatedAntigenDose(
                    adminRecord.Antigen, adminRecord.Cvx, adminRecord.DateAdministered, result.EvaluationStatus, null)),
                AdminIdx = recorded.AdminIdx + 1,
            },
        };
    }

    /// <summary>
    /// Step 6a: if the target dose collection is exhausted, any remaining antigen administered
    /// records get evaluation status 'Extraneous', not just left unprocessed.
    /// </summary>
    private static SeriesWalkState MarkRemainingExtraneous(
        SeriesWalkState state, SeriesDose[] targetDoses, IReadOnlyList<AntigenAdministered> records)
    {
        if (state.TargetIdx < targetDoses.Length)
        {
            return state;
        }

        var remaining = records.Skip(state.AdminIdx).ToArray();
        var extraneousResult = TargetDoseEvaluationResult.NotSatisfied(EvaluationStatus.Extraneous, "Series already complete");

        return state with
        {
            DoseResults = state.DoseResults.AddRange(
                remaining.Select(r => new DoseEvaluationRecord(r, null, extraneousResult))),
            EvaluatedThisAntigen = state.EvaluatedThisAntigen.AddRange(
                remaining.Select(r => new EvaluatedAntigenDose(r.Antigen, r.Cvx, r.DateAdministered, EvaluationStatus.Extraneous, null))),
            AdminIdx = records.Count,
        };
    }

    /// <summary>
    /// Second pass - see class doc comment's "second gap": fast-forward past any remaining target
    /// dose whose Evaluation-context Conditional Skip is satisfied given the patient's CURRENT
    /// age, wherever the walk left off (TargetIdx 0 for a genuinely zero-dose patient, or
    /// wherever administered records ran out for anyone else). No DoseEvaluationRecord is added
    /// for a fast-forwarded dose - nothing was administered to record an outcome for; only
    /// CurrentTargetDoseNumber reflects the new position.
    /// </summary>
    private static SeriesWalkState FastForwardSkippable(
        SeriesWalkState state, SeriesDose[] targetDoses, Patient patient, DateOnly today, Func<string?, bool> resolveCompletedSeries)
    {
        var priorForSkip = state.EvaluatedThisAntigen.Select(EvaluateDoseAgainstTargetDose.MapToPriorDoseForSkipOrConflict).ToArray();

        var skippable = targetDoses
            .Skip(state.TargetIdx)
            .TakeWhile(candidateDose => EvaluateConditionalSkip.CanBeSkipped(
                patient.DateOfBirth, today, ConditionalSkipContext.Evaluation,
                candidateDose.ConditionalSkipInstances, priorForSkip, resolveCompletedSeries))
            .Count();

        return state with { TargetIdx = state.TargetIdx + skippable };
    }
}
