# Granice ulaznih podataka

Tri stvari koje su prolazile validaciju a nisu smele. Sve tri su izmerene nad pokrenutim
API-jem, ne pretpostavljene.

## Nivo iskustva izvan spiska

`"experienceLevel": 999` je vraćalo **200**. Model binder za enum prima bilo koji ceo broj,
pa je vrednost prolazila kroz validaciju, upisivala se u profil i vraćala klijentu kao nivo
koji nijedan ekran ne ume da prikaže. Algoritmi koji se granaju po nivou odgovarali bi na
njega podrazumevanom granom — dakle korisnik dobija program koji ne odgovara ničemu što je
izabrao, bez ijedne poruke o grešci.

`MacrocycleService` je istu proveru već pisao ručno za cilj i model periodizacije (i zato
su `goal: 999` i `periodizationModel: 999` ispravno vraćali 400). Umesto da se ta provera
prepisuje po DTO-u, sada postoji `[DefinedEnum]`, pa svaki nov enum u zahtevu dobija je
odmah.

Provera stoji i na registraciji i na izmeni profila — inače bi izmena profila bila
zaobilaznica oko registracije.

## Email duži od kolone koja ga čuva

Adresa od 400 znakova je vraćala **500**. Prolazila je `[EmailAddress]`, stizala do baze i
padala na koloni koju Identity pravi sa 256 znakova. Greška servera umesto poruke da je
unos neispravan.

`EmailPolicy.MaximumLength` sada prati tu kolonu, na registraciji i na prijavi.

## Lozinka bez gornje granice

Lozinka od 500.000 znakova je prihvatana i upisivana.

Ovo **nije** bilo trošenje procesora, suprotno očekivanju: izmereno, heširanje je trajalo
130 ms, jer PBKDF2 predugačak ključ prvo sažme pa tek onda ponavlja. Granica postoji zato
što lozinka koju niko ne može da otkuca nije lozinka nego greška u unosu, a bez granice bi
neograničen tekst putovao kroz validaciju, heširanje i logove. Sto dvadeset osam znakova je
iznad svega što ijedan menadžer lozinki generiše.

Granica važi i na prijavi i na promeni lozinke, da ne bi postojao ulaz koji je zaobilazi.

## Lozinka u SQL-u skripte za bazu

`db/init/01-app-role.sh` je vrednosti ubacivao pravo u tekst upita:

```sh
CREATE ROLE ${APP_DB_USER} LOGIN PASSWORD '${APP_DB_PASSWORD}';
```

Lozinka sa apostrofom prekida string. Provereno nad pravim PostgreSQL-om, sa
`APP_DB_PASSWORD=ab'c`:

```
ERROR:  unterminated quoted string at or near "';
LINE 4:  CREATE ROLE rls_probe_old LOGIN PASSWORD 'ab'c';
```

U blažem ishodu skripta pukne i baza ostane bez naloga; u gorem se ostatak lozinke protumači
kao SQL. Vrednost bira vlasnik instalacije, ne napadač, ali to je i dalje ubacivanje
nepoznatog teksta u naredbu — a upravo od toga se svuda u kodu čuvamo.

Vrednosti sada idu kao psql promenljive: `:'ime'` za literal, `:"ime"` za identifikator.
Isto što parametrizovan upit radi u kodu.

Provereno nad pravim PostgreSQL-om, sa lozinkom `ab'c;DROP ROLE postgres;--`:

| | rezultat |
|---|---|
| skripta se izvrši | da |
| nalog se prijavi tom lozinkom | da |
| nalog je superkorisnik | ne |
| `postgres` nalog i dalje postoji | da |

## Šta je provereno pa se pokazalo ispravnim

Da provere ne bi delovale kao da su bile potrebne svuda:

| pokušaj | odgovor |
|---|---|
| `goal: 999`, `periodizationModel: 999` | 400 |
| `sex` od 500 znakova | 400 |
| `blockCount=100000` | 400 |
| `startDate` u 9999. godini | 400 |
| slanje fajla (multipart) na bilo koju rutu | 415 |

Poslednji red je i odgovor na pitanje o otpremanju fajlova: aplikacija nema nijednu rutu
koja prima fajl, pa ih okvir odbija pre nego što ijedan naš kod bude pozvan.
