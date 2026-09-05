using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.VaxEngine.Mobile.Data;
using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.ViewModels;

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
        // Not built yet — the next natural stop after these two pages,
        // since this is where the actual vaxengine.core call happens.
        => await Shell.Current.GoToAsync($"forecastresult?patientId={PatientId}");
}
