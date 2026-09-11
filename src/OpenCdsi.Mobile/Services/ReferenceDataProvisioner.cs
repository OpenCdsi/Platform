/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Diagnostics;
using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.Mobile.Services;

// ReferenceDataRepository.Load reads real filesystem paths (Directory.EnumerateFiles under the
// hood) - it has no stream/byte[] overload. MAUI's bundled Resources/Raw assets are only
// reachable through FileSystem.OpenAppPackageFileAsync (a stream API into the app package, not a
// real path on disk), so the CDC XML has to be copied out to a real directory once before the
// engine can load it. manifest.txt (itself a bundled raw asset) lists every file to copy, since
// asset packages can't be enumerated like a real directory at runtime.
//
// Every await in this file uses ConfigureAwait(false), and it's load-bearing, not style: at least
// one caller (CvxLookupService.BuildOptions) genuinely blocks on this Task via
// GetAwaiter().GetResult() from the UI thread (see its own comment for why that's the only option
// there). Without ConfigureAwait(false), any await here that runs while a SynchronizationContext
// is active on the calling thread queues its continuation back onto that same thread - if that
// thread is the one now blocked in GetResult(), the continuation can never run and the Task can
// never complete: a permanent deadlock. Confirmed as a real, 100%-reproducible bug on physical
// Android hardware (not the emulator this was evidently only ever tested on) - QuickForecastViewModel
// calls CvxLookupService.Search() in its own constructor, and PatientsViewModel's constructor
// requires QuickForecastViewModel, so this fires on every single launch via the very first screen,
// not just "the first time a vaccine-search screen is opened" as CvxLookupService's own comment
// (incorrectly) assumed.
public static class ReferenceDataProvisioner
{
    private const string AssetRoot = "ReferenceData";

    // Bump this if the bundled reference data ever changes, to force re-extraction on next launch
    // instead of reusing whatever an earlier app version already copied out.
    private const string ExtractedMarkerVersion = "1";

    public static async Task<ReferenceDataRepository> LoadAsync(CancellationToken ct = default)
    {
        // Trace, not Debug - TRACE is defined in Release builds too (Debug isn't), so these
        // timings are visible via `adb logcat` against the artifact CI actually produces, not
        // just a local Debug build. Two stages logged separately since "extraction" (copying
        // bundled assets to a real path) and "Load" (the engine's own XML parsing) are different
        // code with very different plausible causes if one of them turns out to be the slow one.
        var stopwatch = Stopwatch.StartNew();
        var destRoot = Path.Combine(FileSystem.CacheDirectory, "referencedata");
        await ExtractIfNeededAsync(destRoot, ct).ConfigureAwait(false);
        Trace.TraceInformation(
            $"{nameof(ReferenceDataProvisioner)}: extraction finished after {stopwatch.ElapsedMilliseconds} ms");

        var repository = ReferenceDataRepository.Load(
            antigensDirectory: Path.Combine(destRoot, "antigens"),
            scheduleFilePath: Path.Combine(destRoot, "schedule", "ScheduleSupportingData.xml"));
        Trace.TraceInformation(
            $"{nameof(ReferenceDataProvisioner)}: {nameof(ReferenceDataRepository.Load)} finished after " +
            $"{stopwatch.ElapsedMilliseconds} ms total ({repository.AllSeries.Count} series loaded)");

        return repository;
    }

    private static async Task ExtractIfNeededAsync(string destRoot, CancellationToken ct)
    {
        var markerPath = Path.Combine(destRoot, ".extracted");
        if (File.Exists(markerPath) &&
            await File.ReadAllTextAsync(markerPath, ct).ConfigureAwait(false) == ExtractedMarkerVersion)
            return;

        using var manifestStream =
            await FileSystem.OpenAppPackageFileAsync($"{AssetRoot}/manifest.txt").ConfigureAwait(false);
        using var manifestReader = new StreamReader(manifestStream);
        var manifestText = await manifestReader.ReadToEndAsync(ct).ConfigureAwait(false);
        var relativePaths = manifestText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var relativePath in relativePaths)
        {
            var destPath = Path.Combine(destRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            using var source =
                await FileSystem.OpenAppPackageFileAsync($"{AssetRoot}/{relativePath}").ConfigureAwait(false);
            await using var dest = File.Create(destPath);
            await source.CopyToAsync(dest, ct).ConfigureAwait(false);
        }

        await File.WriteAllTextAsync(markerPath, ExtractedMarkerVersion, ct).ConfigureAwait(false);
    }
}
