using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCdsi.VaxEngine.Mobile.Models;
using OpenCdsi.VaxEngine.Mobile.Services;

namespace OpenCdsi.VaxEngine.Mobile.ViewModels;

// Registered as a singleton (see README) so QuickForecastResultViewModel can
// read this session's state directly, without a database round trip in
// between — nothing here is ever persisted unless "Save as patient" is
// tapped on the result screen. Reset() is called each time the entry page
// appears so state doesn't leak between one quick-forecast session and the
// next.
public partial class QuickForecastViewModel : ObservableObject
{
    private readonly CvxLookupService _cvxLookup;

    public QuickForecastViewModel(CvxLookupService cvxLookup)
    {
        _cvxLookup = cvxLookup;
    }

    [ObservableProperty]
    private DateTime dateOfBirth = DateTime.Today.AddMonths(-2);

    [ObservableProperty]
    private Gender gender = Gender.Unknown;

    public IReadOnlyList<Gender> GenderOptions { get; } = Enum.GetValues<Gender>();

    [RelayCommand]
    private async Task RunForecastAsync()
        => await Shell.Current.GoToAsync("quickforecastresult");

    [ObservableProperty]
    private ObservableCollection<QuickDoseEntry> doses = new();

    // Inline add-dose state — kept on this same page rather than a separate
    // route, since these entries are ephemeral and don't need their own
    // navigable screen the way a real patient's history does.
    [ObservableProperty]
    private bool isAddingDose;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<CvxOption> searchResults = Array.Empty<CvxOption>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmAddDoseCommand))]
    private CvxOption? selectedOption;

    [ObservableProperty]
    private DateTime newDoseDate = DateTime.Today;

    partial void OnSearchTextChanged(string value) => SearchResults = _cvxLookup.Search(value);

    [RelayCommand]
    private void BeginAddDose() => IsAddingDose = true;

    [RelayCommand]
    private void CancelAddDose()
    {
        IsAddingDose = false;
        SearchText = string.Empty;
        SelectedOption = null;
    }

    private bool CanConfirmAddDose => SelectedOption is not null;

    [RelayCommand(CanExecute = nameof(CanConfirmAddDose))]
    private void ConfirmAddDose()
    {
        if (SelectedOption is null) return;

        Doses.Add(new QuickDoseEntry(SelectedOption, DateOnly.FromDateTime(NewDoseDate)));
        CancelAddDose(); // reuse to reset the inline search state
    }

    [RelayCommand]
    private void RemoveDose(QuickDoseEntry entry) => Doses.Remove(entry);

    public void Reset()
    {
        DateOfBirth = DateTime.Today.AddMonths(-2);
        Gender = Gender.Unknown;
        Doses.Clear();
        CancelAddDose();
    }

    // Synthesizes a Patient shape for the engine adapter without ever
    // writing one to the database — the adapter only needs the DOB, gender,
    // and dose history to forecast against.
    public Patient BuildTransientPatient() => new()
    {
        Id = Guid.Empty,
        DateOfBirth = DateOnly.FromDateTime(DateOfBirth),
        Gender = Gender,
        ImmunizationEvents = Doses
            .Select(d => new ImmunizationEvent
            {
                CvxCode = d.Vaccine.Code,
                DateAdministered = d.DateAdministered
            })
            .ToList()
    };
}
