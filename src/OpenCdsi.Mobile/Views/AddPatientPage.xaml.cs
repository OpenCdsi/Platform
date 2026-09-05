using OpenCdsi.Mobile.ViewModels;

namespace OpenCdsi.Mobile.Views;

public partial class AddPatientPage : ContentPage
{
    public AddPatientPage(AddPatientViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
