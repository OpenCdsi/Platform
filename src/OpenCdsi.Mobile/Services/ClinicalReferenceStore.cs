/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.ClinicalReference;

namespace OpenCdsi.Mobile.Services;

// Loads OpenCdsi.ClinicalReference's curated Pink Book chapters once and holds them for the app's
// lifetime - same Lazy<Task<>> sharing rationale as ReferenceDataStore, so a page reached before the
// background load kicked off in MauiProgram.cs finishes just awaits the same in-flight load rather
// than triggering a second one.
public class ClinicalReferenceStore
{
    private readonly Lazy<Task<ClinicalReferenceRepository>> _load = new(() => ClinicalReferenceProvisioner.LoadAsync());

    public Task<ClinicalReferenceRepository> LoadAsync() => _load.Value;
}
