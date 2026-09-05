using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// Built from OpenCdsi.VaxEngine.Core's own schedule reference data
// (ScheduleSupportingData.CvxToAntigen) rather than a hand-typed sample list - every CVX code and
// short description the engine itself knows about, so this list can never disagree with what the
// forecast adapter is actually reasoning over.
//
// "Unspecified formulation" isn't a structured flag anywhere in the source data (schedule XML or
// the C# model) - it's purely a naming convention inside each code's free-text description, and
// not even a consistent one ("Hib, unspecified formulation", "OPV, Unspecified", "Td(adult)
// unspecified formulation" with no comma). Detected here with a case-insensitive substring match,
// same precision the underlying data actually offers - no more.
public class CvxLookupService
{
    private readonly IReadOnlyList<CvxOption> _all;

    public CvxLookupService(ReferenceDataStore referenceDataStore)
    {
        _all = referenceDataStore.Repository.Schedule.CvxToAntigen.Values
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ShortDescription))
            .Select(entry => new CvxOption(
                Code: entry.Cvx,
                DisplayName: entry.ShortDescription!,
                IsUnspecified: entry.ShortDescription!.Contains("unspecified", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<CvxOption> Search(string query)
        => string.IsNullOrWhiteSpace(query)
            ? _all
            : _all.Where(o => o.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
}
