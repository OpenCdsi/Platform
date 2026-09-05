using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.VaxEngine.Mobile.Data;
using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.ViewModels;

// Doubles as the edit-patient screen: reached with no "id" query param from the roster's
// "Add patient" button (create mode), or with one from PatientDetailPage's "Edit" toolbar item
// (edit mode, loading the existing patient into the same form instead of starting blank).
[QueryProperty(nameof(PatientId), "id")]
public partial class AddPatientViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public AddPatientViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [ObservableProperty]
    private string patientId = string.Empty;

    private bool IsEditing => Guid.TryParse(PatientId, out _);

    partial void OnPatientIdChanged(string value)
    {
        OnPropertyChanged(nameof(PageTitle));
        if (IsEditing) _ = LoadCommand.ExecuteAsync(null);
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
        await db.SaveChangesAsync();

        // ".." pops this page before pushing patient detail, so the new
        // patient replaces the (now-stale, blank) form in the back stack.
        // Back from patient detail lands on the roster, not on this form.
        await Shell.Current.GoToAsync($"../patientdetail?id={newPatient.Id}");
    }
}
