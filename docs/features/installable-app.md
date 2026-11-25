# Instalacija na početni ekran i ekran koji se ne gasi

Runda 19, grana 2 od 4. Aplikacija se na telefonu koristi u ruci između serija: ikona na početnom ekranu bez
trake pregledača, i ekran koji se ne gasi dok trening traje.

## Šta je urađeno

- `manifest.webmanifest`: ime, `display: standalone`, portret, boje (tema `#0e5c63`, pozadina `#f4f5f7`, kao
  u aplikaciji), ikone 192 i 512 i jedna „maskable". Povezan u `index.html`, uz `theme-color`, ikonu za iOS
  (`apple-touch-icon`) i oznake za samostalni prikaz.
- Ikone pravi `scripts/make-pwa-icons.py` iz `public/logo.png` (Pillow): obične, „maskable" sa logoom smanjenim na 80%
  da ostane u bezbednoj zoni kad ga Android seče u krug, i neprovidna 180 za iOS. Ikona je pregledana.
- `WakeLockService` drži ekran upaljenim dok je trening **u toku** (ne dok se pregleda ili ispravlja završen).
  Ekran treninga ga traži kad trening postane `InProgress`, otpušta ga pri završetku i pri izlasku sa ekrana, i
  piše „Ekran ostaje upaljen dok traje trening" samo dok ga zaista drži.

## Odluke

- **Nema service worker-a.** Ova grana ne radi van mreže (isključeno iz opsega), a service worker koji ništa ne
  kešira ne bi doneo ništa osim rizika da stara verzija ostane na telefonu posle nadogradnje. Sa manifestom
  pregledači nude „Dodaj na početni ekran"; da li Chrome na Androidu nudi i „Instaliraj aplikaciju" bez
  service worker-a nije proveravano na uređaju. Ako se pokaže da treba, dodaje se u zasebnoj izmeni.
- **Zaključavanje se ponovo traži** kad kartica opet postane vidljiva: pregledač ga sam otpušta kad se
  telefon zaključa ili kad se pređe u drugi program.
- **Odbijen zahtev nije greška**: štednja baterije ili pregledač koji ne podržava API ostavljaju ekran da se
  gasi kao i do sada.

## Merenja

- Testovi klijenta: 258 → 268 (deset za `WakeLockService`).
- Razvojni server servira manifest kao `application/manifest+json` i sve četiri ikone (200, `image/png`);
  izgradnja za produkciju nosi manifest i `icons/`.
- Uživo, u pregledaču u panelu: stranica je u bezbednom kontekstu, API postoji, manifest i `theme-color` su
  povezani. **Pregledač u panelu odbija Wake Lock** (`NotAllowedError`, i kad panel ima fokus), pa je
  provereno samo da odbijanje ne pravi grešku i da se oznaka ne prikazuje; pozitivan slučaj je pokriven
  testovima sa lažnim API-jem, ne uživo.

## Šta nije proveravano

- **Instalacija na pravom telefonu** (Android i iPhone), i da li se ekran zaista ne gasi. Za to treba HTTPS
  (grana 1 ove runde): `scripts/local-https.sh` i `npm run start:lan`.
- Produkcioni nginx: `manifest.webmanifest` treba da dobije tip `application/manifest+json` iz
  `mime.types` (novije verzije nginx-a ga imaju) i CSP (`default-src 'self'`) ga pokriva; nije pokretano
  (Docker nije korišćen).
- **Ikone se keširaju godinu dana** (`immutable`, `nginx.conf` kešira sve `.png`), a nemaju heš u imenu: pri
  promeni loga promeni se i ime fajla i manifest, inače telefon ostaje na staroj ikoni.
- Ekran se ne drži upaljenim u ispravci završenog treninga, namerno.
- `lan` izgradnja je razvojna (`production: false`), pa bi kasnije registrovanje service worker-a vezano za
  `isDevMode()` tu ostalo isključeno.
