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

Da se ne vrati: test `NoThirdPartyResourcesTests` (CSP, `index.html`, stilovi),
`scripts/check-production-index.py` (izgrađena aplikacija) i `scripts/check-icons.py` (svaka ikonica
postoji u fontu).

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

## Izmene u postojećim tekstovima

Ekran za brisanje naloga i uputstvo su tvrdili da rezervne kopije ne postoje. Od noćnih kopija to
nije tačno, pa sada piše da obrisani podaci ostaju u šifrovanim kopijama dok one ne isteknu.

## Šta nije urađeno

- Tekst politike nije pravni savet i nije ga proverio pravnik. Sadržaj koji mora da ima proveren je
  prema članovima 16 i 23 zakona („Sl. glasnik RS“ 87/2018); članovi o iznošenju podataka iz zemlje
  nisu pročitani u izvorniku.
- Nema izvoza podataka iz aplikacije; zahtev za kopiju rukovalac ispunjava ručno.
- Šta uraditi na serveru: [deployment-security.md](../deployment-security.md), „Politika privatnosti“.
