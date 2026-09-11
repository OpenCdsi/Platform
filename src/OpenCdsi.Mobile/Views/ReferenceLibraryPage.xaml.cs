/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.Mobile.ViewModels;

namespace OpenCdsi.Mobile.Views;

public partial class ReferenceLibraryPage : ContentPage
{
    private readonly ReferenceLibraryViewModel _viewModel;
    private bool _loaded;

    public ReferenceLibraryPage(ReferenceLibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // The chapter catalog doesn't change while the app is running - unlike PatientsPage, no
        // need to reload every time this page is returned to.
        if (_loaded) return;
        _loaded = true;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
