using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCdsi.VaxEngine.Mobile.Data;
using OpenCdsi.VaxEngine.Mobile.Services;
using OpenCdsi.VaxEngine.Mobile.ViewModels;
using OpenCdsi.VaxEngine.Mobile.Views;

namespace OpenCdsi.VaxEngine.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "vaxengine.db3");
		builder.Services.AddDbContextFactory<AppDbContext>(options =>
			options.UseSqlite($"Data Source={dbPath}"));

		builder.Services.AddTransient<PatientsViewModel>();
		builder.Services.AddTransient<PatientsPage>();

		builder.Services.AddTransient<AddPatientViewModel>();
		builder.Services.AddTransient<AddPatientPage>();

		builder.Services.AddTransient<PatientDetailViewModel>();
		builder.Services.AddTransient<PatientDetailPage>();

		builder.Services.AddSingleton<ReferenceDataStore>();
		builder.Services.AddSingleton<CvxLookupService>();
		builder.Services.AddTransient<AddDoseViewModel>();
		builder.Services.AddTransient<AddDosePage>();

		builder.Services.AddSingleton<IForecastEngineAdapter, VaxEngineForecastService>();
		builder.Services.AddTransient<ForecastResultViewModel>();
		builder.Services.AddTransient<ForecastResultPage>();

		// QuickForecastViewModel is a SINGLETON, not transient — the result page
		// reads its state directly rather than re-passing DOB/gender/doses through
		// query parameters. QuickForecastPage.OnAppearing() calls Reset() on it
		// each time, so state doesn't leak between sessions.
		builder.Services.AddSingleton<QuickForecastViewModel>();
		builder.Services.AddTransient<QuickForecastPage>();
		builder.Services.AddTransient<QuickForecastResultViewModel>();
		builder.Services.AddTransient<QuickForecastResultPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
		db.Database.EnsureCreated();

		// Loaded once, synchronously, before the first page appears — CvxLookupService and
		// VaxEngineForecastService both assume ReferenceDataStore.Repository is already
		// populated by the time DI resolves them.
		app.Services.GetRequiredService<ReferenceDataStore>().LoadAsync().GetAwaiter().GetResult();

		return app;
	}
}
