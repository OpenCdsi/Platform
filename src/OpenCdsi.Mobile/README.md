# VaxEngine App

Source for **OpenCdsi Mobile**, a patient-facing immunization tracker built
on top of the [OpenCdsi.VaxEngine](https://github.com/OpenCdsi/VaxEngine)
CDSi forecasting engine, consumed here as a NuGet package from GitHub Packages
rather than a project reference. ("VaxEngine App" is this repo/solution's own
name; the app's on-device display name is "OpenCdsi Mobile".)

## Install

<img src="assets/qr-latest-apk.png" alt="QR code linking to the latest release APK" width="200" />

Scan to download the latest signed APK straight to an Android phone (or use the link
directly). This always resolves to whichever tagged release is currently newest — the
QR code itself never needs regenerating:

```
https://github.com/OpenCdsi/OpenCdsi.Mobile/releases/latest/download/com.opencdsi.mobile-Signed.apk
```

The phone will need "install unknown apps" allowed for whatever app is used to scan it,
since this is a direct sideload rather than a Play Store install.

## License

Licensed under the Mozilla Public License 2.0 (MPL-2.0) — see the [LICENSE](LICENSE) file for
the full text and copyright notice. Every `.cs` and `.xaml` file carries the standard MPL 2.0
file-level notice as its first lines (after the `<?xml ?>` declaration, for XAML):

```
/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */
```

**Any new source file added to this project must carry this same header** (as an XML comment,
for `.xaml`) as its first lines, before any `using` statements, namespace declaration, or markup.

**This license covers this project's own source code only.** `Resources/Raw/ReferenceData`
(the bundled CDC CDSi supporting-data XML) is not authored by this project and is explicitly
excluded — see that folder's own [`NOTICE`](src/OpenCdsi.Mobile/Resources/Raw/ReferenceData/NOTICE)
file for its provenance. `Resources/Fonts` (Open Sans, Apache License 2.0) is likewise
third-party and outside this project's own MPL notice.

## Structure

```
src/OpenCdsi.Mobile/   .NET MAUI app (net10.0-android)
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
dotnet build src/OpenCdsi.Mobile/OpenCdsi.Mobile.csproj -f net10.0-android
```

CI builds the Android app on every push/PR touching `src/` — see
`.github/workflows/build-android.yml`. Pushing a semver tag (`v1.2.3`) additionally
stamps that version into the build (`ApplicationDisplayVersion`/`ApplicationVersion`)
and publishes the signed APK to a GitHub Release for that tag.
