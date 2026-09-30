/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.VaxEngine.Core.Pipeline;

/// <summary>
/// §8.8's per-antigen orchestration: runs §8.1-§8.7 (via SelectPrioritizedPatientSeriesForGroup)
/// once per series group for an antigen, then cross-references each group's own prioritized
/// series against its `equivalentSeriesGroups` counterpart to decide which prioritized series
/// are actually "best" for the antigen. Per the chapter's own framing, this can legitimately
/// return more than one series (e.g. a Standard-group series and a Risk-group series both
/// surviving as best, since they're not always substitutes for one another) or none at all.
///
/// Takes every relevant series for ONE antigen across ALL its series groups - the caller is
/// responsible for having already scoped `allMembersForAntigen` to a single antigen (grouping
/// happens internally here by `SeriesGroupInfo.SeriesGroup`, which is only unique WITHIN one
/// antigen's own file - the same group ID string in a different antigen means something
/// unrelated, so mixing series from different antigens into one call here would silently
/// produce nonsense).
/// </summary>
public static class DetermineBestPatientSeriesForAntigen
{
    public static IReadOnlyList<AntigenSeries> Execute(
        IReadOnlyList<SeriesGroupMember> allMembersForAntigen,
        DateOnly dateOfBirth,
        DateOnly assessmentDate)
    {
        // Compute §8.1-§8.7's prioritized series (and its own forecast) for every group first -
        // §8.8 needs every group's result available before it can cross-reference any of them.
        // Materialized in GroupBy's first-appearance order, which is also the order of the result.
        var prioritized = allMembersForAntigen
            .GroupBy(m => m.Series.SeriesGroupInfo.SeriesGroup)
            .Select(group => (GroupId: group.Key, Member: PrioritizedMember(group.ToArray(), dateOfBirth, assessmentDate)))
            .ToArray();

        var prioritizedByGroup = prioritized.ToDictionary(p => p.GroupId, p => p.Member);

        // A group with no resolvable prioritized series has nothing to evaluate for "best".
        // ToList keeps the returned runtime type identical to before the refactor.
        return prioritized
            .Select(p => p.Member)
            .OfType<SeriesGroupMember>()
            .Where(member => IsBest(member, prioritizedByGroup))
            .Select(member => member.Series)
            .ToList();
    }

    private static SeriesGroupMember? PrioritizedMember(SeriesGroupMember[] groupMembers, DateOnly dateOfBirth, DateOnly assessmentDate)
    {
        var prioritizedSeries = SelectPrioritizedPatientSeriesForGroup.Execute(groupMembers, dateOfBirth, assessmentDate);
        return prioritizedSeries is not null
            ? groupMembers.FirstOrDefault(m => m.Series == prioritizedSeries)
            : null;
    }

    /// <summary>Table 8-14, cross-referencing this group's prioritized series against its equivalentSeriesGroups counterpart.</summary>
    private static bool IsBest(SeriesGroupMember member, IReadOnlyDictionary<string, SeriesGroupMember?> prioritizedByGroup)
    {
        var equivalentGroupId = member.Series.EquivalentSeriesGroup;
        var equivalentMember = equivalentGroupId is not null && prioritizedByGroup.TryGetValue(equivalentGroupId, out var eq) ? eq : null;

        var isComplete = ClassifyScorablePatientSeries.IsCompletePatientSeries(member.Forecast.Status);
        var equivalentGroupHasComplete = equivalentMember is not null && ClassifyScorablePatientSeries.IsCompletePatientSeries(equivalentMember.Forecast.Status);
        var equivalentGroupHasRisk = equivalentMember is not null && equivalentMember.Series.SeriesType == SeriesType.Risk;

        return DetermineBestPatientSeries.IsBestPatientSeries(isComplete, equivalentGroupHasComplete, member.Series.SeriesType, equivalentGroupHasRisk);
    }
}
