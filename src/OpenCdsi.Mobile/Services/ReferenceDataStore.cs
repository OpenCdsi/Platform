/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.Mobile.Services;

// Loads OpenCdsi.VaxEngine.Core's reference data once and holds it for the app's lifetime.
// VaxEngineForecastService and CvxLookupService both need the same in-memory catalog - loading it
// once here instead of once each avoids parsing the same ~2.6MB of CDC XML twice.
//
// LoadAsync() is safe to call from multiple places (MauiProgram.cs kicks it off in the background
// at startup; ForecastAsync/CvxLookupService also call it, in case they're reached before that
// background load finishes) - the Lazy<Task<>> means every caller shares the same in-flight or
// completed load rather than triggering a second one.
public class ReferenceDataStore
{
    private readonly Lazy<Task<ReferenceDataRepository>> _load = new(() => ReferenceDataProvisioner.LoadAsync());

    public Task<ReferenceDataRepository> LoadAsync() => _load.Value;
}
