"""Proverava da svaka ikonica koju aplikacija traži postoji u fontu koji isporučuje.

Pokretanje (iz korena repozitorijuma, posle `npm ci` u strength-planner-web):

    python scripts/check-icons.py

Zašto postoji. Ikonica se piše imenom (`<mat-icon>history</mat-icon>`), a font to ime pretvara u sličicu. Kad
ime ne postoji, ništa ne puca: na ekranu ostane napisana reč. Tako je „monitoring" (ime iz novijeg skupa
Material Symbols) stajalo umesto ikonice za statistiku. Imena se čitaju iz same tabele ligatura u fontu: spisak
koji paket nosi uz font sadrži i četrdesetak imena za koja font nema sličicu.

Hvata imena napisana doslovno: u `<mat-icon>`, u `icon="..."` i u `icon: '...'`, kao i navodnicima ograđena imena
u izrazu unutar `<mat-icon>`. Ime sastavljeno na drugi način ova provera ne vidi.
"""
from __future__ import annotations

import pathlib
import re
import struct
import sys
import zlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
WEB = ROOT / "strength-planner-web"
# WOFF, a ne WOFF2: njegove tabele su sažete običnim zlib-om, koji Python ima. Oba fajla paket pravi od istog fonta.
FONT = WEB / "node_modules" / "material-icons" / "iconfont" / "material-icons.woff"


def used_names(text: str) -> set[str]:
    names: set[str] = set()

    for body in re.findall(r"<mat-icon\b[^>]*>(.*?)</mat-icon>", text, flags=re.S):
        body = body.strip()
        if re.fullmatch(r"[a-z0-9_]+", body):
            names.add(body)
        elif body.startswith("{{"):
            # Uslovni izraz: imena su rezultati grana (`? 'a' : 'b'`), a ne vrednosti sa kojima se poredi.
            names.update(re.findall(r"[?:]\s*'([a-z0-9_]+)'", body))

    names.update(re.findall(r"""(?<![\w\[])icon="([a-z0-9_]+)\"""", text))
    names.update(re.findall(r"""\bicon:\s*'([a-z0-9_]+)'""", text))
    return names


def woff_tables(data: bytes) -> dict[bytes, bytes]:
    if data[:4] != b"wOFF":
        raise ValueError("nije WOFF font")

    tables = {}
    for index in range(struct.unpack_from(">H", data, 12)[0]):
        tag, offset, packed, size, _ = struct.unpack_from(">4sIIII", data, 44 + 20 * index)
        content = data[offset:offset + packed]
        tables[tag] = zlib.decompress(content) if packed < size else content
    return tables


def characters_by_glyph(cmap: bytes) -> dict[int, str]:
    """Koji znak daje koju sličicu, iz tabela formata 4 i 12."""
    result: dict[int, str] = {}
    for index in range(struct.unpack_from(">H", cmap, 2)[0]):
        start = struct.unpack_from(">I", cmap, 8 + 8 * index)[0]
        kind = struct.unpack_from(">H", cmap, start)[0]
        pairs: list[tuple[int, int]] = []

        if kind == 4:
            count = struct.unpack_from(">H", cmap, start + 6)[0] // 2
            ends, starts = start + 14, start + 16 + 2 * count
            deltas, ranges = starts + 2 * count, starts + 4 * count
            for segment in range(count):
                last = struct.unpack_from(">H", cmap, ends + 2 * segment)[0]
                first = struct.unpack_from(">H", cmap, starts + 2 * segment)[0]
                delta = struct.unpack_from(">H", cmap, deltas + 2 * segment)[0]
                offset = struct.unpack_from(">H", cmap, ranges + 2 * segment)[0]
                for code in range(first, min(last, 0xFFFE) + 1):
                    if offset:
                        glyph = struct.unpack_from(">H", cmap, ranges + 2 * segment + offset + 2 * (code - first))[0]
                        glyph = (glyph + delta) & 0xFFFF if glyph else 0
                    else:
                        glyph = (code + delta) & 0xFFFF
                    pairs.append((code, glyph))
        elif kind == 12:
            for group in range(struct.unpack_from(">I", cmap, start + 12)[0]):
                first, last, glyph = struct.unpack_from(">III", cmap, start + 16 + 12 * group)
                pairs.extend((code, glyph + code - first) for code in range(first, last + 1))

        for code, glyph in pairs:
            # Isti znak ima veliko i malo slovo na istoj sličici u nekim fontovima; ime ikonice je malim slovima.
            if glyph and (glyph not in result or chr(code).islower()):
                result[glyph] = chr(code)
    return result


