# HASHME v1.2 — Release Notes

Initial release date: 29 August 2026  
Final installer correction verified: 20 September 2026  
Public source-available repository authorised: 22 September 2026  
Owner and publisher: KANE-O, trading as KANE-O-TECH

HASHME v1.2 is the production-compatible patch to the v1.0 baseline. It
preserves the accepted hashing engine and local catalogue while completing the
compact floating presentation and Windows installer.

## Product changes

- Floating strip width reduced from 900 to 600 device-independent pixels while
  retaining its 54-pixel profile.
- The supplied HashMe mascot is embedded in the executable and displayed at
  36 × 36 pixels.
- The complete strip accepts file drops.
- Long visual results are truncated only on screen; Copy and tooltips retain
  complete filenames and every 64-character SHA-256 value.
- Multiple results remain on one line.
- Clear resets the visible strip without erasing stored verification history.
- Existing content is shown as `EXISTING`.
- `CHANGED` and `MISMATCH` do not overwrite stored records.
- Changed records require a separately confirmed right-click replacement.
- Pair Match reports `MATCH` or `DIFFERENT`.
- HashMe Dark, Graphite, Light, and High Contrast themes remain available.

## Final Windows installer correction

The final website installer uses a one-file IExpress wrapper around the per-user
WiX MSI. The MSI is signed before embedding; the finished EXE is then signed
separately and timestamped.

- EXE: `HASHME_v1.2_Setup_Windows_x64.exe`
- EXE SHA-256: `9d90fbc68d3f02e011eca9cf84885f6d62d1763b3719eb0b2aa299f20d804156`
- MSI SHA-256: `7ff5f257ba1fbd0f0d8d4c84a48697abf452b002e2e0dc7f8236a947dac9781e`
- Signer: `CN=KANE-O, O=KANE-O, C=AU`
- Certificate thumbprint: `5B2E3674F686ECA6E02A6C917A9A2993562F325D`
- Timestamp responder: DigiCert SHA256 RSA4096 Timestamp Responder 2026 1

The signer certificate uses a private root and is not publicly trusted by
Windows. The timestamp is publicly trusted, but it does not convert the private
signing root into a publicly trusted publisher certificate.

## Distribution

The official installer remains distributed through
[hashme256.com](https://hashme256.com/). GitHub release binaries are intentionally
kept separate for this release. The repository publishes source, tests, build
automation, licensing, and product documentation.
