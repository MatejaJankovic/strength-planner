"""Proverava da svaka ikonica koju aplikacija traži postoji u fontu koji isporučuje.

Pokretanje (iz korena repozitorijuma, posle `npm ci` u strength-planner-web):

    python scripts/check-icons.py

Zašto postoji. Ikonica se piše imenom (`<mat-icon>history</mat-icon>`), a font to ime pretvara u sličicu. Kad
ime ne postoji, ništa ne puca: na ekranu ostane napisana reč. Tako je „monitoring" (ime iz novijeg skupa
Material Symbols) stajalo umesto ikonice za statistiku. Spisak imena se čita iz paketa `material-icons`.

Hvata imena napisana doslovno: u `<mat-icon>`, u `icon="..."` i u `icon: '...'`, kao i navodnicima ograđena imena
u izrazu unutar `<mat-icon>`. Ime sastavljeno na drugi način ova provera ne vidi.
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
WEB = ROOT / "strength-planner-web"
CODEPOINTS = WEB / "node_modules" / "material-icons" / "css" / "_codepoints.scss"


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


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if not CODEPOINTS.exists():
        print(f"Nema {CODEPOINTS}: prvo pokreni npm ci u strength-planner-web.")
        return 2

    known = set(re.findall(r'"([a-z0-9_]+)":\s*[0-9a-f]{4}', CODEPOINTS.read_text(encoding="utf-8")))
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
