/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Microsoft.Extensions.DependencyInjection;

namespace OpenCdsi.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// Only takes effect on desktop platforms (Windows, Mac Catalyst) - Android/iOS always fill
		// the screen regardless. Every page here was built for a phone screen and isn't (yet)
		// adaptive to a wide window, so the default is portrait-ish rather than a typical desktop
		// aspect ratio. MinimumWidth/Height keeps it from being squished smaller than the layout can
		// handle; deliberately no maximum, so the window still resizes freely and Windows' own Snap
		// can tile it to half a screen the same shape it'd already be at on a phone.
		window.Width = 420;
		window.Height = 900;
		window.MinimumWidth = 380;
		window.MinimumHeight = 700;

		return window;
	}
}