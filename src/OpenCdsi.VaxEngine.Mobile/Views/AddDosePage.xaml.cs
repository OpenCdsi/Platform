using OpenCdsi.VaxEngine.Mobile.ViewModels;

namespace OpenCdsi.VaxEngine.Mobile.Views;

public partial class AddDosePage : ContentPage
{
    public AddDosePage(AddDoseViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
