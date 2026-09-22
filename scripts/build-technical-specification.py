from __future__ import annotations

import hashlib
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph, Table, TableStyle


PROJECT = Path(__file__).resolve().parents[1]
HANDOFF = PROJECT.parent / "HashMe_Website_Handoff"
GRAPHICS = HANDOFF / "graphics"
OUTPUT = HANDOFF / "documents" / "HASHME_v1.2_Technical_Specification.pdf"
ARTIFACTS = PROJECT / "artifacts"

PAGE_W, PAGE_H = letter
NAVY = colors.HexColor("#03111E")
PANEL = colors.HexColor("#082B44")
PANEL_ALT = colors.HexColor("#0A3551")
CYAN = colors.HexColor("#16C9FF")
CYAN_DARK = colors.HexColor("#0A7DA8")
WHITE = colors.HexColor("#F3FAFF")
MUTED = colors.HexColor("#8EBFD6")
GREEN = colors.HexColor("#1DE985")
RED = colors.HexColor("#FF5364")
ORANGE = colors.HexColor("#FF9C35")


def register_fonts() -> tuple[str, str, str]:
    regular = Path(r"C:\Windows\Fonts\segoeui.ttf")
    semibold = Path(r"C:\Windows\Fonts\seguisb.ttf")
    mono = Path(r"C:\Windows\Fonts\CascadiaMono.ttf")
    if regular.exists():
        pdfmetrics.registerFont(TTFont("SegoeUI", regular))
    if semibold.exists():
        pdfmetrics.registerFont(TTFont("SegoeUISemibold", semibold))
    if mono.exists():
        pdfmetrics.registerFont(TTFont("CascadiaMono", mono))
    return (
        "SegoeUI" if regular.exists() else "Helvetica",
        "SegoeUISemibold" if semibold.exists() else "Helvetica-Bold",
        "CascadiaMono" if mono.exists() else "Courier",
    )


BODY_FONT, BOLD_FONT, MONO_FONT = register_fonts()

BODY = ParagraphStyle(
    "Body", fontName=BODY_FONT, fontSize=9.2, leading=12, textColor=WHITE
)
SMALL = ParagraphStyle(
    "Small", fontName=BODY_FONT, fontSize=8.2, leading=10.5, textColor=WHITE
)
LABEL = ParagraphStyle(
    "Label", fontName=BOLD_FONT, fontSize=8.3, leading=10, textColor=WHITE
)
MONO = ParagraphStyle(
    "Mono", fontName=MONO_FONT, fontSize=7.35, leading=9.5, textColor=CYAN
)


def p(text: str, style: ParagraphStyle = BODY) -> Paragraph:
    return Paragraph(text, style)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def artifact_sha256(path: Path) -> str:
    return sha256(path) if path.is_file() else "not built"


def background(c: canvas.Canvas, page: int, section: str) -> None:
    c.setFillColor(NAVY)
    c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
    c.setStrokeColor(CYAN_DARK)
    c.setLineWidth(1.0)
    c.line(34, PAGE_H - 38, PAGE_W - 34, PAGE_H - 38)
    c.line(34, 36, PAGE_W - 34, 36)
    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 8)
    c.drawString(40, PAGE_H - 29, "HASHME / TECHNICAL SPECIFICATION")
    c.setFillColor(MUTED)
    c.setFont(BODY_FONT, 7.5)
    c.drawRightString(PAGE_W - 40, PAGE_H - 29, section.upper())
    c.drawString(40, 22, "© 2026 KANE-O / KANE-O-TECH / hashme256@proton.me")
    c.drawRightString(PAGE_W - 40, 22, f"PAGE {page}")


def heading(c: canvas.Canvas, title: str, subtitle: str | None = None) -> None:
    c.setFillColor(WHITE)
    c.setFont(BOLD_FONT, 22)
    c.drawString(42, PAGE_H - 82, title)
    if subtitle:
        c.setFillColor(MUTED)
        c.setFont(BODY_FONT, 9.5)
        c.drawString(43, PAGE_H - 101, subtitle)


