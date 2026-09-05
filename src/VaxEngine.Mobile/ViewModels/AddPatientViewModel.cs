using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using VaxEngine.Mobile.Data;
using VaxEngine.Mobile.Models;

namespace VaxEngine.Mobile.ViewModels;

// Three modes, distinguished by which query params arrive:
//  - no "id", no "fromQuickForecast": plain create, reached from the roster's "Add patient"
//    button - blank form.
//  - "id" set: edit, reached from PatientDetailPage's "Edit" toolbar item - loads the existing
//    patient into the same form instead of starting blank.
//  - "fromQuickForecast=true": create, reached from QuickForecastResultPage's "Save as patient" -
//    DOB/gender pre-filled from that session, and its doses get attached to the new patient on
//    save instead of the session's own now-removed direct-save path.
[QueryProperty(nameof(PatientId), "id")]
[QueryProperty(nameof(FromQuickForecast), "fromQuickForecast")]
public partial class AddPatientViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly QuickForecastViewModel _quickForecastSession;

    public AddPatientViewModel(IDbContextFactory<AppDbContext> dbContextFactory, QuickForecastViewModel quickForecastSession)
    {
        _dbContextFactory = dbContextFactory;
        _quickForecastSession = quickForecastSession;
    }

    [ObservableProperty]
    private string patientId = string.Empty;

    [ObservableProperty]
    private string fromQuickForecast = string.Empty;

    private bool IsEditing => Guid.TryParse(PatientId, out _);
    private bool IsFromQuickForecast => string.Equals(FromQuickForecast, "true", StringComparison.OrdinalIgnoreCase);

    partial void OnPatientIdChanged(string value)
    {
        OnPropertyChanged(nameof(PageTitle));
        if (IsEditing) _ = LoadCommand.ExecuteAsync(null);
    }

    partial void OnFromQuickForecastChanged(string value)
    {
        if (!IsFromQuickForecast) return;

        DateOfBirth = _quickForecastSession.DateOfBirth;
        Gender = _quickForecastSession.Gender;
    }

    public string PageTitle => IsEditing ? "Edit patient" : "Add patient";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string firstName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string lastName = string.Empty;

    // DatePicker binds to DateTime, not DateOnly — converted at save time.
    [ObservableProperty]
    private DateTime dateOfBirth = DateTime.Today.AddYears(-1);

    [ObservableProperty]
    private Gender gender = Gender.Unknown;

    public IReadOnlyList<Gender> GenderOptions { get; } = Enum.GetValues<Gender>();

    private bool CanSave => !string.IsNullOrWhiteSpace(FirstName)
                             && !string.IsNullOrWhiteSpace(LastName);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(PatientId, out var id)) return;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id);
        if (patient is null) return;

        FirstName = patient.FirstName;
        LastName = patient.LastName;
        DateOfBirth = patient.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        Gender = patient.Gender;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        if (IsEditing)
        {
            var id = Guid.Parse(PatientId);
            var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id);
            if (patient is null) return;

            patient.FirstName = FirstName.Trim();
            patient.LastName = LastName.Trim();
            patient.DateOfBirth = DateOnly.FromDateTime(DateOfBirth);
            patient.Gender = Gender;
            await db.SaveChangesAsync();

            // Pops back to the same PatientDetailPage instance already on the stack, which
            // reloads on appearing (see PatientDetailPage.xaml.cs) and shows the edited fields.
            await Shell.Current.GoToAsync("..");
            return;
        }

        var newPatient = new Patient
        {
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            DateOfBirth = DateOnly.FromDateTime(DateOfBirth),
            Gender = Gender
        };

        db.Patients.Add(newPatient);

        if (IsFromQuickForecast)
        {
            foreach (var dose in _quickForecastSession.Doses)
            {
                db.ImmunizationEvents.Add(new ImmunizationEvent
                {
                    PatientId = newPatient.Id,
                    CvxCode = dose.Vaccine.Code,
                    DateAdministered = dose.DateAdministered
                });
            }
        }

        await db.SaveChangesAsync();

        if (IsFromQuickForecast)
        {
            _quickForecastSession.Reset();

            // Pop QuickForecastPage, QuickForecastResultPage, and this page before pushing
            // patient detail, so back from there lands on the roster, not on stale quick-forecast/
            // add-patient screens for a session that's now over.
            await Shell.Current.GoToAsync($"../../../patientdetail?id={newPatient.Id}");
            return;
        }

        // ".." pops this page before pushing patient detail, so the new
        // patient replaces the (now-stale, blank) form in the back stack.
        // Back from patient detail lands on the roster, not on this form.
        await Shell.Current.GoToAsync($"../patientdetail?id={newPatient.Id}");
    }
}
