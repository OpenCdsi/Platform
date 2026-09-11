/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.ClinicalReference;
using OpenCdsi.Mobile.Data;
using OpenCdsi.Mobile.Models;
using OpenCdsi.Mobile.Services;

namespace OpenCdsi.Mobile.ViewModels;

[QueryProperty(nameof(PatientId), "patientId")]
public partial class ForecastResultViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IForecastEngineAdapter _forecastEngine;
    private readonly ClinicalReferenceStore _clinicalReferenceStore;

    public ForecastResultViewModel(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IForecastEngineAdapter forecastEngine,
        ClinicalReferenceStore clinicalReferenceStore)
    {
        _dbContextFactory = dbContextFactory;
        _forecastEngine = forecastEngine;
        _clinicalReferenceStore = clinicalReferenceStore;
    }

    [ObservableProperty]
    private string patientId = string.Empty;

    partial void OnPatientIdChanged(string value) => _ = LoadCommand.ExecuteAsync(null);

    [ObservableProperty]
    private Patient? patient;

    [ObservableProperty]
    private IReadOnlyList<ForecastRowViewModel> rows = Array.Empty<ForecastRowViewModel>();

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(PatientId, out var id)) return;

        IsBusy = true;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            Patient = await db.Patients
                .Include(p => p.ImmunizationEvents)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (Patient is not null)
            {
                var entries = await _forecastEngine.ForecastAsync(Patient);
                var chapters = await _clinicalReferenceStore.LoadAsync();
                Rows = entries.Select(entry => BuildRow(entry, chapters)).ToList();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Nothing left to look up once a series is Complete or the patient is already Immune - every
    // other status is a real question ("why not now", "why not recommended") a curated chapter can
    // answer, if one exists for this antigen yet. entry.AntigenName is a vaccine group name, which
    // usually matches an AntigenChapter key one-to-one, but a few groups (DTaP/Tdap/Td, MMR) span
    // several chapters at once - see VaccineGroupAntigenAliases.
    private static ForecastRowViewModel BuildRow(ForecastEntry entry, ClinicalReferenceRepository chapters)
    {
        if (entry.Status is ForecastStatus.Complete or ForecastStatus.Immune)
            return new ForecastRowViewModel(entry, ReferenceGlyphState.Hidden, chapterAntigenKey: null);

        var antigenKey = VaccineGroupAntigenAliases.AntigenKeysFor(entry.AntigenName)
            .FirstOrDefault(key => chapters.TryGetByAntigen(key) is not null);

        return antigenKey is not null
            ? new ForecastRowViewModel(entry, ReferenceGlyphState.Enabled, antigenKey)
            : new ForecastRowViewModel(entry, ReferenceGlyphState.Disabled, chapterAntigenKey: null);
    }

    // The one info glyph does double duty depending on GlyphState: Enabled navigates to the
    // chapter, closing any other row's open tooltip on the way out; Disabled just toggles this
    // row's own inline tooltip (see ForecastRowViewModel.ToggleTooltip). Contraindicated/
    // NotRecommended results land on a pre-expanded Contraindications section - see
    // ChapterDetailViewModel's AutoJumpFlag - everything else opens at the top, collapsed.
    [RelayCommand]
    private async Task OpenReferenceAsync(ForecastRowViewModel? row)
    {
        if (row is null) return;

        if (row.GlyphState == ReferenceGlyphState.Disabled)
        {
            row.ToggleTooltip();
            return;
        }

        if (row.GlyphState != ReferenceGlyphState.Enabled || row.ChapterAntigenKey is null) return;

        CloseTooltips();

        var route = $"chapterdetail?antigenKey={Uri.EscapeDataString(row.ChapterAntigenKey)}";
        if (row.Status is ForecastStatus.Contraindicated or ForecastStatus.NotRecommended)
            route += "&autoJump=true";

        await Shell.Current.GoToAsync(route);
    }

    // Bound to a tap on the page background so an open tooltip dismisses on a tap anywhere else,
    // not just its own 3s timeout or a second tap on the same glyph.
    [RelayCommand]
    private void CloseTooltips()
    {
        foreach (var row in Rows)
            row.CloseTooltip();
    }
}
