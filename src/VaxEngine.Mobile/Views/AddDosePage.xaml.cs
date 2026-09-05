using VaxEngine.Mobile.ViewModels;

namespace VaxEngine.Mobile.Views;

public partial class AddDosePage : ContentPage
{
    public AddDosePage(AddDoseViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
