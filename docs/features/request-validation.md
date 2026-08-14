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

## Dopune posle pregleda koda

**Uputstvo za nadogradnju je i dalje govorilo da se lozinka ukuca u SQL.** Skripta je
popravljena, ali je `deployment-security.md` operatera sa zatečenim volumenom slao da ručno
otkuca `CREATE ROLE ... PASSWORD '...'` — dakle baš onu naredbu koja se lomi na apostrofu.
A to je i put koji se najčešće koristi, jer se init skripte pokreću samo pri prvom pravljenju
baze. Sada uputstvo pušta samu skriptu, koja je i idempotentna (provereno: drugo pokretanje
preskače pravljenje naloga).

**Frontend nije znao za gornju granicu lozinke.** Postavljena je na serveru i na DTO-u, a
`Validators` u pregledaču su imali samo donju — pa bi korisnik koji nalepi dugu lozinku iz
menadžera prošao svaku proveru pa dobio goli 400 bez označenog polja. To je tačno ono
razmimoilaženje zbog kog `PasswordPolicy` i postoji. Provereno u pregledaču nad pravim
formularom: 9 znakova → greška o minimumu, 10 i 128 → ispravno, 129 → greška o maksimumu.

**Granicu email adrese sada drži test.** Vrednost 256 je odgovarala koloni, ali je nije
ništa vezivalo za nju. Test je čita iz samog EF modela — isto što već rade provere za broj
mišićnih grupa i za `Equipment`, koja i postoji zato što je ta ista vrsta razmimoilaženja
jednom pretvorila 400 u 500.

**Skripta proverava svoje ulazne promenljive.** Sa praznim `APP_DB_PASSWORD` je ranije
izlazila sa nulom i pravila nalog bez lozinke (provereno: `rolpassword` ostaje NULL), pa se
aplikacija posle nije mogla prijaviti — a greška se videla tek kao neuspela veza pri startu
API-ja. Sada odbija da se izvrši i kaže šta nedostaje.

**`[DefinedEnum]` sada pada glasno na pogrešnoj upotrebi.** Nad tipom koji nije enum i nad
`[Flags]` enum-om (gde `Enum.IsDefined` odbija ispravne kombinacije zastavica) baca izuzetak
umesto da tiho vrati „neispravno" — greška je u tome gde je atribut stavljen, a ne u
korisnikovom unosu. Suvišno raspakivanje `Nullable<T>` je uklonjeno: CLR pakuje nullable
vrednost kao `null` ili kao samu vrednost, pa ta grana nikada nije mogla da se izvrši.
