/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCdsi.Mobile.Data;
using OpenCdsi.Mobile.Services;
using OpenCdsi.Mobile.ViewModels;
using OpenCdsi.Mobile.Views;

namespace OpenCdsi.Mobile;

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

		// Kicked off here, in the background, so the ~2.6MB of CDC reference data (first-run
		// extraction from the app package, then XML parsing) never blocks the first page from
		// appearing. Deliberately NOT awaited: CreateMauiApp() runs on the platform's main thread
		// before any window exists, and blocking it here previously meant the app showed nothing
		// at all until this finished. CvxLookupService and VaxEngineForecastService both await
		// (or block on, as a last resort — see CvxLookupService) the same shared load themselves
		// wherever they actually need it, so this is correct even if a page reaches them before
		// this finishes; it just means loading happens while the roster is already on screen.
		_ = app.Services.GetRequiredService<ReferenceDataStore>().LoadAsync();

		return app;
	}
}
