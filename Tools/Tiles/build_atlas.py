"""Builds Assets/Resources/Tiles/tile-atlas.png from the SVG sources in Tiles/.

The atlas is a 9x5 grid that matches TileObject.AtlasOffset:
  row 0 (top)    flowers   f1..f8  -> columns 0..7
  row 1          winds z1..z4 (cols 0..3), dragons red/green/white z7/z6/z5 (cols 4..6)
  row 2          bamboo  s1..s9  -> columns 0..8
  row 3          dots    p1..p9  -> columns 0..8
  row 4 (bottom) characters m1..m9 -> columns 0..8

Run:  python Tools/Tiles/build_atlas.py
Requires: skia-python (pip install skia-python)
"""

import os
import sys

import skia

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TILES = os.path.join(ROOT, "Tiles")
EXTRA = os.path.join(ROOT, "Assets", "Resources", "Tiles")
OUT = os.path.join(ROOT, "Assets", "Resources", "Tiles", "tile-atlas.png")

COLUMNS = 9
ROWS = 5
CELL_W = 192
CELL_H = 256

# (column, row) -> svg path. Row 0 is the top row of the image.
LAYOUT = {}


def put(column, row, name):
    folder = EXTRA if name.startswith("f") or name == "z5" else TILES
    LAYOUT[(column, row)] = os.path.join(folder, name + ".svg")


# top row: eight flowers
for i in range(8):
    put(i, 0, "f" + str(i + 1))
# winds
for i, name in enumerate(["z1", "z2", "z3", "z4"]):
    put(i, 1, name)
# dragons: red (z7), green (z6), white (z5)
put(4, 1, "z7")
put(5, 1, "z6")
put(6, 1, "z5")
# bamboo
for i in range(9):
    put(i, 2, "s" + str(i + 1))
# dots
for i in range(9):
    put(i, 3, "p" + str(i + 1))
# characters
for i in range(9):
    put(i, 4, "m" + str(i + 1))


# The SVGs are authored at 300x400; Skia renders them at their intrinsic size and
# only clips at the container, it does not scale. So each tile is rendered at the
# source size and then drawn into its atlas cell with scaling.
SOURCE_W, SOURCE_H = 300, 400


def render_cell(path):
    data = skia.Data.MakeFromFileName(path)
    stream = skia.MemoryStream(data.data(), data.size())
    dom = skia.SVGDOM.MakeFromStream(stream)
    dom.setContainerSize(skia.Size.Make(SOURCE_W, SOURCE_H))
    surface = skia.Surface(SOURCE_W, SOURCE_H)
    canvas = surface.getCanvas()
    canvas.clear(skia.ColorTRANSPARENT)
    dom.render(canvas)
    return surface.makeImageSnapshot()


def main():
    missing = [p for p in LAYOUT.values() if not os.path.exists(p)]
    if missing:
        for p in missing:
            print("missing source:", p, file=sys.stderr)
        return 1

    width, height = CELL_W * COLUMNS, CELL_H * ROWS
    surface = skia.Surface(width, height)
    canvas = surface.getCanvas()
    canvas.clear(skia.ColorTRANSPARENT)
    paint = skia.Paint(AntiAlias=True)
    sampling = skia.SamplingOptions(skia.FilterMode.kLinear)

    for (column, row), path in sorted(LAYOUT.items()):
        cell = render_cell(path)
        destination = skia.Rect.MakeXYWH(column * CELL_W, row * CELL_H, CELL_W, CELL_H)
        canvas.drawImageRect(cell, destination, sampling, paint)

    image = surface.makeImageSnapshot()
    image.save(OUT, skia.kPNG)
    print("wrote", OUT, width, "x", height, os.path.getsize(OUT), "bytes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
