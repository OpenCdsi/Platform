/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

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
