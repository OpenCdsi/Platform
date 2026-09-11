/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.Mobile.Models;

// Per-forecast-row state for the "learn about this vaccine" info glyph. Hidden for statuses with
// nothing left to look up (Complete, Immune); Disabled - shown but non-navigating, with a tooltip
// explaining why - when the antigen simply has no curated ClinicalReference chapter yet.
public enum ReferenceGlyphState
{
    Hidden,
    Enabled,
    Disabled
}
