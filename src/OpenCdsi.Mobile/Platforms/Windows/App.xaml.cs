/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Microsoft.UI.Xaml;

namespace OpenCdsi.Mobile.WinUI;

// In its own WinUI namespace, not OpenCdsi.Mobile directly - MauiWinUIApplication's App would
// otherwise collide with the shared App : Application in ../../App.xaml.cs.
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
