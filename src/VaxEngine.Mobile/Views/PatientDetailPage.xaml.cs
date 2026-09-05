using VaxEngine.Mobile.ViewModels;

namespace VaxEngine.Mobile.Views;

public partial class PatientDetailPage : ContentPage
{
    private readonly PatientDetailViewModel _viewModel;

    public PatientDetailPage(PatientDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Reload every time the page appears, not just when the "id" query param first arrives —
        // the same page instance is reused when returning from add dose, edit patient, or a voided
        // dose, so without this those changes wouldn't show until the id changed (it doesn't).
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
