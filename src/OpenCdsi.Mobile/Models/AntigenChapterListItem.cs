/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.Mobile.Models;

// One row of the standalone reference library - just enough to list and search chapters without
// loading each AntigenChapter's full content until a row is actually opened.
public sealed record AntigenChapterListItem(string AntigenKey, string DiseaseName);