def coverage(table: bytes, start: int) -> list[int]:
    kind, count = struct.unpack_from(">HH", table, start)
    if kind == 1:
        return list(struct.unpack_from(f">{count}H", table, start + 4))

    glyphs: list[int] = []
    for index in range(count):
        first, last, _ = struct.unpack_from(">HHH", table, start + 4 + 6 * index)
        glyphs.extend(range(first, last + 1))
    return glyphs


def ligature_names(data: bytes) -> set[str]:
    """Reči koje font pretvara u sličicu: sve zamene vrste „ligatura" iz tabele GSUB."""
    tables = woff_tables(data)
    characters = characters_by_glyph(tables[b"cmap"])
    gsub = tables[b"GSUB"]
    lookups = struct.unpack_from(">H", gsub, 8)[0]
    names: set[str] = set()

    for index in range(struct.unpack_from(">H", gsub, lookups)[0]):
        lookup = lookups + struct.unpack_from(">H", gsub, lookups + 2 + 2 * index)[0]
        kind, _, subtables = struct.unpack_from(">HHH", gsub, lookup)
        for number in range(subtables):
            start = lookup + struct.unpack_from(">H", gsub, lookup + 6 + 2 * number)[0]
            actual = kind
            if kind == 7:  # produžetak: prava vrsta i adresa stoje u njemu
                actual = struct.unpack_from(">H", gsub, start + 2)[0]
                start += struct.unpack_from(">I", gsub, start + 4)[0]
            if actual != 4:
                continue

            _, covered, sets = struct.unpack_from(">HHH", gsub, start)
            for position, first in enumerate(coverage(gsub, start + covered)[:sets]):
                group = start + struct.unpack_from(">H", gsub, start + 6 + 2 * position)[0]
                for entry in range(struct.unpack_from(">H", gsub, group)[0]):
                    ligature = group + struct.unpack_from(">H", gsub, group + 2 + 2 * entry)[0]
                    length = struct.unpack_from(">H", gsub, ligature + 2)[0]
                    rest = struct.unpack_from(f">{length - 1}H", gsub, ligature + 4)
                    glyphs = (first, *rest)
                    if all(glyph in characters for glyph in glyphs):
                        names.add("".join(characters[glyph] for glyph in glyphs))
    return names


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if not FONT.exists():
        print(f"Nema {FONT}: prvo pokreni npm ci u strength-planner-web.")
        return 2

    known = ligature_names(FONT.read_bytes())
    uses: dict[str, set[str]] = {}

    for source in sorted(WEB.joinpath("src").rglob("*")):
        if source.suffix not in {".html", ".ts"} or source.name.endswith(".spec.ts"):
            continue

        for name in used_names(source.read_text(encoding="utf-8")):
            uses.setdefault(name, set()).add(source.name)

    # Prazan spisak bi značio da se promenio oblik paketa ili izvora, a ne da je sve u redu.
    if not known or not uses:
        print(f"Provera nije imala šta da uporedi (imena u fontu: {len(known)}, u aplikaciji: {len(uses)}).")
        return 2

    missing = sorted(set(uses) - known)
    print(f"Ikonice: {len(uses)} u upotrebi, {'sve postoje' if not missing else str(len(missing)) + ' ne postoji'}")
    for name in missing:
        print(f"  {name} ({', '.join(sorted(uses[name]))})")

    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