def draw_table(
    c: canvas.Canvas,
    rows: list[list[Paragraph]],
    x: float,
    top: float,
    widths: list[float],
    header: bool = False,
    padding: int = 6,
) -> float:
    table = Table(rows, colWidths=widths, repeatRows=1 if header else 0)
    style = [
        ("BACKGROUND", (0, 0), (-1, -1), PANEL),
        ("ROWBACKGROUNDS", (0, 0), (-1, -1), [PANEL, PANEL_ALT]),
        ("GRID", (0, 0), (-1, -1), 0.65, CYAN_DARK),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("LEFTPADDING", (0, 0), (-1, -1), padding),
        ("RIGHTPADDING", (0, 0), (-1, -1), padding),
        ("TOPPADDING", (0, 0), (-1, -1), padding - 1),
        ("BOTTOMPADDING", (0, 0), (-1, -1), padding - 1),
    ]
    if header:
        style.extend(
            [
                ("BACKGROUND", (0, 0), (-1, 0), CYAN_DARK),
                ("TEXTCOLOR", (0, 0), (-1, 0), WHITE),
            ]
        )
    table.setStyle(TableStyle(style))
    _, height = table.wrapOn(c, sum(widths), PAGE_H)
    table.drawOn(c, x, top - height)
    return top - height


def draw_cover(c: canvas.Canvas) -> None:
    background(c, 1, "Private website reference")
    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 10)
    c.drawCentredString(PAGE_W / 2, 708, "KANE-O'S HASHME v1.2")
    c.setFillColor(WHITE)
    c.setFont(BOLD_FONT, 30)
    c.drawCentredString(PAGE_W / 2, 670, "TECHNICAL SPECIFICATION")
    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 13)
    c.drawCentredString(PAGE_W / 2, 642, "WINDOWS 11+ x64 / SHA-256 / LOCAL-FIRST")

    image = ImageReader(str(GRAPHICS / "HASHME_Approved_Cover.png"))
    c.drawImage(image, 55, 322, width=502, height=269, preserveAspectRatio=True, mask="auto")

    c.setStrokeColor(CYAN_DARK)
    c.setFillColor(PANEL)
    c.roundRect(78, 215, 456, 70, 10, fill=1, stroke=1)
    c.setFillColor(WHITE)
    c.setFont(BOLD_FONT, 12)
    c.drawCentredString(PAGE_W / 2, 258, "PRIVATE SOURCE-AVAILABLE CANDIDATE")
    c.setFillColor(MUTED)
    c.setFont(BODY_FONT, 9)
    c.drawCentredString(PAGE_W / 2, 238, "Technical, integration and release reference for website development")

    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 9)
    c.drawCentredString(PAGE_W / 2, 164, "OWNER / PUBLISHER: KANE-O, TRADING AS KANE-O-TECH")
    c.setFillColor(MUTED)
    c.setFont(BODY_FONT, 8.5)
    c.drawCentredString(PAGE_W / 2, 145, "SOVEREIGN AUTHORITY: SOVEREIGN / KANE-O")
    c.drawCentredString(PAGE_W / 2, 126, "SUPPORT AND SECURITY: hashme256@proton.me")
    c.drawCentredString(PAGE_W / 2, 107, "BUILD RECORD: 19 SEPTEMBER 2026")
    c.showPage()


