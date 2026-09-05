using VaxEngine.Mobile.ViewModels;

namespace VaxEngine.Mobile.Views;

public partial class ForecastResultPage : ContentPage
{
    public ForecastResultPage(ForecastResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
