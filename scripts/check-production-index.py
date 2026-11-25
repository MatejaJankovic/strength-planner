"""Proverava izgrađen `index.html` protiv pravila koje aplikacija sama sebi postavlja: bez umetnutog koda.

Pokretanje (iz korena repozitorijuma, posle `npm --prefix strength-planner-web run build`):

    python scripts/check-production-index.py [putanja/do/index.html]

Zašto postoji. CSP (`strength-planner-web/security-headers.conf`) dozvoljava samo `script-src 'self'`, bez
`'unsafe-inline'`, pa pregledač odbija svaki umetnut rukovalac događaja i umetnutu skriptu. Angular po
podrazumevanoj optimizaciji umeće „kritični" CSS i ostatak učitava kao
`<link rel="stylesheet" media="print" onload="this.media='all'">`: onaj `onload` CSP blokira, pa glavni CSS ostaje
na `media="print"` do kraja sesije. U razvojnom serveru optimizacije nema, pa se to nije videlo; u isporučenoj slici
(nginx) tamna tema, a i svi globalni stilovi, ne bi stigli. Nađeno pregledom, ponovljeno u pregledaču sa istim
zaglavljem; ispravljeno sa `optimization.styles.inlineCritical: false` u `angular.json`.

Ova provera hvata tu klasu greške u samom izgrađenom fajlu, bez pregledača: pada ako u njemu ima rukovaoca
događaja (`onload=`, `onclick=` ...), umetnute skripte, glavnog CSS-a na `media="print"`, ili ako skripta za temu
(`theme-init.js`) ne stoji pre prvog stila.
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT = ROOT / "strength-planner-web" / "dist" / "strength-planner-web" / "browser" / "index.html"


def check(html: str) -> list[str]:
    # Komentari ne izvršavaju ništa, a mogu da pominju iste nazive (`theme-init.js`), pa se skidaju. `<noscript>`
    # se ne izvršava kad je skripta uključena, a CSP ga ne dotiče: ostaje van provere.
    body = re.sub(r"<!--.*?-->", "", html, flags=re.S)
    body = re.sub(r"<noscript>.*?</noscript>", "", body, flags=re.S)
    problems: list[str] = []

    for tag in re.finditer(r"<([a-zA-Z][\w-]*)\b([^>]*)>", body):
        for handler in re.findall(r"\s(on[a-z]+)\s*=", tag.group(2)):
            problems.append(f"<{tag.group(1)}> ima rukovaoca događaja `{handler}=`: CSP ga blokira")

    for script in re.finditer(r"<script\b([^>]*)>(.*?)</script>", body, flags=re.S):
        if "src=" not in script.group(1) and script.group(2).strip():
            problems.append("umetnuta skripta bez `src`: CSP je blokira")

    for link in re.finditer(r"<link\b[^>]*>", body):
        if 'rel="stylesheet"' in link.group(0) and 'media="print"' in link.group(0):
            problems.append("stil na `media=\"print\"`: puni se tek umetnutim `onload`, koji CSP blokira")

    theme_script = re.search(r'<script\b[^>]*\bsrc="[^"]*theme-init\.js"', body)
    theme = theme_script.start() if theme_script else -1
    first_style = body.find('rel="stylesheet"')
    if theme == -1:
        problems.append("nema `theme-init.js`: tema se ne bi primenila pre prvog iscrtavanja")
    elif first_style != -1 and theme > first_style:
        problems.append("`theme-init.js` stoji posle prvog stila: stil bi se primenio pre teme")

    return problems


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    path = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
    if not path.exists():
        print(f"Nema {path}: prvo izgradi aplikaciju (npm --prefix strength-planner-web run build).")
        return 2

    problems = check(path.read_text(encoding="utf-8"))
    print(f"{path.name}: {'u redu' if not problems else str(len(problems)) + ' problema'}")
    for problem in problems:
        print("  " + problem)

    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