def draw_platform(c: canvas.Canvas) -> None:
    background(c, 2, "Platform and architecture")
    heading(c, "1. Product and Platform", "Authoritative build, runtime and data-boundary specification")
    rows = [
        [p("PRODUCT", LABEL), p("HASHME - KANE-O's lightweight SHA-256 utility", SMALL)],
        [p("RELEASE", LABEL), p("v1.2.0; assembly/file version 1.2.0.0", SMALL)],
        [p("OWNER / PUBLISHER", LABEL), p("KANE-O, trading as KANE-O-TECH", SMALL)],
        [p("AUTHORITY", LABEL), p("Sovereign / KANE-O", SMALL)],
        [p("PLATFORM", LABEL), p("Windows 11 or later; x64; Per-Monitor V2 high-DPI aware", SMALL)],
        [p("APPLICATION", LABEL), p("WPF, net10.0-windows; self-contained single-file PE32+ GUI", SMALL)],
        [p("INTERFACE", LABEL), p("600 x 54 device-independent pixels; floating strip; whole-window drag/drop; notification-area support", SMALL)],
        [p("HASHING", LABEL), p("SHA-256; 64 lowercase hexadecimal characters; streaming, local and read-only", SMALL)],
        [p("INSTALL SCOPE", LABEL), p(r"Per-user, no elevation; %LOCALAPPDATA%\KANE-O\HASHME; Start-menu shortcut", SMALL)],
        [p("LOCAL DATA", LABEL), p(r"settings.json and hash-records.json under %LOCALAPPDATA%\KANE-O\HASHME", SMALL)],
        [p("STARTUP", LABEL), p("Optional HKCU Run entry: KANE-O.HASHME", SMALL)],
        [p("NETWORK", LABEL), p("No network access requested or used", SMALL)],
        [p("SOURCE FILE SAFETY", LABEL), p("Dropped files are read for hashing and are never rewritten", SMALL)],
        [p("SUPPORT / SECURITY", LABEL), p("hashme256@proton.me", SMALL)],
    ]
    bottom = draw_table(c, rows, 42, 662, [132, 396], padding=5)

    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 12)
    c.drawString(42, bottom - 30, "Processing boundary")
    c.setStrokeColor(CYAN_DARK)
    c.setFillColor(PANEL_ALT)
    c.roundRect(42, bottom - 91, 528, 43, 8, fill=1, stroke=1)
    c.setFillColor(WHITE)
    c.setFont(BOLD_FONT, 9.2)
    c.drawCentredString(
        PAGE_W / 2,
        bottom - 67,
        "INPUT FILES  ->  STREAMING SHA-256  ->  LOCAL EVIDENCE  ->  STATUS + COPY",
    )
    c.setFillColor(MUTED)
    c.setFont(BODY_FONT, 8.2)
    c.drawString(42, bottom - 116, "No cloud service, upload path, telemetry channel or remote account is required.")
    c.showPage()


def draw_behavior(c: canvas.Canvas) -> None:
    background(c, 3, "Functional behavior")
    heading(c, "2. Functional and Evidence Model", "Operational states, verification inputs and deterministic output")

    status_rows = [
        [p("STATUS", LABEL), p("SIGNAL", LABEL), p("AUTHORITATIVE MEANING", LABEL)],
        [p("READY", LABEL), p("IDLE", SMALL), p("Waiting for one or more files.", SMALL)],
        [p("NEW", LABEL), p("BLUE", SMALL), p("No matching local record or recognised SHA-256 sidecar was found.", SMALL)],
        [p("EXISTING", LABEL), p("ORANGE", SMALL), p("The recalculated hash matches a prior path, identical content or recognised record.", SMALL)],
        [p("CHANGED", LABEL), p("RED", SMALL), p("The path was recorded before, but the current bytes produce a different hash.", SMALL)],
        [p("MISMATCH", LABEL), p("RED", SMALL), p("The current hash does not match a recognised sidecar or SHA256SUMS manifest.", SMALL)],
        [p("MATCH", LABEL), p("GREEN", SMALL), p("Pair Match received two files with identical SHA-256 values.", SMALL)],
        [p("DIFFERENT", LABEL), p("RED", SMALL), p("Pair Match received two files with different SHA-256 values.", SMALL)],
        [p("ERROR", LABEL), p("RED", SMALL), p("The file could not be read or hashed; the displayed detail explains why.", SMALL)],
    ]
    bottom = draw_table(c, status_rows, 42, 660, [82, 72, 374], header=True, padding=5)

    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 12)
    c.drawString(42, bottom - 28, "Evidence inputs and controls")
    bullets = [
        "Sidecars: filename.ext.sha256 or filename.sha256; a bare 64-character hash is accepted.",
        "Manifests: SHA256SUMS, SHA256SUMS.txt or SHA256SUMS.sha256 in the file directory.",
        "Accepted lines include conventional 'hash  filename' and BSD 'SHA256 (filename) = hash' formats.",
        "Pair Match is armed for one drop of exactly two files, then disarms automatically.",
        "Changed records are fail-closed and require explicit approval before baseline replacement.",
        "Copy returns complete filenames and full hashes even when the visible field is truncated.",
    ]
    y = bottom - 51
    c.setFont(BODY_FONT, 8.7)
    for line in bullets:
        c.setFillColor(CYAN)
        c.circle(48, y + 3, 1.6, fill=1, stroke=0)
        c.setFillColor(WHITE)
        c.drawString(58, y, line)
        y -= 20

    c.setStrokeColor(CYAN_DARK)
    c.setFillColor(PANEL)
    c.roundRect(42, y - 34, 528, 46, 8, fill=1, stroke=1)
    c.setFillColor(WHITE)
    c.setFont(BOLD_FONT, 9)
    c.drawString(56, y - 7, "CLEAR IS NOT DELETE")
    c.setFont(BODY_FONT, 8.4)
    c.drawString(56, y - 23, "Clear resets the visible result and Pair Match state; it does not erase settings or the local catalogue.")
    c.showPage()


