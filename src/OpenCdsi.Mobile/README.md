# VaxEngine App

A .NET MAUI Android app for VaxEngine — a patient-facing immunization
tracker built on top of the [OpenCdsi.VaxEngine](https://github.com/OpenCdsi/VaxEngine)
CDSi forecasting engine, consumed here as a NuGet package from GitHub Packages
rather than a project reference.

## Structure

```
src/OpenCdsi.VaxEngine.Mobile/   .NET MAUI app (net10.0-android)
```

Currently ships one screen — the patients roster (list, search, navigate to
a patient) — backed by a local SQLite database via EF Core. Add-patient,
patient-detail, and quick-forecast screens (the ones that will actually call
into `OpenCdsi.VaxEngine.Core`) are not built yet.

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
