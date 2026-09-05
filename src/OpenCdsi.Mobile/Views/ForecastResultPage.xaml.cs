using OpenCdsi.Mobile.ViewModels;

namespace OpenCdsi.Mobile.Views;

public partial class ForecastResultPage : ContentPage
{
    public ForecastResultPage(ForecastResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
