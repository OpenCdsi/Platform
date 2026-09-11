/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.Mobile.Models;

namespace OpenCdsi.Mobile.Services;

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
        // to block rather than await. This is NOT limited to "the first time a vaccine-search
        // screen is opened" - QuickForecastViewModel calls Search() in its own constructor, and
        // PatientsViewModel's constructor requires QuickForecastViewModel (a singleton), so this
        // runs unconditionally on the UI thread on every single app launch, via the very first
        // screen. That makes ReferenceDataStore.LoadAsync's ConfigureAwait(false) discipline (see
        // ReferenceDataProvisioner) load-bearing, not optional: without it, this blocking call
        // deadlocks permanently whenever the background load (kicked off in MauiProgram.cs) hasn't
        // already completed by the time Patients first constructs its ViewModel - confirmed as a
        // 100%-reproducible hang on physical Android hardware.
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
