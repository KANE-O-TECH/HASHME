from __future__ import annotations

import os
import runpy
import shutil
import sys
from argparse import ArgumentParser
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TEMP = ROOT / "artifacts" / "render-temp"


parser = ArgumentParser(
    description="Run an external DOCX renderer with an optional LibreOffice directory."
)
parser.add_argument(
    "--renderer",
    default=os.environ.get("HASHME_DOCX_RENDERER"),
    help="Path to render_docx.py (or set HASHME_DOCX_RENDERER).",
)
parser.add_argument(
    "--libreoffice-dir",
    default=os.environ.get("HASHME_LIBREOFFICE_DIR"),
    help="Directory containing soffice (or set HASHME_LIBREOFFICE_DIR).",
)
args, renderer_args = parser.parse_known_args()

if not args.renderer:
    raise SystemExit(
        "A DOCX renderer is required. Pass --renderer or set HASHME_DOCX_RENDERER."
    )

renderer = Path(args.renderer).expanduser().resolve()
if not renderer.is_file():
    raise SystemExit(f"DOCX renderer not found: {renderer}")

if args.libreoffice_dir:
    libreoffice_dir = Path(args.libreoffice_dir).expanduser().resolve()
    if not libreoffice_dir.is_dir():
        raise SystemExit(f"LibreOffice directory not found: {libreoffice_dir}")
    os.environ["PATH"] = (
        str(libreoffice_dir) + os.pathsep + os.environ.get("PATH", "")
    )
elif not shutil.which("soffice"):
    raise SystemExit(
        "LibreOffice soffice was not found on PATH. Pass --libreoffice-dir or set "
        "HASHME_LIBREOFFICE_DIR."
    )

TEMP.mkdir(parents=True, exist_ok=True)
os.environ["TEMP"] = str(TEMP)
os.environ["TMP"] = str(TEMP)

sys.argv = [str(renderer), *renderer_args]
runpy.run_path(str(renderer), run_name="__main__")
