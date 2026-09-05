/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.Mobile.Views;

namespace OpenCdsi.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("addpatient", typeof(AddPatientPage));
		Routing.RegisterRoute("patientdetail", typeof(PatientDetailPage));
		Routing.RegisterRoute("adddose", typeof(AddDosePage));
		Routing.RegisterRoute("forecastresult", typeof(ForecastResultPage));
		Routing.RegisterRoute("quickforecast", typeof(QuickForecastPage));
		Routing.RegisterRoute("quickforecastresult", typeof(QuickForecastResultPage));
	}
}
