using OpenCdsi.Mobile.ViewModels;

namespace OpenCdsi.Mobile.Views;

public partial class AddDosePage : ContentPage
{
    public AddDosePage(AddDoseViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
