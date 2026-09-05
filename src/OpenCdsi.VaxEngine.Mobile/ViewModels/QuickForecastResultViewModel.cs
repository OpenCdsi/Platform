using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.VaxEngine.Mobile.Data;
using OpenCdsi.VaxEngine.Mobile.Models;
using OpenCdsi.VaxEngine.Mobile.Services;

namespace OpenCdsi.VaxEngine.Mobile.ViewModels;

public partial class QuickForecastResultViewModel : ObservableObject
{
    private readonly QuickForecastViewModel _session;
    private readonly IForecastEngineAdapter _forecastEngine;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public QuickForecastResultViewModel(
        QuickForecastViewModel session,
        IForecastEngineAdapter forecastEngine,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _session = session;
        _forecastEngine = forecastEngine;
        _dbContextFactory = dbContextFactory;
    }

    public DateTime DateOfBirth => _session.DateOfBirth;
    public Gender Gender => _session.Gender;

    [ObservableProperty]
    private IReadOnlyList<ForecastEntry> entries = Array.Empty<ForecastEntry>();

    [RelayCommand]
    private async Task LoadAsync()
        => Entries = await _forecastEngine.ForecastAsync(_session.BuildTransientPatient());

    [RelayCommand]
    private async Task SaveAsPatientAsync()
    {
        // Quick-forecast entry deliberately never asks for a name — this is
        // the one moment a name becomes necessary, so it's asked for here,
        // right before promotion, rather than up front for every encounter.
        var page = Shell.Current.CurrentPage;
        var firstName = await page.DisplayPromptAsync("Save as patient", "First name");
        if (string.IsNullOrWhiteSpace(firstName)) return;

        var lastName = await page.DisplayPromptAsync("Save as patient", "Last name");
        if (string.IsNullOrWhiteSpace(lastName)) return;

        var patient = new Patient
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            DateOfBirth = DateOnly.FromDateTime(_session.DateOfBirth),
            Gender = _session.Gender
        };

        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            db.Patients.Add(patient);
            foreach (var dose in _session.Doses)
            {
                db.ImmunizationEvents.Add(new ImmunizationEvent
                {
                    PatientId = patient.Id,
                    CvxCode = dose.Vaccine.Code,
                    DateAdministered = dose.DateAdministered
                });
            }
            await db.SaveChangesAsync();
        }

        _session.Reset();

        // Pop both quick-forecast pages before pushing patient detail, so
        // back from there lands on the roster, not on stale entry/result
        // screens for a session that's now over.
        await Shell.Current.GoToAsync($"../../patientdetail?id={patient.Id}");
    }
}
