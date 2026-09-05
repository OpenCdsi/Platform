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
