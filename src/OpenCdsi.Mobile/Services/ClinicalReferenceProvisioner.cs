/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Diagnostics;
using OpenCdsi.ClinicalReference;

namespace OpenCdsi.Mobile.Services;

// Same shape and same reason as ReferenceDataProvisioner: ClinicalReferenceRepository.Load reads a
// real filesystem directory (Directory.EnumerateFiles), but MAUI's bundled Resources/Raw assets are
// only reachable through the stream-based FileSystem.OpenAppPackageFileAsync, so the bundled Pink
// Book JSON has to be copied out to a real directory once before the library can load it.
// manifest.txt (itself a bundled raw asset) lists every file to copy, since asset packages can't be
// enumerated like a real directory at runtime. Kept as its own provisioner/marker version (rather
// than folded into ReferenceDataProvisioner) so a Pink Book content refresh doesn't force
// re-extraction of the unrelated CDC XML reference data, and vice versa.
//
// Every await here uses ConfigureAwait(false) for the same reason as ReferenceDataProvisioner -
// see its own comment. Nothing currently blocks synchronously on ClinicalReferenceStore.LoadAsync
// the way CvxLookupService does for ReferenceDataStore, but this stays consistent with it so a
// future synchronous consumer doesn't reintroduce the exact same deadlock class.
public static class ClinicalReferenceProvisioner
{
    private const string AssetRoot = "ClinicalReference";

    // Bump this if the bundled chapter content ever changes, to force re-extraction on next launch
    // instead of reusing whatever an earlier app version already copied out.
    private const string ExtractedMarkerVersion = "1";

    public static async Task<ClinicalReferenceRepository> LoadAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var destRoot = Path.Combine(FileSystem.CacheDirectory, "clinicalreference");
        await ExtractIfNeededAsync(destRoot, ct).ConfigureAwait(false);
        Trace.TraceInformation(
            $"{nameof(ClinicalReferenceProvisioner)}: extraction finished after {stopwatch.ElapsedMilliseconds} ms");

        var repository = ClinicalReferenceRepository.Load(destRoot);
        Trace.TraceInformation(
            $"{nameof(ClinicalReferenceProvisioner)}: {nameof(ClinicalReferenceRepository.Load)} finished after " +
            $"{stopwatch.ElapsedMilliseconds} ms total ({repository.ChaptersByAntigen.Count} chapters loaded)");

        return repository;
    }

    private static async Task ExtractIfNeededAsync(string destRoot, CancellationToken ct)
    {
        var markerPath = Path.Combine(destRoot, ".extracted");
        if (File.Exists(markerPath) &&
            await File.ReadAllTextAsync(markerPath, ct).ConfigureAwait(false) == ExtractedMarkerVersion)
            return;

        Directory.CreateDirectory(destRoot);

        using var manifestStream =
            await FileSystem.OpenAppPackageFileAsync($"{AssetRoot}/manifest.txt").ConfigureAwait(false);
        using var manifestReader = new StreamReader(manifestStream);
        var manifestText = await manifestReader.ReadToEndAsync(ct).ConfigureAwait(false);
        var relativePaths = manifestText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var relativePath in relativePaths)
        {
            var destPath = Path.Combine(destRoot, relativePath);

            using var source =
                await FileSystem.OpenAppPackageFileAsync($"{AssetRoot}/{relativePath}").ConfigureAwait(false);
            await using var dest = File.Create(destPath);
            await source.CopyToAsync(dest, ct).ConfigureAwait(false);
        }

        await File.WriteAllTextAsync(markerPath, ExtractedMarkerVersion, ct).ConfigureAwait(false);
    }
}
