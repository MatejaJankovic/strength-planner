"""Pravi ikone za instalaciju na početni ekran iz strength-planner-web/public/logo.png.

Pokretanje (iz korena repozitorijuma; traži Pillow):

    python scripts/make-pwa-icons.py

Šta pravi, u strength-planner-web/public/icons/:
  icon-192.png, icon-512.png   obične ikone (providan ugao zaobljenog kvadrata ostaje providan)
  icon-maskable-512.png        ikona za "maskable": Android je seče u krug ili kvadrat sa zaobljenim
                               ćoškovima, pa se logo smanjuje na 80% da ostane u bezbednoj zoni,
                               a ostatak je puna boja logoa
  apple-touch-icon.png         180 x 180, bez providnosti (iOS providne delove puni crnom i sam
                               zaobljava ćoškove)

Ikone nemaju heš u imenu, a nginx ih kešira godinu dana (`immutable`, vidi nginx.conf): ako se
logo promeni, promeni i imena fajlova (i manifest), inače pregledači ostaju na staroj ikoni.
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
LOGO = ROOT / "strength-planner-web" / "public" / "logo.png"
OUT = ROOT / "strength-planner-web" / "public" / "icons"

OUT.mkdir(parents=True, exist_ok=True)

logo = Image.open(LOGO).convert("RGBA")

# Boja pozadine logoa: središte leve ivice, daleko od slova i od providnih ćoškova.
background = logo.getpixel((20, logo.height // 2))[:3] + (255,)


def resized(size: int) -> Image.Image:
    return logo.resize((size, size), Image.LANCZOS)


resized(192).save(OUT / "icon-192.png", optimize=True)
resized(512).save(OUT / "icon-512.png", optimize=True)

maskable = Image.new("RGBA", (512, 512), background)
inner = resized(int(512 * 0.8))
maskable.paste(inner, ((512 - inner.width) // 2, (512 - inner.height) // 2), inner)
maskable.save(OUT / "icon-maskable-512.png", optimize=True)

touch = Image.new("RGBA", (180, 180), background)
touch.paste(resized(180), (0, 0), resized(180))
touch.convert("RGB").save(OUT / "apple-touch-icon.png", optimize=True)

print("pozadina:", "#%02x%02x%02x" % background[:3])
for path in sorted(OUT.glob("*.png")):
    with Image.open(path) as image:
        print(f"{path.name}: {image.size[0]}x{image.size[1]}, {path.stat().st_size} B")
