# OpenCdsi Platform

A clinical decision support engine implementing the CDC's [CDSi Logic Specification](https://www.cdc.gov/iis/cdsi/index.html)
(v4.6) for immunization forecasting — given a patient's vaccination history, it tells you what's
due next and grades every dose already given as Valid, Not Valid, or Extraneous. Built for
real-time use in EHR integrations, with the CDC's own reference data (30 antigen files + the
schedule) treated as external, hot-swappable data rather than compiled into the application, so a
new CDC data drop is a file swap, not a code change.

This repo is a monorepo covering the engine, its HTTP API, and a mobile app built on top of both.
For the detailed, chapter-by-chapter build history — design decisions, real bugs found, spec
gotchas — see [DEVLOG.md](DEVLOG.md).

## What's in here

| Path | What it is |
|---|---|
| [`src/OpenCdsi.VaxEngine.Core`](src/OpenCdsi.VaxEngine.Core) | The engine itself — a .NET library implementing CDSi Chapters 4–9 (organize history, evaluate doses, forecast, select best series, merge vaccine groups). Published as a NuGet package. |
| [`src/OpenCdsi.VaxEngine.Api`](src/OpenCdsi.VaxEngine.Api) | An ASP.NET Core web API wrapping the engine over HTTP. Published as a Docker image. |
| [`src/OpenCdsi.VaxEngine.Contracts`](src/OpenCdsi.VaxEngine.Contracts) | Request/response DTOs shared by the API. |
| [`src/OpenCdsi.VaxEngine.Demo`](src/OpenCdsi.VaxEngine.Demo) | A console app that runs sample patients through the full pipeline and prints real output — the fastest way to see the engine work without standing up the API. |
| [`src/OpenCdsi.ClinicalReference`](src/OpenCdsi.ClinicalReference) | Curated, per-antigen clinical background (CDC Pink Book summaries), separate from the CDSi logic data. Published as a NuGet package. |
| [`src/OpenCdsi.Mobile`](src/OpenCdsi.Mobile) | A .NET MAUI app (Android + Windows) consuming the engine. |
| [`data/supportingdata`](data/supportingdata) | The CDC's own CDSi supporting data (30 antigen XML files + schedule) — not authored by this project; see its [NOTICE](data/supportingdata/NOTICE). |
| [`data/pinkbook`](data/pinkbook) | This project's own curated Pink Book summaries backing `OpenCdsi.ClinicalReference`; see its [NOTICE](data/pinkbook/NOTICE). |

Four `.slnx` solution files scope builds to what you're working on — see [Build from source](#build-from-source).

## Using the published artifacts

You don't need to clone this repo to use the engine. Three things are published:

### 1. The API, as a Docker image

```bash
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production \
  ghcr.io/opencdsi/platform/vaxengine-api:latest
```

Ships with its own copy of the CDC reference data baked in, so it runs standalone. `GET /health`
confirms it's up; `POST /api/v3/forecast` and `POST /api/v3/evaluate` are the two main endpoints
(see the Swagger UI in a non-production environment, or [`src/OpenCdsi.VaxEngine.Api`](src/OpenCdsi.VaxEngine.Api)
for the full surface). To run it against newer CDC data than what's baked into the image, mount a
host directory over `/data`:

```bash
docker run -p 8080:8080 -v "$PWD/data:/data:ro" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  ghcr.io/opencdsi/platform/vaxengine-api:latest
```

Images are multi-arch (`linux/amd64` + `linux/arm64`), tagged from `backend-v*` git tags (e.g.
`1.2.3`, `1.2`, `1`, `latest`).

### 2. The engine, as a NuGet package

`OpenCdsi.VaxEngine.Core` (tagged `engine-v*`) and `OpenCdsi.ClinicalReference` (tagged
`clinref-v*`) are published to GitHub Packages under this org:
`https://nuget.pkg.github.com/OpenCdsi/index.json`.

GitHub Packages requires authentication to pull even for public packages — you'll need a GitHub
personal access token with the `read:packages` scope:

```bash
dotnet nuget add source https://nuget.pkg.github.com/OpenCdsi/index.json \
  --name OpenCdsi --username <your-github-username> --password <your-PAT>
```

`OpenCdsi.VaxEngine.Core` needs the CDC's CDSi supporting data on disk at runtime — it's
deliberately not bundled into the package. Download it from
[cdc.gov/iis/cdsi](https://www.cdc.gov/iis/cdsi/index.html), lay it out as
`<data>/antigens/AntigenSupportingData-*.xml` and `<data>/schedule/ScheduleSupportingData.xml`,
then:

```csharp
var repository = ReferenceDataRepository.Load(
    antigensDirectory: Path.Combine(dataRoot, "antigens"),
    scheduleFilePath: Path.Combine(dataRoot, "schedule", "ScheduleSupportingData.xml"));
```

This repo's own [`data/supportingdata`](data/supportingdata) is a working example of that layout.

### 3. The mobile app

`src/OpenCdsi.Mobile` is a .NET MAUI app for Android and Windows that consumes the engine
directly via a project reference. Build releases are tagged `mobile-v*` — see
[`.github/workflows/build-mobile.yml`](.github/workflows/build-mobile.yml).

## Build from source

```bash
dotnet build Engine.slnx    # Core, Contracts, Demo + their tests
dotnet build Backend.slnx   # Core, Contracts, Api + Api.Tests
dotnet build Mobile.slnx    # Core + the MAUI app
dotnet build Platform.slnx  # everything

dotnet test Engine.slnx     # same pattern for test
```

Pick the solution scoped to what you're changing — there's no repo-wide default `.sln`, so a bare
`dotnet build` is ambiguous.

To see the pipeline run without building the API or a container:

```bash
dotnet run --project src/OpenCdsi.VaxEngine.Demo
```

To run the API locally without Docker:

```bash
dotnet run --project src/OpenCdsi.VaxEngine.Api
```

Or with Docker, building from source instead of pulling the published image:

```bash
docker compose up --build
```

## License

Licensed under the Mozilla Public License 2.0 (MPL-2.0) — see [LICENSE](LICENSE). This covers
this project's own source code (`src/`, `tests/`) and the curated Pink Book summaries in
`data/pinkbook/`. The CDC's own supporting data in `data/supportingdata/` is not authored by this
project and is excluded — see that directory's [NOTICE](data/supportingdata/NOTICE) for details.
