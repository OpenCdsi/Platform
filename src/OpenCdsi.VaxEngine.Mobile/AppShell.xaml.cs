using OpenCdsi.VaxEngine.Mobile.Views;

namespace OpenCdsi.VaxEngine.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("addpatient", typeof(AddPatientPage));
		Routing.RegisterRoute("patientdetail", typeof(PatientDetailPage));
	}
}
