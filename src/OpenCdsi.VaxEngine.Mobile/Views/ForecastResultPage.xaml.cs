using OpenCdsi.VaxEngine.Mobile.ViewModels;

namespace OpenCdsi.VaxEngine.Mobile.Views;

public partial class ForecastResultPage : ContentPage
{
    public ForecastResultPage(ForecastResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
