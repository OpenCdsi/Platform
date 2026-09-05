/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.Mobile.Models;

namespace OpenCdsi.Mobile.Services;

// The seam between the app and OpenCdsi.VaxEngine.Core. VaxEngineForecastService is the real
// implementation (registered as this interface in MauiProgram.cs), mapping the engine's output
// into ForecastEntry; every ViewModel/page only depends on this interface, never the engine
// package directly.
public interface IForecastEngineAdapter
{
    Task<IReadOnlyList<ForecastEntry>> ForecastAsync(Patient patient, CancellationToken ct = default);
}
