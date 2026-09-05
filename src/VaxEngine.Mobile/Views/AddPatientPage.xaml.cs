using VaxEngine.Mobile.ViewModels;

namespace VaxEngine.Mobile.Views;

public partial class AddPatientPage : ContentPage
{
    public AddPatientPage(AddPatientViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
