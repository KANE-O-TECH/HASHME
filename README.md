# HASHME v1.2

**Owner and publisher:** KANE-O, trading as KANE-O-TECH  
**Platform:** Windows 11 or later, x64  
**Status:** Public source-available release  
**Official website:** [hashme256.com](https://hashme256.com/)

HASHME is a slim floating SHA-256 utility for Windows. Drop one file or a group
of files onto the strip and HASHME calculates complete SHA-256 fingerprints
locally without rewriting the source files.

## What HASHME does

- Calculates SHA-256 locally with no account, registration, advertising, or
  network upload.
- Accepts drops across the complete floating strip.
- Handles multiple files on one display line and copies every complete filename
  and 64-character SHA-256 value.
- Identifies `NEW`, `EXISTING`, `CHANGED`, and `MISMATCH` states.
- Supports explicit stored-record replacement after an authorised change.
- Supports Pair Match for comparing exactly two files.
- Resolves supported Windows `.lnk` and file-based `.url` shortcuts.
- Preserves always-on-top, position lock, theme, placement, and startup settings.

## Status meanings

| Status | Exact meaning |
| --- | --- |
| `NEW` | This exact content has not previously been recorded by this HASHME installation and no recognised SHA-256 sidecar record was found. |
| `EXISTING` | The recalculated hash matches HASHME's prior record, known identical content, or a recognised sidecar/manifest record. |
| `CHANGED` | This path was recorded before, but its current bytes produce a different SHA-256. The stored record is preserved until explicit replacement is approved. |
| `MISMATCH` | The current content does not match a recognised `.sha256` or `SHA256SUMS` record. |
| `MATCH` | Two files supplied in Pair Match mode have identical SHA-256 values. |
| `DIFFERENT` | Two files supplied in Pair Match mode have different SHA-256 values. |

SHA-256 is a deterministic fingerprint calculated from file content. It is not
encryption, a secret key, or proof of authorship. HASHME never embeds a hash into
a dropped file. Its local catalogue is stored under
`%LOCALAPPDATA%\KANE-O\HASHME`.

## Everyday use

1. Open HASHME.
2. Drag one or more files onto the floating strip.
3. Wait for the result.
4. Select Copy to copy complete filenames and SHA-256 values.
5. Select Clear to reset the visible strip without erasing stored history.

If `CHANGED` appears and the change is authorised, right-click and choose
**Replace stored record with current hash…**. HASHME rechecks the file, asks for
confirmation, and updates only its local catalogue.

## Official Windows download

Use the official website for the separately packaged Windows installer:

- Website: [https://hashme256.com/](https://hashme256.com/)
- Protected download: [https://bit.ly/4yGYYg2](https://bit.ly/4yGYYg2)
- Download passcode: `sha256`
- Installer filename: `HASHME_v1.2_Setup_Windows_x64.exe`
- SHA-256: `9d90fbc68d3f02e011eca9cf84885f6d62d1763b3719eb0b2aa299f20d804156`
- Signer: `CN=KANE-O, O=KANE-O, C=AU`
- Certificate thumbprint: `5B2E3674F686ECA6E02A6C917A9A2993562F325D`

The installer carries a KANE-O Authenticode signature and a DigiCert timestamp,
but the signing root is privately issued rather than publicly trusted. Windows
SmartScreen may therefore still show a reputation warning. Verify the complete
SHA-256 and certificate thumbprint before running the installer.

GitHub release binaries are intentionally kept separate from this source
repository for the current release.

## Build from source

Prerequisites:

- Windows 11 x64
- .NET 10 SDK from the 10.0.4xx feature band
- WiX Toolset 5 or later when building the MSI

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-windows.ps1
```

The build restores and compiles the projects, runs the 15-test regression suite,
publishes the self-contained Windows executable, and creates checksummed release
artifacts under `artifacts/`. Signing keys and certificates are deliberately
excluded from the repository.

## Documentation

- [Operator's Manual (PDF)](docs/HASHME_v1.2_Operators_Manual.pdf)
- [Operator's Manual (editable DOCX)](docs/HASHME_v1.2_Operators_Manual.docx)
- [Technical Specification (PDF)](docs/HASHME_v1.2_Technical_Specification.pdf)
- [Windows device acceptance test](docs/DEVICE_ACCEPTANCE_TEST.md)
- [Build verification record](docs/HASHME_v1.2_BUILD_VERIFICATION.md)
- [Ownership and release record](docs/OWNERSHIP_AND_RELEASE_RECORD.md)

## Privacy and security

- No network access is requested or used by HASHME.
- No administrator rights are required by the normal per-user installation.
- Files are opened read-only and hashed locally.
- No file content is written to HASHME's catalogue.
- The catalogue contains SHA-256 values, file paths, and first/last-seen times.
- Vulnerabilities should be reported privately as described in
  [SECURITY.md](SECURITY.md).

## Copyright and licensing

Copyright © 2026 KANE-O, trading as KANE-O-TECH. All rights reserved except as
expressly permitted by the applicable licence.

HASHME software source is available for permitted noncommercial purposes under
the [PolyForm Noncommercial License 1.0.0](LICENSE.md). This is a
**source-available** licence, not an OSI-approved open-source licence.

The HASHME name, mascot, logos, icons, interface artwork, cover artwork,
promotional graphics, and other brand assets are separately governed by
[BRAND_ASSETS_LICENSE.md](BRAND_ASSETS_LICENSE.md). Required notices are in
[NOTICE.md](NOTICE.md). Third-party terms remain under `licenses/`.

Support, security, licensing, and permission enquiries:
**hashme256@proton.me**
