/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.Mobile.Data;
using OpenCdsi.Mobile.Models;

namespace OpenCdsi.Mobile.ViewModels;

[QueryProperty(nameof(PatientId), "id")]
public partial class PatientDetailViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public PatientDetailViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [ObservableProperty]
    private string patientId = string.Empty;

    partial void OnPatientIdChanged(string value) => _ = LoadCommand.ExecuteAsync(null);

    [ObservableProperty]
    private Patient? patient;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(PatientId, out var id)) return;

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        // The query filter on ImmunizationEvent already excludes voided
        // doses, so this naturally shows only active history.
        Patient = await db.Patients
            .Include(p => p.ImmunizationEvents.OrderByDescending(e => e.DateAdministered))
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    [RelayCommand]
    private async Task AddDoseAsync()
        => await Shell.Current.GoToAsync($"adddose?patientId={PatientId}");

    [RelayCommand]
    private async Task ViewForecastAsync()
        => await Shell.Current.GoToAsync($"forecastresult?patientId={PatientId}");

    [RelayCommand]
    private async Task EditPatientAsync()
        => await Shell.Current.GoToAsync($"addpatient?id={PatientId}");

    [RelayCommand]
    private async Task DeletePatientAsync()
    {
        if (Patient is null) return;

        var page = Shell.Current.CurrentPage;
        var confirmed = await page.DisplayAlert(
            "Remove patient",
            $"Remove {Patient.FullName} and their entire immunization history? This can't be undone.",
            "Remove", "Cancel");
        if (!confirmed) return;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.Patients.FirstOrDefaultAsync(p => p.Id == Patient.Id);
        if (entity is null) return;

        // ImmunizationEvent.PatientId is a required FK, so EF's default (and what
        // EnsureCreated() actually applies) is cascade delete - no need to remove doses first.
        db.Patients.Remove(entity);
        await db.SaveChangesAsync();

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task VoidDoseAsync(ImmunizationEvent? dose)
    {
        if (dose is null) return;

        var page = Shell.Current.CurrentPage;
        var confirmed = await page.DisplayAlert(
            "Remove dose",
            $"Remove this dose (CVX {dose.CvxCode}, {dose.DateAdministered:dd MMM yyyy}) from the record?",
            "Remove", "Cancel");
        if (!confirmed) return;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.ImmunizationEvents.FirstOrDefaultAsync(e => e.Id == dose.Id);
        if (entity is null) return;

        // Voided, not deleted - forecasts are computed against history, so silently removing a
        // dose would change future forecast results with no trace of why (see AppDbContext's own
        // query filter, which already excludes voided doses from every normal query).
        entity.IsVoided = true;
        entity.VoidedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await LoadCommand.ExecuteAsync(null);
    }
}
