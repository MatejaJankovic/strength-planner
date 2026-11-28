# Privatnost i fontovi

Pravni minimum pre objavljivanja: politika privatnosti sa kontaktom, napomena da aplikacija ne
zamenjuje trenera ni lekara, i fontovi koji ne dolaze sa tuđih servera.

## Fontovi

Inter, Space Grotesk i Material Icons su se učitavali sa Google Fonts, pa je pri svakom otvaranju
stranice IP adresa posetioca odlazila Google-u. Sada stižu iz npm paketa (`@fontsource/inter`,
`@fontsource/space-grotesk`, `material-icons`) i ulaze u build. Učitavaju se iste debljine kao ranije.
CSP više ne dozvoljava nijedan tuđi izvor.

Usput nađeno: ikonica za statistiku tražila se imenom `monitoring`, koje postoji samo u novijem skupu
Material Symbols. Ni Google-ov font je nije imao, pa je na ekranu pisala reč. Zamenjena je sa
`insights`.

Tekstovi licenci (OFL za Inter i Space Grotesk, Apache 2.0 za ikonice) idu u web sliku kao
`3rdpartylicenses.txt`.

Da se ne vrati: test `NoThirdPartyResourcesTests` (CSP, izvorni fajlovi),
`scripts/check-production-index.py` (izgrađena stranica i stilovi) i `scripts/check-icons.py` (za svaku
ikonicu font ima sličicu).

## Politika privatnosti

- Stranica `/privacy`, otvorena i bez naloga; link stoji na prijavi, na poslednjem koraku
  registracije i u Podešavanjima.
- Ime rukovaoca i kontakt adresa dolaze iz podešavanja servera (`PRIVACY_CONTROLLER_NAME`,
  `PRIVACY_CONTACT_EMAIL`, preko `GET /api/privacy`), pa ne stoje u javnom repozitorijumu. TLS
  varijanta se bez njih ne diže (`scripts/check-compose.py` to proverava).
- Stranica nosi i odeljak „Nije zdravstveni savet“.

## Pristanak

Odgovor o tome kako se osećaš i beleške mogu da otkriju zdravstveno stanje, a za takve podatke zakon
traži izričit pristanak. Poslednji korak registracije ima polje koje nije unapred čekirano; server bez
njega odbija registraciju, a uz nalog pamti vreme pristanka i verziju politike
(`AspNetUsers.PrivacyConsentAtUtc`, `PrivacyPolicyVersion`; migracija `AddPrivacyConsent`). Nalozi
napravljeni ranije nemaju zapis. Aplikacija ne traži nov pristanak kada se verzija promeni.

## Uzrast

Donja granica je podignuta sa 10 na **16 godina** (`ProfilePolicy.MinimumAge`). Srpski zakon dozvoljava
pristanak od 15 godina, evropsko pravilo je podrazumevano 16, pa viša granica pokriva oba bez provere
roditeljske saglasnosti. Klijent je dozvoljavao 14–90, a server 10–100; sada oba drže 16–100.

Klizač u registraciji namerno ide niže od granice: ko upiše 14 vidi poruku i ne može dalje, umesto da
se odgovor sam pretvori u 16. Izjavu o uzrastu nosi i tekst pristanka.

Postojeći nalog sa uzrastom ispod 16 (moguć samo u bazi napravljenoj pre ove izmene) ne može da sačuva
izmenu profila dok je uzrast takav. Aplikacija još nije objavljena, pa takvih naloga na serveru nema.

## Visina

Politika kaže da se visina ne mora uneti, a registracija je uvek upisivala ponuđenih 175 cm. Korak
sada ima „Ne želim da navedem“.

## Izmene u postojećim tekstovima

Ekran za brisanje naloga i uputstvo su tvrdili da rezervne kopije ne postoje. Od noćnih kopija to
nije tačno, pa sada piše da obrisani podaci ostaju u šifrovanim kopijama dok one ne isteknu.

## Šta nije urađeno

- Tekst politike nije pravni savet i nije ga proverio pravnik. Provereni su članovi 16, 23 i 64 zakona
  („Sl. glasnik RS“ 87/2018).
- Nema izvoza podataka iz aplikacije; zahtev za kopiju rukovalac ispunjava ručno.
- Slika profila se čuva sa podacima koje telefon upiše u fajl (politika to kaže); aplikacija ih ne
  uklanja.
- Rokovi za ručne kopije (30 dana) i za bazu posle vraćanja (7 dana) važe samo ako ih rukovalac
  poštuje; ništa ih ne sprovodi samo. Isto važi za stare kopije kada nove prestanu da se prave.
- Nalog koji se posle vraćanja ponovo pojavi briše se skriptom `deploy/delete-account.sql`.

## Usput ispravljeno

- Visina obrisana na ekranu za izmenu profila slala se kao nula, pa se ceo profil nije mogao sačuvati.
- Naredba za prepis vlasništva nad tabelama u uputstvu za server bila je pokvarena i nije mogla da radi.
- Šta uraditi na serveru: [deployment-security.md](../deployment-security.md), „Politika privatnosti“.
