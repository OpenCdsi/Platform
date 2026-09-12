# syntax=docker/dockerfile:1

# Multi-arch build (linux/amd64 + linux/arm64, the latter for Apple Silicon and ARM servers).
#
# The build stage is pinned to $BUILDPLATFORM - it always runs natively on the builder's own
# architecture, never under QEMU emulation - and cross-compiles for the requested $TARGETARCH via
# `dotnet restore/publish -a`. This is Microsoft's own recommended pattern for .NET images
# (dotnet/dotnet-docker samples): an emulated `dotnet publish` is extremely slow, a cross-compiled
# one is not. `-a $TARGETARCH` sets only the RID's architecture and does NOT imply a self-contained
# publish, so the output stays framework-dependent and runs on the plain aspnet runtime image.
#
# The runtime stage has no --platform pin, so buildx builds it for $TARGETPLATFORM. Its only RUN
# (the curl install below) does run under emulation for the non-native arch, but it's a single
# small package and costs seconds, unlike an emulated full build.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Only the .csproj files OpenCdsi.VaxEngine.Api actually depends on (itself, OpenCdsi.VaxEngine.Contracts, OpenCdsi.VaxEngine.Core,
# OpenCdsi.ClinicalReference) are copied for the restore-caching step - restoring
# OpenCdsi.VaxEngine.Api.csproj directly (not the whole OpenCdsi.VaxEngine.sln) means this image
# build never needs to know about OpenCdsi.VaxEngine.Demo or either test project, none of which
# are part of what gets published here. Also means adding a new
# project to the solution later doesn't require touching this Dockerfile unless OpenCdsi.VaxEngine.Api
# itself gains a new dependency.
COPY src/OpenCdsi.VaxEngine.Core/OpenCdsi.VaxEngine.Core.csproj src/OpenCdsi.VaxEngine.Core/
COPY src/OpenCdsi.ClinicalReference/OpenCdsi.ClinicalReference.csproj src/OpenCdsi.ClinicalReference/
COPY src/OpenCdsi.VaxEngine.Contracts/OpenCdsi.VaxEngine.Contracts.csproj src/OpenCdsi.VaxEngine.Contracts/
COPY src/OpenCdsi.VaxEngine.Api/OpenCdsi.VaxEngine.Api.csproj src/OpenCdsi.VaxEngine.Api/
RUN dotnet restore src/OpenCdsi.VaxEngine.Api/OpenCdsi.VaxEngine.Api.csproj -a $TARGETARCH

COPY src/OpenCdsi.VaxEngine.Core/ src/OpenCdsi.VaxEngine.Core/
# OpenCdsi.ClinicalReference.csproj embeds data/pinkbook/*.json and NOTICE as Content items
# (CopyToOutputDirectory), resolved via a "../../data/pinkbook" relative path from the project
# directory - needed here at publish time, not just in the runtime stage's own copy below.
COPY data/pinkbook/ data/pinkbook/
COPY src/OpenCdsi.ClinicalReference/ src/OpenCdsi.ClinicalReference/
COPY src/OpenCdsi.VaxEngine.Contracts/ src/OpenCdsi.VaxEngine.Contracts/
COPY src/OpenCdsi.VaxEngine.Api/ src/OpenCdsi.VaxEngine.Api/
RUN dotnet publish src/OpenCdsi.VaxEngine.Api/OpenCdsi.VaxEngine.Api.csproj -c Release -a $TARGETARCH -o /app --no-restore

# Runtime stage - the smaller ASP.NET runtime image, not the full SDK. No --platform pin, so this
# is built for the target arch.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl isn't present in the base aspnet image by default - installed specifically so
# docker-compose's own HEALTHCHECK (see docker-compose.yml) has something to call.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# The repo's data/ ships inside the image so a freshly `docker run`/`docker pull`'d container is
# immediately runnable with no volume required. VOLUME /data below still makes /data a mount
# point: bind-mounting a host directory there (see README's "top priority is easy updates" note)
# hides this baked-in copy, so a CDC schedule/logic update remains a volume content swap, not an
# image rebuild, for anyone who wants that workflow.
COPY data/ /data/

ENV ASPNETCORE_URLS=http://+:8080
ENV CDSI_DATA_PATH=/data

EXPOSE 8080
VOLUME /data

ENTRYPOINT ["dotnet", "OpenCdsi.VaxEngine.Api.dll"]