def draw_release(c: canvas.Canvas) -> None:
    background(c, 4, "Build and release evidence")
    heading(c, "3. Build, Packaging and Integrity", "Reproducible toolchain, test evidence and current private-release boundary")

    build_rows = [
        [p("BUILD TOOLCHAIN", LABEL), p("Official .NET SDK 10.0.401; WiX Toolset 5+", SMALL)],
        [p("BUILD RESULT", LABEL), p("PASS - 0 warnings, 0 errors", SMALL)],
        [p("CORE TESTS", LABEL), p("PASS - 15/15", SMALL)],
        [p("PUBLISH MODE", LABEL), p("Windows x64, self-contained, compressed single-file executable", SMALL)],
        [p("PORTABLE", LABEL), p("ZIP containing HASHME.exe, README, release notes, HASHME licence and notices, brand-asset terms, and .NET notices", SMALL)],
        [p("INSTALLER", LABEL), p("Per-user MSI; no elevation; same-version upgrades permitted", SMALL)],
        [p("AUTHENTICODE", LABEL), p("Current candidate is unsigned", SMALL)],
        [p("PUBLICATION", LABEL), p("Not authorised; company formation, signed IP assignment, contributor terms, commit privacy, signing policy and go-live approval remain open", SMALL)],
    ]
    bottom = draw_table(c, build_rows, 42, 660, [132, 396], padding=5)

    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 12)
    c.drawString(42, bottom - 30, "SHA-256 release evidence")
    evidence = [
        ("HASHME.exe", artifact_sha256(ARTIFACTS / "publish" / "HASHME.exe")),
        ("Portable Windows x64 ZIP", artifact_sha256(ARTIFACTS / "HASHME_v1.2_Portable_Windows_x64.zip")),
        ("Windows x64 MSI", artifact_sha256(ARTIFACTS / "HASHME_v1.2_Setup_Windows_x64.msi")),
        ("Original mascot PNG", artifact_sha256(PROJECT / "src" / "HashMe.App" / "Assets" / "HASHME.png")),
    ]
    rows = [[p(name, LABEL), p(value, MONO)] for name, value in evidence]
    bottom = draw_table(c, rows, 42, bottom - 43, [146, 382], padding=5)

    c.setFillColor(CYAN)
    c.setFont(BOLD_FONT, 12)
    c.drawString(42, bottom - 30, "Website integration notes")
    notes = [
        "Use the supplied approved cover and original mascot files without redrawing the character.",
        "Product name: HASHME. Owner/publisher label: KANE-O, trading as KANE-O-TECH.",
        "Public contact: hashme256@proton.me.",
        "Describe this candidate as source-available, not OSI-approved open-source software.",
        "Do not describe this private candidate as publicly released or authorised for publication.",
        "SHA-256 is a fingerprint and evidence mechanism; it is not encryption.",
    ]
    y = bottom - 51
    c.setFont(BODY_FONT, 8.8)
    for line in notes:
        c.setFillColor(CYAN)
        c.circle(48, y + 3, 1.6, fill=1, stroke=0)
        c.setFillColor(WHITE)
        c.drawString(58, y, line)
        y -= 20

    c.setFillColor(MUTED)
    c.setFont(BODY_FONT, 7.1)
    c.drawString(42, 66, "This specification records the current private candidate. It does not authorise publication.")
    c.drawString(
        42,
        54,
        "Copyright © 2026 KANE-O, trading as KANE-O-TECH. All rights reserved except as expressly permitted by the applicable licence.",
    )
    c.showPage()


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=letter)
    c.setTitle("HASHME v1.2 Technical Specification")
    c.setAuthor("KANE-O")
    c.setSubject("HASHME Windows product, integration, build and integrity specification")
    c.setCreator("KANE-O-TECH")
    c.setKeywords("HASHME, SHA-256, Windows, KANE-O-TECH, source-available")
    draw_cover(c)
    draw_platform(c)
    draw_behavior(c)
    draw_release(c)
    c.save()
    print(OUTPUT)


if __name__ == "__main__":
    main()
