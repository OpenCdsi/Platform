using VaxEngine.Mobile.ViewModels;

namespace VaxEngine.Mobile.Views;

public partial class QuickForecastResultPage : ContentPage
{
    private readonly QuickForecastResultViewModel _viewModel;

    public QuickForecastResultPage(QuickForecastResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
