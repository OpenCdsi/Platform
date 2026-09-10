#!/usr/bin/env bash
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
#
# Generates the self-signed code-signing certificate used to sign the Windows MSIX release
# builds for OpenCdsi Mobile (see build-windows in .github/workflows/build-android.yml). This is
# a one-time setup step, not something CI re-runs per build - the same certificate has to keep
# signing every release, or testers would need to re-trust a new one (and re-install) every time.
#
# The Subject (CN=OpenCdsi) must byte-for-byte match the Publisher attribute in
# src/OpenCdsi.Mobile/Platforms/Windows/Package.appxmanifest - if you regenerate this cert with a
# different Subject, update that file too.
#
# After running this:
#   1. Add its output as two GitHub repo secrets (Settings > Secrets and variables > Actions):
#        WINDOWS_MSIX_CERT_BASE64   - base64 of the .pfx (this script prints it)
#        WINDOWS_MSIX_CERT_PASSWORD - the password (this script prints it)
#   2. Commit the .cer (public half, no private key, PEM/base64-encoded so it's ordinary text) to
#      src/OpenCdsi.Mobile/assets/opencdsi-mobile-signing-cert.cer - that's what testers install
#      into their own Trusted People store once, per this project's own README.
#   3. Delete the local .pfx - the private key only needs to exist as the GitHub secret.
set -euo pipefail

OUT_DIR="${1:-.}"
mkdir -p "$OUT_DIR"
cd "$OUT_DIR"

PASSWORD="$(openssl rand -base64 24 | tr -d '=+/' | cut -c1-24)"

openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 1825 -nodes \
  -subj "/CN=OpenCdsi" \
  -addext "extendedKeyUsage=codeSigning" \
  -addext "keyUsage=digitalSignature" \
  -addext "basicConstraints=critical,CA:false"

openssl pkcs12 -export -out OpenCdsiMobile-signing.pfx -inkey key.pem -in cert.pem \
  -password "pass:$PASSWORD" -name "OpenCdsi Mobile Signing"

# PEM, not -outform DER: Windows accepts a Base-64 encoded X.509 .cer exactly like the binary
# form, and PEM is ordinary ASCII text - easier to review, diff, and move around than binary DER.
cp cert.pem opencdsi-mobile-signing-cert.cer

rm -f key.pem cert.pem

echo
echo "Done. Files written to $OUT_DIR:"
echo "  OpenCdsiMobile-signing.pfx        - private key + cert, goes ONLY into the"
echo "                                       WINDOWS_MSIX_CERT_BASE64 secret, then gets deleted"
echo "  opencdsi-mobile-signing-cert.cer  - public cert only, commit to"
echo "                                       src/OpenCdsi.Mobile/assets/opencdsi-mobile-signing-cert.cer"
echo
echo "WINDOWS_MSIX_CERT_BASE64 (paste as the secret value):"
base64 -w0 OpenCdsiMobile-signing.pfx
echo
echo
echo "WINDOWS_MSIX_CERT_PASSWORD (paste as the secret value):"
echo "$PASSWORD"
