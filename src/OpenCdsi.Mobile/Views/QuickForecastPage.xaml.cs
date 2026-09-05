/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.Mobile.ViewModels;

namespace OpenCdsi.Mobile.Views;

public partial class QuickForecastPage : ContentPage
{
    private readonly QuickForecastViewModel _viewModel;

    public QuickForecastPage(QuickForecastViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // QuickForecastViewModel is a singleton (see MauiProgram.cs) so its
        // state survives navigation to the result page and back — reset it
        // here so a fresh visit to this page doesn't inherit a prior session.
        _viewModel.Reset();
    }
}
