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
    private readonly ReferenceDataStore _referenceDataStore;
    private readonly Lazy<IReadOnlyList<CvxOption>> _all;
    private readonly Lazy<IReadOnlyDictionary<string, CvxOption>> _byCode;

    public CvxLookupService(ReferenceDataStore referenceDataStore)
    {
        _referenceDataStore = referenceDataStore;
        _all = new Lazy<IReadOnlyList<CvxOption>>(BuildOptions);
        _byCode = new Lazy<IReadOnlyDictionary<string, CvxOption>>(
            () => _all.Value.ToDictionary(o => o.Code));
    }

    public IReadOnlyList<CvxOption> Search(string query)
        => string.IsNullOrWhiteSpace(query)
            ? _all.Value
            : _all.Value
                .Where(o => o.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || o.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

    // Exact lookup for rendering a stored CvxCode back to its display name (e.g. dose history),
    // as opposed to Search()'s substring matching against user-typed input.
    public CvxOption? FindByCode(string code)
        => _byCode.Value.GetValueOrDefault(code);

    private IReadOnlyList<CvxOption> BuildOptions()
    {
        // Search() is called synchronously (from a ViewModel's OnSearchTextChanged), so this has
        // to block rather than await - but only the first time a vaccine-search screen is actually
        // opened, not at app startup. The background load MauiProgram.cs kicks off at launch has
        // almost always already finished by the time a user navigates this deep, so in practice
        // this returns immediately; it only genuinely waits if they get here unusually fast.
        var repository = _referenceDataStore.LoadAsync().GetAwaiter().GetResult();

        return repository.Schedule.CvxToAntigen.Values
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ShortDescription))
            .Select(entry => new CvxOption(
                Code: entry.Cvx,
                DisplayName: entry.ShortDescription!,
                IsUnspecified: entry.ShortDescription!.Contains("unspecified", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
