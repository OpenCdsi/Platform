# VaxEngine App

Source for **OpenCdsi Mobile**, a patient-facing immunization tracker built
on top of the [OpenCdsi.VaxEngine](https://github.com/OpenCdsi/VaxEngine)
CDSi forecasting engine, consumed here as a NuGet package from GitHub Packages
rather than a project reference. ("VaxEngine App" is this repo/solution's own
name; the app's on-device display name is "OpenCdsi Mobile".)

## Structure

```
src/OpenCdsi.VaxEngine.Mobile/   .NET MAUI app (net10.0-android)
```

All MVP screens are built and navigable: patients roster (search, edit,
delete), add/edit patient, patient detail (immunization history with
add/void dose), forecast result, and a standalone quick-forecast
entry/result pair for a first encounter before deciding to add a patient —
all backed by a local SQLite database via EF Core. Forecasting itself calls
the real `OpenCdsi.VaxEngine.Core` pipeline (`VaxEngineForecastService`,
registered as `IForecastEngineAdapter` in `MauiProgram.cs`) against the CDC
reference data bundled under `Resources/Raw/ReferenceData` — see that
folder's `manifest.txt` and `ReferenceDataProvisioner` for how the bundled
XML gets extracted to a real path on first run (the engine reads real
files, not app-package streams).

`CvxLookupService` builds its vaccine list from that same reference data
(`ScheduleSupportingData.CvxToAntigen`), not a hand-typed sample — every
code the vaccine pickers show is one the forecast engine actually
recognizes. `AddDosePage` and `QuickForecastPage`'s inline picker both
search it by name or CVX code through one shared item template
(`Resources/Styles/CvxOptionTemplate.xaml`), so the two can't drift apart.

## Building

Building for Android needs the .NET `maui-android` workload and the real
Android SDK (platform `android.jar`, build-tools) — install both normally
via Visual Studio, VS Code + the MAUI extension, or:

```
dotnet workload install maui-android
```

Restoring also needs read access to this org's GitHub Packages feed (see
`nuget.config`) even though the package is public — set:

```
GITHUB_ACTOR=<your GitHub username>
GITHUB_TOKEN=<a PAT with read:packages scope>
```

then:

```
dotnet restore
dotnet build src/OpenCdsi.VaxEngine.Mobile/OpenCdsi.VaxEngine.Mobile.csproj -f net10.0-android
```

CI builds the Android app on every push/PR touching `src/` — see
`.github/workflows/build-android.yml`.
