#!/usr/bin/env python3
"""Export the compact launcher into the sample's existing Unity icon slots, retaining GUIDs.

Run: python3 UnityExample/Tools/export_launcher_icons.py
Requires Pillow and CairoSVG. The SVG is the source; no downloaded font/logo is needed.
"""
from io import BytesIO
from pathlib import Path
import ctypes.util
import os

# Optional for system Python on macOS, which does not inherit DYLD search paths.
if os.environ.get("CAIRO_LIBRARY"):
    find_library = ctypes.util.find_library
    ctypes.util.find_library = lambda name: os.environ["CAIRO_LIBRARY"] if name == "cairo" else find_library(name)

import cairosvg
from PIL import Image


def main():
    tools = Path(__file__).resolve().parent
    icons = tools.parent / "Assets" / "Icons"
    master = Image.open(BytesIO(cairosvg.svg2png(url=str(tools / "launcher.svg"),
        output_width=2048, output_height=2048))).convert("RGB")
    for path in sorted(icons.glob("*/*.png")):
        # Existing Unity importer slots are the export contract (including the 81dp fallback).
        with Image.open(path) as previous:
            size = previous.size
        master.resize(size, Image.Resampling.LANCZOS).save(path)
        print(str(path.relative_to(tools.parent)) + " " + str(size[0]))


if __name__ == "__main__":
    main()
