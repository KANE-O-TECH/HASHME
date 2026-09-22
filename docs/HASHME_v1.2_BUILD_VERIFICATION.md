# HASHME v1.2 — Build and Release Verification

Source verification date: 20 September 2026  
Public repository record updated: 22 September 2026  
Owner and publisher: KANE-O, trading as KANE-O-TECH  
Release class: Compatible production patch to the v1.0 baseline

## Source verification

- Application build: **PASS — 0 warnings, 0 errors**
- Core hashing regression suite: **PASS — 15/15**
- Self-contained Windows x64 publish: **PASS**
- Product/assembly/file version: **1.2.0 / 1.2.0.0**
- Declared strip size: **600 × 54 DIP**
- Approved HashMe mascot resource: embedded
- Installation scope: per-user, no elevation authored
- Destination: `%LOCALAPPDATA%\KANE-O\HASHME`
- UpgradeCode: `{9B89B81B-488F-4D8F-AC24-5D3E64833D44}`

## Final website installer evidence

Generated: 20 September 2026 at 22:42 AEST

| Artifact | SHA-256 |
| --- | --- |
| `HASHME_v1.2_Setup_Windows_x64.exe` | `9d90fbc68d3f02e011eca9cf84885f6d62d1763b3719eb0b2aa299f20d804156` |
| `HASHME_v1.2_Setup_Windows_x64.msi` | `7ff5f257ba1fbd0f0d8d4c84a48697abf452b002e2e0dc7f8236a947dac9781e` |
| Corrected source package | `6f56eccbb292dd051b5d832b521e259cdbf5248dd78a67b358e3c18a8dc929db` |

Both installer layers were signed by:

- Subject: `CN=KANE-O, O=KANE-O, C=AU`
- Thumbprint: `5B2E3674F686ECA6E02A6C917A9A2993562F325D`
- Timestamp signer: DigiCert SHA256 RSA4096 Timestamp Responder 2026 1
- Trust result: signed and timestamped; private signing root is not publicly
  trusted

The website installer is distributed separately from this repository. A source
build is not expected to reproduce the signed website installer byte-for-byte
because signing and final packaging are controlled release operations.

## Publication boundary

The source repository was authorised for public source-available publication on
22 September 2026. GitHub release binaries remain intentionally unpublished for
the current baseline.
