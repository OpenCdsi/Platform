#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.

# Flattens every .trx file in a directory into sorted "outcome<TAB>test<TAB>message" lines.
# The failure message is kept (not just the outcome) so a refactor that changes HOW a known
# conformance failure fails still shows up as a diff against tests/baseline.
import sys, glob, xml.etree.ElementTree as ET

ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
rows = []
for f in glob.glob(sys.argv[1] + '/*.trx'):
    for u in ET.parse(f).getroot().iterfind('.//t:UnitTestResult', ns):
        msg = u.find('.//t:ErrorInfo/t:Message', ns)
        m = (msg.text or '').replace('\n', ' | ').strip() if msg is not None else ''
        rows.append(f"{u.get('outcome')}\t{u.get('testName')}\t{m}")
for row in sorted(rows):
    print(row)
