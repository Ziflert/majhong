"""Diff our engine's golden-hand scores against the independent MCR reference.

Runs Tools/GoldenCompare (our engine) and Tools/mcr-ref (the reference library) on the same
Tools/GoldenCompare/hands.json and reports, per hand, where the fan breakdown differs. Only the
non-flower total and the multiset of fan points are compared: fan names differ between the two
sides by design (English/Russian catalogue vs. the reference's Chinese names).

Usage:  python Tools/GoldenCompare/compare.py
"""

import json
import os
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DOTNET = r"D:/Temprary/dotnet-sdk/dotnet.exe"
NODE = "node"
HANDS = os.path.join(ROOT, "Tools", "GoldenCompare", "hands.json")


def run_engine():
    proc = subprocess.run(
        [DOTNET, "run", "--project", os.path.join(ROOT, "Tools", "GoldenCompare"), "--", HANDS],
        capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)
    return parse_lines(proc.stdout)


def run_reference():
    proc = subprocess.run(
        [NODE, "score-reference.mjs", HANDS],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        cwd=os.path.join(ROOT, "Tools", "mcr-ref"))
    if proc.returncode != 0:
        print(proc.stdout, proc.stderr, file=sys.stderr)
        raise SystemExit("reference scorer failed")
    return parse_lines(proc.stdout)


def parse_lines(text):
    out = {}
    for line in text.splitlines():
        line = line.strip()
        if not line.startswith("{"):
            continue
        row = json.loads(line)
        out[row["id"]] = row
    return out


def fan_points(row):
    return sorted(f["points"] for f in row.get("fans", []))


def main():
    engine = run_engine()
    reference = run_reference()

    ids = [h["id"] for h in json.load(open(HANDS, encoding="utf-8"))["hands"]]
    mismatches = 0
    for hid in ids:
        a, b = engine.get(hid), reference.get(hid)
        if a is None or b is None:
            print(f"{hid:26s} MISSING on {'engine' if a is None else 'reference'}")
            mismatches += 1
            continue

        if "failed" in a or not a.get("validWin", True):
            print(f"{hid:26s} ENGINE rejects: {a.get('error', a.get('failed'))}")
            mismatches += 1
            continue
        if "failed" in b:
            print(f"{hid:26s} REFERENCE rejects: {b.get('failed')} {b.get('message','')[:40]}")
            mismatches += 1
            continue

        at, bt = a["nonFlowerTotal"], b["nonFlowerTotal"]
        ap, bp = fan_points(a), fan_points(b)
        if at == bt and ap == bp:
            print(f"{hid:26s} ok      total={at}")
        else:
            mismatches += 1
            flag = "TOTAL" if at != bt else "breakdown"
            print(f"{hid:26s} {flag}  engine={at} reference={bt}")
            if ap != bp:
                print(f"{'':27s} engine points : {ap}")
                print(f"{'':27s} reference pts : {bp}")

    print()
    total = len(ids)
    print(f"{total - mismatches}/{total} hands agree")
    return 1 if mismatches else 0


if __name__ == "__main__":
    raise SystemExit(main())
