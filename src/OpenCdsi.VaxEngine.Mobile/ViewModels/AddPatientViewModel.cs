using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.VaxEngine.Mobile.Data;
using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.ViewModels;

public partial class AddPatientViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public AddPatientViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

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

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var patient = new Patient
        {
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            DateOfBirth = DateOnly.FromDateTime(DateOfBirth),
            Gender = Gender
        };

        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
        }

        // ".." pops this page before pushing patient detail, so the new
        // patient replaces the (now-stale, blank) form in the back stack.
        // Back from patient detail lands on the roster, not on this form.
        await Shell.Current.GoToAsync($"../patientdetail?id={patient.Id}");
    }
}
