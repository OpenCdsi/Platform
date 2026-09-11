/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.Mobile.Services;

// FileSystem.OpenAppPackageFileAsync goes through Android's AssetManager.Open under the hood,
// which is not safe for genuinely concurrent access - two independent streams opened at once can
// hang at the native/JNI level with no managed exception and nothing in logcat. Confirmed against
// a real device: once ClinicalReferenceProvisioner started extracting alongside the pre-existing
// ReferenceDataProvisioner, launch intermittently stuck before Patients ever appeared - the
// intermittency (worked once, hung three times running) matches a race between the two loads'
// asset reads rather than a deterministic bug.
//
// Both provisioners hold this gate for their entire extraction pass, so their AssetManager reads
// never actually overlap - regardless of which order MauiProgram's startup kickoff runs them in,
// or whether a page reaches one of them first via its own await.
public static class AppPackageAssetGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task RunAsync(Func<Task> action)
    {
        await Gate.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            Gate.Release();
        }
    }
}
