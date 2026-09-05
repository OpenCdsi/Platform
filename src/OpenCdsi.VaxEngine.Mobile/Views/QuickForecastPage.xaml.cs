using OpenCdsi.VaxEngine.Mobile.ViewModels;

namespace OpenCdsi.VaxEngine.Mobile.Views;

public partial class QuickForecastPage : ContentPage
{
    private readonly QuickForecastViewModel _viewModel;

    public QuickForecastPage(QuickForecastViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // QuickForecastViewModel is a singleton (see MauiProgram.cs) so its
        // state survives navigation to the result page and back — reset it
        // here so a fresh visit to this page doesn't inherit a prior session.
        _viewModel.Reset();
    }
}
