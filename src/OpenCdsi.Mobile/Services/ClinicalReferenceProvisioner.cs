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
// manifest.txt (itself a bundled asset) lists every file to copy, since asset packages can't be
// enumerated like a real directory at runtime. The chapters come straight from data/pinkbook and
// the manifest is generated at build time with a SHA-256 per chapter (see the csproj's
// GenerateClinicalReferenceManifest), so re-extraction is keyed on the manifest text itself: any
// chapter added, removed or edited changes it, with no version constant to remember to bump.
// Kept as its own provisioner/marker (rather than folded into ReferenceDataProvisioner) so a Pink
// Book content refresh doesn't force re-extraction of the unrelated CDC XML reference data, and
// vice versa.
//
// Every await here uses ConfigureAwait(false) for the same reason as ReferenceDataProvisioner -
// see its own comment. Nothing currently blocks synchronously on ClinicalReferenceStore.LoadAsync
// the way CvxLookupService does for ReferenceDataStore, but this stays consistent with it so a
// future synchronous consumer doesn't reintroduce the exact same deadlock class.
public static class ClinicalReferenceProvisioner
{
    private const string AssetRoot = "ClinicalReference";

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
        using var manifestStream =
            await FileSystem.OpenAppPackageFileAsync($"{AssetRoot}/manifest.txt").ConfigureAwait(false);
        using var manifestReader = new StreamReader(manifestStream);
        var manifestText = await manifestReader.ReadToEndAsync(ct).ConfigureAwait(false);

        // The marker holds the manifest this copy was extracted from - see the class comment.
        var markerPath = Path.Combine(destRoot, ".extracted");
        if (File.Exists(markerPath) &&
            await File.ReadAllTextAsync(markerPath, ct).ConfigureAwait(false) == manifestText)
            return;

        // Start clean so a chapter dropped from data/pinkbook doesn't linger from an older extraction.
        if (Directory.Exists(destRoot))
            Directory.Delete(destRoot, recursive: true);
        Directory.CreateDirectory(destRoot);

        var lines = manifestText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // sha256sum format: "<hash>  <file>" - the hash is only for change detection.
            var relativePath = line[(line.IndexOf("  ", StringComparison.Ordinal) + 2)..];
            var destPath = Path.Combine(destRoot, relativePath);

            using var source =
                await FileSystem.OpenAppPackageFileAsync($"{AssetRoot}/{relativePath}").ConfigureAwait(false);
            await using var dest = File.Create(destPath);
            await source.CopyToAsync(dest, ct).ConfigureAwait(false);
        }

        await File.WriteAllTextAsync(markerPath, manifestText, ct).ConfigureAwait(false);
    }
}
