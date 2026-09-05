using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using OpenCdsi.VaxEngine.Mobile.Data;
using OpenCdsi.VaxEngine.Mobile.Models;
using OpenCdsi.VaxEngine.Mobile.Services;

namespace OpenCdsi.VaxEngine.Mobile.ViewModels;

[QueryProperty(nameof(PatientId), "patientId")]
public partial class AddDoseViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly CvxLookupService _cvxLookup;

    public AddDoseViewModel(IDbContextFactory<AppDbContext> dbContextFactory, CvxLookupService cvxLookup)
    {
        _dbContextFactory = dbContextFactory;
        _cvxLookup = cvxLookup;
        // Populated up front, not left empty until the first keystroke, so the picker shows what
        // it's searching over as soon as it opens instead of looking blank/broken.
        searchResults = _cvxLookup.Search(string.Empty);
    }

    [ObservableProperty]
    private string patientId = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<CvxOption> searchResults;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private CvxOption? selectedOption;

    [ObservableProperty]
    private DateTime dateAdministered = DateTime.Today;

    partial void OnSearchTextChanged(string value) => SearchResults = _cvxLookup.Search(value);

    private bool CanSave => SelectedOption is not null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (SelectedOption is null || !Guid.TryParse(PatientId, out var patientGuid)) return;

        var dose = new ImmunizationEvent
        {
            PatientId = patientGuid,
            CvxCode = SelectedOption.Code,
            DateAdministered = DateOnly.FromDateTime(DateAdministered)
        };

        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            db.ImmunizationEvents.Add(dose);
            await db.SaveChangesAsync();
        }

        // Pop back to patient detail, which reloads and shows the new dose.
        await Shell.Current.GoToAsync("..");
    }
}
