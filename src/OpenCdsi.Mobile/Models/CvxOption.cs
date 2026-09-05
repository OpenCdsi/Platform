/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.Mobile.Models;

// A selectable row in AddDosePage's vaccine search. IsUnspecified flags CVX
// codes like "DTaP, unspecified formulation" — real codes providers do use
// when the specific product isn't known, not a placeholder/error state.
public record CvxOption(string Code, string DisplayName, bool IsUnspecified = false);
