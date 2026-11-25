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

- Testovi klijenta: 258 → 281 (devetnaest za `WakeLockService`, pet za ožičenje u ekranu treninga).
- Vraćanje pravila na committed stablu: bez ponovnog traženja kad kartica opet postane vidljiva ruši 2 testa;
  zaključavanje koje stigne posle `release()` ne otpušta se 1; odbijen zahtev baca grešku 2.
- Razvojni server servira manifest kao `application/manifest+json` i sve četiri ikone (200, `image/png`);
  izgradnja za produkciju nosi manifest i `icons/`.
- Uživo, u pregledaču u panelu: stranica je u bezbednom kontekstu, API postoji, manifest i `theme-color` su
  povezani. **Pregledač u panelu odbija Wake Lock** (`NotAllowedError`, i kad panel ima fokus), pa je
  provereno samo da odbijanje ne pravi grešku i da se oznaka ne prikazuje; pozitivan slučaj je pokriven
  testovima sa lažnim API-jem, ne uživo.

## Šta je pregled našao (ispravljeno)

- **Ekran je mogao da ostane upaljen neograničeno.** Zaključavanje se držalo dok je trening `InProgress`, a
  ništa ne završava trening samo od sebe: ko završi poslednju seriju, ne skroluje do „Završi trening" i spusti
  telefon sa otvorenim ekranom, ostavio bi ga upaljenog do pražnjenja baterije (i do sledećeg otvaranja
  zaboravljenog treninga). Sada se ekran otpušta posle 15 minuta bez dodira (`pointerdown`, `keydown`,
  `touchstart`), a prvi sledeći dodir ga ponovo traži; kartica koja postane vidljiva ga ne vraća dok je rok
  istekao. Pet testova sa lažnim satom.
- Događaji mogu da stignu obrnutim redom (`visibilitychange` pre `release`): tada niko ne bi ponovo tražio
  zaključavanje; sada ga traži i sam događaj otpuštanja kad je kartica vidljiva.
- **Ožičenje u ekranu nije bilo pokriveno**: brisanje `effect`-a ili obrnut uslov ostavljali su ceo skup zelenim
  (oblik koji se u ovom projektu ponavlja: pravilo ima test, ali ga niko ne poziva). Sada postoji
  `workout-session.spec.ts` (traži u toku, ne traži za planiran, završen i preskočen, otpušta pri završetku i
  pri izlasku, oznaka samo dok se drži).
- Zaštita od istovremenih zahteva (`requesting`) nije imala test: bez nje drugi zahtev prepisuje prvi
  `sentinel` i jedno zaključavanje ostaje neotpušteno posle `release()`.
- Slušaoci na dokumentu se skidaju kad se servis uništi: bez toga su instance iz ranijih testova reagovale
  na događaje u sledećem, pa bi poredak testova menjao rezultat.
- Skripta za ikone je mešala alfa-kanal (`paste` sa maskom) i tvrdila da ima providne ćoškove, a logo je pun
  RGB kvadrat; sada koristi `alpha_composite` i čuva RGB kad nema providnosti (ikone su 12% manje).
- Zastarele izjave: README je i dalje pisao da dolaze service worker i ekran koji se ne gasi, a
  `docs/teza-uskladjivanje.md` i `CLAUDE.md` su PWA navodili kao van obima.
- Vraćanje pravila posle pregleda: `hold()` izbačen iz efekta ruši 2; zaštita od istovremenih zahteva izbačena
  ruši 1; dodir koji ne produžava rok 1; rok koji nikad ne istekne 2.

## Šta pregled navodi a nije proveravano

- **iPhone:** WebKit je neko vreme imao grešku zbog koje Wake Lock ne radi u aplikaciji sa početnog ekrana iako
  se zahtev prihvati; oznaka „Ekran ostaje upaljen" bi tada pisala nešto što nije tačno, a servis to ne može
  da otkrije. Uputstvo to kaže, ali nije proveravano na uređaju.
- Zahtev koji se nikad ne razreši bi držao `requesting` zauvek; specifikacija nema takav tok, pa nije obrađeno.

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
