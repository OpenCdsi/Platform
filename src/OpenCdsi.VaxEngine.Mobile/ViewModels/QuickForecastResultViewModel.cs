using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCdsi.VaxEngine.Mobile.Models;
using OpenCdsi.VaxEngine.Mobile.Services;

namespace OpenCdsi.VaxEngine.Mobile.ViewModels;

public partial class QuickForecastResultViewModel : ObservableObject
{
    private readonly QuickForecastViewModel _session;
    private readonly IForecastEngineAdapter _forecastEngine;

    public QuickForecastResultViewModel(QuickForecastViewModel session, IForecastEngineAdapter forecastEngine)
    {
        _session = session;
        _forecastEngine = forecastEngine;
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
        // Quick-forecast entry deliberately never asks for a name - hands off to AddPatientPage
        // (in its "fromQuickForecast" mode, see AddPatientViewModel) so a name can be entered on
        // a real form instead of a pair of bare prompts, with DOB/gender/doses already carried
        // over from this session.
        => await Shell.Current.GoToAsync("addpatient?fromQuickForecast=true");
}
