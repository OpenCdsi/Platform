/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.Mobile.ViewModels;

namespace OpenCdsi.Mobile.Views;

public partial class PatientsPage : ContentPage
{
    private readonly PatientsViewModel _viewModel;

    public PatientsPage(PatientsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Reload every time the page appears, not just once — returning
        // from "add dose" or "add patient" should show the new record
        // without a manual refresh.
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
