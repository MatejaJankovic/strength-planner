"""Meri kontrast boja teksta i pozadine u svetloj i tamnoj temi, iz samih tokena.

Pokretanje (iz korena repozitorijuma):

    python scripts/check-theme-contrast.py

Čita `strength-planner-web/src/styles.scss`: svetle vrednosti iz `:root { ... }`, a tamne iz
`@mixin dark-tokens { ... }`. Tintovi (`color-mix(in srgb, X N%, var(--color-elevated))`) se računaju
isto kao u pregledaču (mešanje u sRGB), pa se meri ono što se stvarno vidi, a ne samo čiste boje.

Prag je WCAG 2.x: 4.5 za običan tekst, 3.0 za veliki ili podebljan tekst i za elemente interfejsa.
Izlaz je tabela; kod izlaza je 1 ako neki par u TAMNOJ temi pada ispod praga. Svetla tema se ne oborava
(jedinice joj ostaju iz ranijih rundi), ali se prikazuje, da se vidi šta je bilo pre.

Uz kontrast proverava još dve stvari koje nijedan test ne vidi:
  - da `public/theme-init.js` (skripta koja postavlja temu pre prvog iscrtavanja) i `theme.service.ts` imaju
    isti ključ u pregledaču i iste boje trake pregledača;
  - da u stilovima komponenata nema boje napisane kao vrednost (heksadecimalno, `white`, `rgb(...)`): takva
    boja ostaje svetla u tamnoj temi. Izuzeci su senke i zatamnjenje (tamno na svetlom i na tamnom) i
    prsten fokusa, koji se namerno meša sa belim.
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
STYLES = ROOT / "strength-planner-web" / "src" / "styles.scss"


def read_tokens(text: str) -> tuple[dict[str, str], dict[str, str]]:
    light_block = re.search(r":root\s*\{(.*?)\n\}", text, re.S).group(1)
    dark_block = re.search(r"@mixin dark-tokens\s*\{(.*?)\n\}", text, re.S).group(1)
    pattern = re.compile(r"--([\w-]+):\s*([^;]+);")
    light = {name: value.strip() for name, value in pattern.findall(light_block)}
    dark = {name: value.strip() for name, value in pattern.findall(dark_block)}
    return light, dark


def hex_to_rgb(value: str) -> tuple[float, float, float]:
    value = value.lstrip("#")
    if len(value) == 3:
        value = "".join(ch * 2 for ch in value)
    return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))  # type: ignore[return-value]


def luminance(rgb: tuple[float, float, float]) -> float:
    def channel(c: float) -> float:
        c /= 255
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4

    r, g, b = (channel(c) for c in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a: tuple[float, float, float], b: tuple[float, float, float]) -> float:
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def mix(color: tuple[float, float, float], percent: float, base: tuple[float, float, float]):
    """`color-mix(in srgb, color percent%, base)`."""
    p = percent / 100
    return tuple(color[i] * p + base[i] * (1 - p) for i in range(3))


class Theme:
    def __init__(self, tokens: dict[str, str]):
        self.tokens = tokens

    def rgb(self, name: str):
        value = self.tokens[name]
        reference = re.fullmatch(r"var\(--([\w-]+)\)", value)
        if reference:
            return self.rgb(reference.group(1))
        return hex_to_rgb(value)

    def tint(self, name: str, percent: float, base: str = "color-elevated"):
        return mix(self.rgb(name), percent, self.rgb(base))


# (opis, tekst, pozadina, prag): tekst i pozadina su ili ime tokena ili ("tint", ime, procenat, osnova).
def pairs(theme: Theme):
    t = theme
    return [
        ("tekst na stranici", t.rgb("color-ink"), t.rgb("color-surface"), 4.5),
        ("tekst na kartici", t.rgb("color-ink"), t.rgb("color-elevated"), 4.5),
        ("prigušen tekst na stranici", t.rgb("color-muted"), t.rgb("color-surface"), 4.5),
        ("prigušen tekst na kartici", t.rgb("color-muted"), t.rgb("color-elevated"), 4.5),
        ("prigušen tekst na čipu", t.rgb("color-muted"), t.rgb("color-chip"), 4.5),
        ("tekst na čipu", t.rgb("color-ink"), t.rgb("color-chip"), 4.5),
        ("primarna kao tekst na stranici (eyebrow, veze)", t.rgb("color-primary"), t.rgb("color-surface"), 4.5),
        ("primarna kao tekst na kartici", t.rgb("color-primary"), t.rgb("color-elevated"), 4.5),
        ("tekst na primarnom dugmetu", t.rgb("color-on-primary"), t.rgb("color-primary"), 4.5),
        ("tekst na ink dugmetu", t.rgb("color-on-ink"), t.rgb("color-ink"), 4.5),
        ("tekst na dugmetu za brisanje", t.rgb("color-on-danger"), t.rgb("color-danger-strong"), 4.5),
        ("tekst na iznad dugmetu", t.rgb("color-on-above"), t.rgb("volume-above"), 4.5),
        ("iznad kao tekst na kartici (greške)", t.rgb("color-above"), t.rgb("color-elevated"), 4.5),
        ("iznad kao tekst na stranici", t.rgb("color-above"), t.rgb("color-surface"), 4.5),
        ("optimalno kao tekst na stranici", t.rgb("color-optimal"), t.rgb("color-surface"), 4.5),
        ("tekst greške na tintu 12%", t.rgb("color-danger-ink"), t.tint("volume-above", 12), 4.5),
        ("tekst iznad na tintu 16%", t.rgb("color-above-ink"), t.tint("volume-above", 16), 4.5),
        ("tekst optimalno na tintu 16%", t.rgb("color-optimal-ink"), t.tint("volume-optimal", 16), 4.5),
        ("tekst ispod na tintu 16%", t.rgb("color-below-ink"), t.tint("volume-below", 16), 4.5),
        ("tekst na akcentu 24%", t.rgb("color-accent-ink"), t.tint("color-accent", 24), 4.5),
        ("tekst na punom akcentu (dugme, značka)", t.rgb("color-on-accent"), t.rgb("color-accent"), 4.5),
        ("onemogućeno dugme", t.rgb("color-disabled-ink"), t.rgb("color-disabled-bg"), 3.0),
        ("prsten fokusa na stranici", t.rgb("focus-ring"), t.rgb("color-surface"), 3.0),
        ("ivica kartice prema stranici", t.rgb("color-border"), t.rgb("color-surface"), 1.0),
    ]


APP = ROOT / "strength-planner-web"


def check_init_script() -> list[str]:
    """Ključ i boje u `theme-init.js` moraju da budu isti kao u `theme.service.ts`."""
    service = (APP / "src" / "app" / "core" / "theme" / "theme.service.ts").read_text(encoding="utf-8")
    script = (APP / "public" / "theme-init.js").read_text(encoding="utf-8")
    problems = []

    key = re.search(r"THEME_STORAGE_KEY\s*=\s*'([^']+)'", service).group(1)
    if f"'{key}'" not in script:
        problems.append(f"theme-init.js ne čita ključ '{key}' iz servisa")

    colors = re.search(r"THEME_COLORS[^=]*=\s*\{([^}]*)\}", service, re.S).group(1)
    for name, value in re.findall(r"(light|dark):\s*'(#[0-9a-fA-F]{6})'", colors):
        if f"'{value}'" not in script:
            problems.append(f"theme-init.js nema boju trake {value} ({name}) iz servisa")

    return problems


ALLOWED_LITERALS = re.compile(
    r"rgb\(20 21 26 / \d+%\)"  # senke i zatamnjenje: tamno na svetlom, i tamno na tamnom
    r"|var\(--focus-ring\) 72%, white"  # prsten fokusa: namerno se meša sa belim
    r"|white-space"
)


def check_stray_literals() -> list[str]:
    """Boje u stilovima komponenata moraju da dolaze iz tokena, ne da budu napisane."""
    problems = []
    literal = re.compile(r"#[0-9a-fA-F]{3,8}\b|\bwhite\b|\bblack\b|rgba?\(")

    for sheet in sorted((APP / "src").rglob("*.scss")):
        if sheet.name == "styles.scss":
            continue

        for number, line in enumerate(sheet.read_text(encoding="utf-8").splitlines(), start=1):
            code = line.split("//")[0]
            if literal.search(ALLOWED_LITERALS.sub("", code)):
                problems.append(f"{sheet.relative_to(APP).as_posix()}:{number}: {line.strip()}")

    return problems


def main() -> int:
    light_tokens, dark_overrides = read_tokens(STYLES.read_text(encoding="utf-8"))
    dark_tokens = {**light_tokens, **dark_overrides}
    failed_dark = 0

    for label, theme in (("SVETLA", Theme(light_tokens)), ("TAMNA", Theme(dark_tokens))):
        print(f"\n{label} tema")
        print(f"{'par':52} {'odnos':>7} {'prag':>5}")
        for description, foreground, background, threshold in pairs(theme):
            ratio = contrast(foreground, background)
            ok = ratio >= threshold
            if not ok and label == "TAMNA":
                failed_dark += 1
            print(f"{description:52} {ratio:7.2f} {threshold:5.1f}  {'ok' if ok else 'ISPOD PRAGA'}")

    print(f"\nTamna tema: {failed_dark} parova ispod praga.")

    init_problems = check_init_script()
    literal_problems = check_stray_literals()
    print(f"theme-init.js i servis: {'usklađeni' if not init_problems else 'RAZILAZE SE'}")
    for problem in init_problems:
        print("  " + problem)
    print(f"Boje napisane u komponentama: {len(literal_problems)}")
    for problem in literal_problems:
        print("  " + problem)

    return 1 if (failed_dark or init_problems or literal_problems) else 0


if __name__ == "__main__":
    sys.exit(main())
