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
		// query parameters. See its own comment for where Reset() runs (starting a
		// new session from the roster, and after a successful "Save as patient").
		builder.Services.AddSingleton<QuickForecastViewModel>();
		builder.Services.AddTransient<QuickForecastPage>();
		builder.Services.AddTransient<QuickForecastResultViewModel>();
		builder.Services.AddTransient<QuickForecastResultPage>();

		builder.Services.AddSingleton<ClinicalReferenceStore>();
		builder.Services.AddTransient<ChapterDetailViewModel>();
		builder.Services.AddTransient<ChapterDetailPage>();
		builder.Services.AddTransient<ReferenceLibraryViewModel>();
		builder.Services.AddTransient<ReferenceLibraryPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
		db.Database.EnsureCreated();

		// Kicked off here, in the background, so bundled reference data (first-run extraction from
		// the app package, then parsing) never blocks the first page from appearing. Deliberately
		// NOT awaited: CreateMauiApp() runs on the platform's main thread before any window exists,
		// and blocking it here previously meant the app showed nothing at all until this finished.
		// Each store's own consumers (CvxLookupService, VaxEngineForecastService,
		// ChapterDetailViewModel, ReferenceLibraryViewModel) await the same shared load themselves
		// wherever they actually need it, so this is correct even if a page reaches one before this
		// finishes; it just means loading happens while the roster is already on screen.
		//
		// CvxLookupService blocks synchronously on ReferenceDataStore.LoadAsync from the UI thread
		// (see its own comment) - that's only deadlock-safe because every await in
		// ReferenceDataProvisioner's load chain uses ConfigureAwait(false), so none of its
		// continuations need this thread free to resume. Don't add an await here (or anywhere else
		// in this chain) without ConfigureAwait(false), or that blocking call deadlocks permanently.
		_ = app.Services.GetRequiredService<ReferenceDataStore>().LoadAsync();
		_ = app.Services.GetRequiredService<ClinicalReferenceStore>().LoadAsync();

		return app;
	}
}
