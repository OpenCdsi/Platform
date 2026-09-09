/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.Mobile.Data;
using OpenCdsi.Mobile.Models;
using OpenCdsi.Mobile.Services;

namespace OpenCdsi.Mobile.ViewModels;

[QueryProperty(nameof(PatientId), "patientId")]
public partial class ForecastResultViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IForecastEngineAdapter _forecastEngine;

    public ForecastResultViewModel(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IForecastEngineAdapter forecastEngine)
    {
        _dbContextFactory = dbContextFactory;
        _forecastEngine = forecastEngine;
    }

    [ObservableProperty]
    private string patientId = string.Empty;

    partial void OnPatientIdChanged(string value) => _ = LoadCommand.ExecuteAsync(null);

    [ObservableProperty]
    private Patient? patient;

    [ObservableProperty]
    private IReadOnlyList<ForecastEntry> entries = Array.Empty<ForecastEntry>();

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
                Entries = await _forecastEngine.ForecastAsync(Patient);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
