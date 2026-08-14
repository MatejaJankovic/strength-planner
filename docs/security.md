# Bezbednost aplikacije

Šta je urađeno, kako je provereno i šta svesno ostaje otvoreno.

Za korake pri isporuci (TLS, sertifikati, nadogradnja postojeće baze) vidi
[`deployment-security.md`](deployment-security.md).

> **Napomena o ovom dokumentu.** Repozitorijum je javan. Zato ovde stoji šta je *zatvoreno*
> i kako se to proverava, a ne uputstvo za iskorišćavanje onoga što nije. Detalji koji bi
> nekome skratili posao namerno izostaju; sve što ostaje otvoreno navedeno je na nivou koji
> vlasniku govori šta da uradi, a napadaču ne daje ništa što ne bi ionako probao.

---

## Model pretnje, ukratko

Jedan korisnik po nalogu; svi podaci su lični trenažni zapisi. Ozbiljne su tri stvari:

1. **Da jedan korisnik vidi tuđe podatke.**
2. **Da neko preuzme tuđ nalog** — pogađanjem lozinke ili krađom tokena.
3. **Da korisnik izgubi svoje podatke.**

Sve ostalo je manje važno od toga.

---

## Pregled po tačkama

Kratak odgovor na svaku od dvadeset stavki liste po kojoj je rađena poslednja provera.
Detalji su niže, u odeljcima na koje redovi upućuju.

| | Stavka | Stanje | Gde |
|---|---|---|---|
| 1 | Sakriti ključeve | zatvoreno | [Tajne](#tajne-i-kljucevi) |
| 2 | Očistiti tajne iz istorije | zatvoreno | [Tajne](#tajne-i-kljucevi) |
| 3 | Javni ključ ka bazi | ne postoji takav sloj | [Pristup bazi](#pristup-bazi) |
| 4 | Ograničenje po redu (RLS) | zatvoreno, na nivou aplikacije | [Kontrola pristupa](#kontrola-pristupa) |
| 5 | Šifrovanje osetljivih podataka | delimično, svesno | [Osetljivi podaci](#osetljivi-podaci) |
| 6 | Autorizacija na serveru | zatvoreno | [Kontrola pristupa](#kontrola-pristupa) |
| 7 | Zaključan pristup zapisu | zatvoreno | [Kontrola pristupa](#kontrola-pristupa) |
| 8 | Sprečeno menjanje tuđih polja | zatvoreno | [Ulazni podaci](#ulazni-podaci-i-injekcija) |
| 9 | Bezbedni kolačići sesije | nema kolačića | [Nalozi i tokeni](#nalozi-i-tokeni) |
| 10 | Heširanje lozinki | zatvoreno | [Nalozi i tokeni](#nalozi-i-tokeni) |
| 11 | Ograničenje pokušaja prijave | zatvoreno | [Zloupotreba](#zloupotreba-i-automati) |
| 12 | Zaštita od automata | delimično, svesno | [Zloupotreba](#zloupotreba-i-automati) |
| 13 | Parametrizovani upiti | zatvoreno | [Ulazni podaci](#ulazni-podaci-i-injekcija) |
| 14 | Validacija ulaza | zatvoreno | [Ulazni podaci](#ulazni-podaci-i-injekcija) |
| 15 | Escapovanje korisničkog sadržaja | zatvoreno | [Prikaz sadržaja](#prikaz-sadrzaja) |
| 16 | Ograničeno otpremanje fajlova | nema otpremanja | [Prikaz sadržaja](#prikaz-sadrzaja) |
| 17 | Odgovori bez viška | zatvoreno | [Odgovori](#sta-odgovori-nose) |
| 18 | Bezbednosna zaglavlja | zatvoreno | [Zaglavlja i prenos](#zaglavlja-i-prenos) |
| 19 | Obavezan HTTPS | zatvoreno u isporuci | [Zaglavlja i prenos](#zaglavlja-i-prenos) |
| 20 | Skeniranje zavisnosti | zatvoreno, i automatizovano | [Zavisnosti](#zavisnosti) |

Tri reda nisu „urađeno" i to je namerno — pisati da jesu bilo bi netačno:

- **3 (javni ključ ka bazi)** opisuje postavku u kojoj se pregledač povezuje pravo na bazu
  javnim ključem, pa ceo teret pada na pravila u bazi. Ovde toga nema: klijent ne zna za
  bazu i ne drži nikakav pristup njoj, sav saobraćaj ide kroz API. Stavka je neprimenjiva,
  a ono što ona štiti pokriveno je time što pristupa bazi ima samo server.
- **5 (šifrovanje)** je urađeno tamo gde nosi razliku, a nije tamo gde bi bilo ukras — vidi
  [Osetljivi podaci](#osetljivi-podaci).
- **12 (automati)** ima zamku i usko ograničenje registracija, ali nema CAPTCHA-u, i to je
  odluka sa razlogom — vidi [Zloupotreba](#zloupotreba-i-automati).

---

## Kontrola pristupa

*(stavke 4, 6, 7)*

Svaki upit u servisima ograničen je na `userId` iz `sub` claim-a tokena. Nijedan kontroler
ne prima identitet korisnika iz tela zahteva, rute ni query stringa. Od 30 endpoint-a samo
`register` i `login` su anonimni; globalna `FallbackPolicy` traži autentifikaciju svuda gde
atribut nedostaje.

**Provereno nad pokrenutom aplikacijom**, a ne čitanjem koda: napravljena su dva naloga i
korisnik B je pokušao da dohvati ili izmeni svaki resurs korisnika A — mezocikluse,
dugoročni plan, treninge, serije, planove vežbi, custom vežbu i analitiku.

| Zahtev korisnika B | Odgovor |
|---|---|
| `GET` / `DELETE /mesocycles/{tuđi}` | 404 |
| `GET /macrocycles/{tuđi}` | 404 |
| `GET /sessions/{tuđi}`, `POST .../start`, `.../complete` | 404 |
| `POST /exercise-plans/{tuđi}/sets` | 404 |
| `PUT` / `DELETE /sets/{tuđi}` | 404 |
| `PUT /exercises/{tuđa custom}/weight-step` | 404 |
| `GET /analytics/volume`, `/analytics/tonnage` sa tuđim `mesocycleId` | 404 |

**Nijedan nije prošao.**

### Sloj ispod toga

Do nedavno je ta kontrola živela **isključivo** u servisima: šezdesetak upita, svaki sa
svojim `Where(x => x.UserId == userId)`. To je radilo, ali je garancija bila „niko nije
zaboravio". Takav propust se ne vidi — upit bez uslova radi savršeno dok u bazi postoji
jedan nalog.

Sada `AppDbContext` svakoj tabeli sa korisničkim podacima dodaje filter vlasništva na nivou
modela. Tabele bez svoje `UserId` kolone (nedelje, treninzi, planovi vežbi, serije) dobijaju
isto pravilo preko navigacije do vlasnika.

Da filter zaista nosi odbranu, izmereno je tako što je uslov po `userId` uklonjen iz jednog
servisa, pa je filter ostao jedino što stoji između korisnika B i tuđeg mezociklusa:

| | B traži mezociklus korisnika A |
|---|---|
| filter uključen | 404 |
| filter isključen | **200, ceo plan u telu odgovora** |

Test čuva i da filter postoji **i da zaista čita vlasnika** — provera „filter postoji" ne
vredi ništa, jer je `HasQueryFilter(x => true)` filter kao i svaki drugi, a to je upravo
izraz kojim je gore izmereno curenje.

Detalji i ono što ovim nije rešeno: [`features/row-level-security.md`](features/row-level-security.md).

### Zašto nije Postgres RLS

Ograničenje je na nivou aplikacije, ne baze. Postgres RLS štiti i od kompromitovanog naloga
ka bazi — ali klijent ovde nikada ne drži pristup bazi, pa je realan scenario upit koji je
zaboravio uslov, i njega ovo pokriva. Pravi Postgres RLS nad tabelama koje nemaju svoju
kolonu vlasnika tražio bi podupite kroz četiri tabele po redu, ili denormalizovanu `UserId`
kolonu svuda; drugo je izmena šeme i zaseban posao.

---

## Nalozi i tokeni

*(stavke 9, 10)*

### Lozinke

Dužina je između **10 i 128 znakova**, na jednom mestu (`PasswordPolicy`) koje poštuju i
Identity i validacija zahteva i ekran za registraciju. Ranije je bilo šest bez ijedne
dodatne provere, što je prihvatalo `123456` — izmereno, ne pretpostavljeno.

Uslovi složenosti su namerno isključeni: dužina je jedina mera koja stvarno otežava
pogađanje, dok pravila o velikim slovima i znakovima uglavnom teraju ljude na predvidive
obrasce.

Gornja granica **nije** zaštita od trošenja procesora, iako tako izgleda: izmereno, lozinka
od 500.000 znakova heširala se za 130 ms, jer PBKDF2 predugačak ključ prvo sažme pa tek onda
ponavlja. Postoji zato što lozinka koju niko ne može da otkuca nije lozinka nego greška u
unosu.

**Heširanje radi Identity — PBKDF2-HMAC-SHA512, so po lozinci. Sopstvenog heširanja nema i
ne treba ga biti.** Jedini deo koji ima smisla podešavati je broj iteracija, jer on određuje
koliko košta pogađanje ako baza ikada iscuri: podignut je sa .NET-ovih podrazumevanih 100.000
na 210.000, koliko OWASP preporučuje za SHA-512. Izmena je unazad kompatibilna, jer je broj
iteracija upisan u sam heš.

### Prijava

- Ista poruka za pogrešnu lozinku, nepostojeći nalog i zaključan nalog.
- **Isto i vreme odgovora.** Nepostojeći nalog se ranije vraćao pre heširanja, pa je razlika
  sama po sebi odavala koji email ima nalog. Sada se i na promašaju troši isti posao:
  razlika je pala sa desetina milisekundi na ~7 ms.
- Zaključavanje posle 5 promašaja na 5 minuta.

### Tokeni, i zašto nema kolačića

JWT, HS256, važenje 60 minuta. Token nosi `sub`, `email`, `jti` i **security stamp** iz
Identity-ja, koji se proverava pri svakom zahtevu. Zato promena lozinke **odmah poništava
sve ranije izdate tokene** — provereno: stari token pređe sa 200 na 401 preko promene.

**Aplikacija ne izdaje nijedan kolačić.** Identity je uključen preko `AddIdentityCore`, koji
namerno ne registruje kolačić-šeme; sesija ne postoji kao pojam, jer je token bez stanja.
Zato pitanje „da li su kolačići sesije bezbedno podešeni" ovde nema predmet: nema šta da se
podesi, a i ne može da procuri ono čega nema.

Token stoji u `localStorage`. To je uobičajen kompromis za SPA: kolačić sa `httpOnly` bi
tražio i zaštitu od CSRF-a. Ono što ovde nosi odbranu jeste CSP, jer bi bez njega jedna XSS
rupa odmah postala krađa tokena — a XSS-a nema, vidi [Prikaz sadržaja](#prikaz-sadrzaja).

Provereno i da se token ne da falsifikovati:

| Pokušaj | Odgovor |
|---|---|
| ispravan token | 200 |
| bez tokena | 401 |
| izmenjen potpis | 401 |
| izmenjen `sub`, zadržan stari potpis | 401 |
| `alg=none` | 401 |

### Promena lozinke

Traži trenutnu lozinku, prolazi kroz isto zaključavanje kao prijava (inače bi bila
neograničen orakl za pogađanje onome ko se domogne tuđeg tokena), i traži potvrdu nove.

Potvrda nije formalnost: **u sistemu nema oporavka lozinke**, pa bi greška u kucanju značila
trajan gubitak naloga i svih podataka u njemu. Ekran to i piše.

---

## Ulazni podaci i injekcija

*(stavke 8, 13, 14)*

### Injekcija

Nema nijednog `FromSqlRaw`, `ExecuteSqlRaw` ni `NpgsqlCommand` u celom repozitorijumu. Svih
sedam `migrationBuilder.Sql(...)` blokova su statični literali bez interpolacije. Svi
`ExecuteUpdateAsync` pozivi koriste tipizirane `SetProperty` lambde. Connection string dolazi
iz konfiguracije i ne dodiruje korisnički unos.

Jedno mesto **jeste** sastavljalo SQL od promenljivih, i to izvan C# koda: skripta koja pri
prvom pokretanju pravi nalog za bazu. Lozinka je išla pravo u tekst naredbe, pa je apostrof
u njoj prekidao string — provereno nad pravim PostgreSQL-om:

```
ERROR:  unterminated quoted string at or near "';
LINE 4: CREATE ROLE ... LOGIN PASSWORD 'ab'c';
```

Vrednost bira vlasnik instalacije, ne napadač, ali to je i dalje ubacivanje nepoznatog
teksta u naredbu. Sada ide kao psql promenljiva, što je isto što parametrizovan upit radi u
kodu. Provereno lozinkom koja sadrži i apostrof i SQL fragment: nalog se napravi, prijavi se
tom lozinkom, nije superkorisnik, a `postgres` nalog ostaje netaknut.

### Menjanje polja koja korisnik ne bi smeo da postavlja

Nijedan zahtev-DTO ne nosi `UserId`, `Id`, `IsCustom`, `CreatedByUserId`, `IsActive`,
`Status` ni `Source` — sve se postavlja na serveru.

Enumi su bili rupa u tome. Model binder prima **bilo koji ceo broj** za enum, pa je
`"experienceLevel": 999` vraćalo **200**, upisivalo se u profil i vraćalo klijentu kao nivo
koji nijedan ekran ne prikazuje, dok bi algoritmi na njega odgovarali podrazumevanom granom.
Sada postoji `[DefinedEnum]`, primenjen i na registraciju i na izmenu profila.

### Granice

Granice prate bazu, jer je razmimoilaženje između njih već pravilo štetu:

| Polje | Bilo | Sada |
|---|---|---|
| `Equipment` | 64 u DTO-u, 32 u koloni → 500 | 32, uz test |
| `Email` | bez granice → 500 na 400 znakova | 256, uz test koji je čita iz EF modela |
| lozinka | bez gornje granice | 128 |
| broj mišićnih grupa | bez granice | koliko ih sistem poznaje, uz test |

Provere koje su se pokazale ispravnim i pre svega ovoga — navedene da popravke ne izgledaju
kao da je svuda nedostajalo: `goal` i `periodizationModel` van opsega (400), `sex` od 500
znakova (400), `blockCount=100000` (400), `startDate` u 9999. godini (400).

---

## Prikaz sadržaja

*(stavke 15, 16)*

Angular podrazumevano enkodira sve što uđe u šablon. U celom `src`-u nema nijednog
`innerHTML`, `bypassSecurityTrust*`, `DomSanitizer`, `eval` ni `document.write` — provereno
pretragom, ne pretpostavljeno. API ne renderuje HTML: vraća isključivo JSON, i to sa
`Content-Security-Policy: default-src 'none'`.

**Otpremanja fajlova nema.** Nijedna ruta ne prima fajl, pa ih okvir odbija pre nego što
ijedan naš kod bude pozvan — provereno slanjem multipart tela, odgovor je 415. To je i pravi
odgovor na „ograniči otpremanje fajlova": najuže moguće ograničenje je da ta površina ne
postoji.

---

## Šta odgovori nose

*(stavka 17)*

Odgovori se sastavljaju iz DTO-a, ne iz entiteta, pa se u telo ne može zalutati poljem koje
je dodato u bazu. `CurrentUserDto` nosi email i profil, nikada heš lozinke, security stamp
ni bilo šta iz Identity tabela.

Poruke o greškama su ujednačene tamo gde bi razlika bila podatak: prijava i registracija
vraćaju istu poruku bez obzira na razlog. Van razvoja se detalji izuzetaka ne šalju —
`ProblemDetails.Detail` nosi isti tekst kao `Title`, a ne poruku izuzetka.

Odgovori nose i `Cache-Control: no-store`, jer sadrže lične trenažne podatke i nemaju šta da
traže u deljenom kešu.

---

## Zaglavlja i prenos

*(stavke 18, 19)*

### Zaglavlja

nginx šalje CSP, `nosniff`, `DENY` okvir, referrer i permissions politiku na sve što
posluži; verzija se ne objavljuje. Zaglavlja se uključuju u **svaki** `location`, jer ih
nginx ne nasleđuje u blok koji ima svoj `add_header`.

**Isto sada šalje i sam API.** nginx pokriva samo saobraćaj koji kroz njega prođe, a API se
pokreće i bez njega — u razvoju, u testovima, i u svakoj isporuci gde neko objavi port ili
promeni proxy. Do sada je tada odgovor išao potpuno go, uz `Server: Kestrel` kao besplatan
podatak o tome šta se traži. Pravilo na API-ju je uže nego na frontendu (`default-src
'none'`), i to je tačno a ne strogo: API vraća isključivo JSON.

Skup zaglavlja drži test — lista je statična i briše se jednim potezom, a odgovor bez nje
izgleda identično.

### HTTPS

**TLS je sada deo isporuke, a ne uputstvo.** Bio je jedina stavka koju kod nije mogao da
reši, a rečenica „stavi neki reverse proxy" u praksi znači da to niko ne uradi. Sada:

```bash
APP_DOMAIN=tvoj-domen.rs TLS_EMAIL=ti@primer.com \
  docker compose -f docker-compose.yml -f docker-compose.tls.yml up -d
```

Caddy sam pribavlja i obnavlja Let's Encrypt sertifikat, preusmerava HTTP na HTTPS i šalje
HSTS. Uz TLS se pali i preusmeravanje na samom API-ju.

Prva verzija toga **nije radila ništa**, i to se videlo tek merenjem: zahtev preko čistog
HTTP-a vraćao je 401 bez preusmeravanja i bez HSTS-a. Tri odvojena uzroka — preusmeravanje
nije znalo port, nginx je prepisivao šemu koju je Caddy postavio, i adresa klijenta bi se
izgubila iza dva proxy-ja, čime bi svi korisnici delili jednu particiju ograničenja broja
zahteva. Sve tri su ispravljene; detalji u
[`features/transport-and-abuse.md`](features/transport-and-abuse.md).

HSTS namerno šalje Caddy, jer je on jedino mesto koje pouzdano zna da TLS postoji. U nginx
konfiguraciji ga nema: preko čistog HTTP-a ga pregledači ignorišu, a aplikaciju bi zaključao
ako se ikada posluži bez TLS-a.

---

## Zloupotreba i automati

*(stavke 11, 12)*

Ograničenje broja zahteva na auth rutama je **20 u minutu po adresi**, sa telom odgovora i
`Retry-After` — bez tela frontend ne ume da razlikuje 429 od greške u podacima, pa bi
korisniku koji je prebrzo kliktao prikazao „proveri podatke".

**Registracija ima svoju, mnogo užu granicu: 5 na sat.** Zajednička je namerno labava da
prijava ostane upotrebljiva kada nekoliko ljudi deli izlaznu adresu; za registraciju je to
previše, jer se nalog pravi jednom. Registracija je i jedino mesto gde neko ko nije
prijavljen može da pravi zapise. Provereno da su budžeti odvojeni: posle iscrpljene
registracije prijava i dalje vraća 200.

Uz to postoji **zamka (honeypot)**: skriveno polje koje čovek ne vidi, ne može da tabuje do
njega, ne može mu doći ni čitačem ekrana, i koje pregledač ne popunjava. Automat koji redom
puni sva polja ga popuni, i takva registracija se odbija istom porukom kao svaki drugi
neuspeh.

**Ovo nije CAPTCHA i ne treba ga tako čitati.** Zamka zaustavlja opšte automate, ne nekoga ko
je pročitao ovaj kod. CAPTCHA nije uzeta svesno: značila bi slanje IP adrese svakog korisnika
Google-u ili Cloudflare-u pri svakoj registraciji i rupu u CSP-u za njihove skripte, a pravu
granicu ovde ionako postavlja broj registracija po adresi.

---

## Pristup bazi

*(stavka 3)*

Klijent ne zna za bazu. Ne postoji ključ koji pregledač drži, javan ili ne — sav pristup ide
kroz API, koji jedini ima connection string.

Sama aplikacija se ne povezuje superkorisnikom: `db/init/01-app-role.sh` pravi nalog sa
pravom da čita i piše podatke, ali bez superkorisničkih prava. Ranije se povezivala nalogom
iz `POSTGRES_USER`, koji je u `postgres` imidžu superkorisnik.

Prosleđena zaglavlja se uzimaju u obzir samo sa privatnih adresa. Ovo je bitno: dok su liste
poverenja bile prazne, ograničenje broja zahteva se zaobilazilo slanjem drugog
`X-Forwarded-For` po zahtevu. Provereno u oba smera — 26 zahteva sa različitim vrednostima
prošlo je bez ijednog 429 pre ispravke, a šest ih je odbijeno posle.

---

## Osetljivi podaci

*(stavka 5)*

Šta aplikacija uopšte čuva: email, heš lozinke, i trenažne podatke — telesna masa, uzrast,
pol, opterećenja i ponavljanja.

- **Lozinke se ne čuvaju, nego heširaju** (PBKDF2-HMAC-SHA512, 210.000 iteracija).
  Nepovratno, što je jače od šifrovanja: nema ključa koji može da procuri.
- **U prenosu** sve ide kroz TLS čim je isporuka podešena kao gore.
- **Tajne** ne stoje u repozitorijumu, vidi ispod.

Šifrovanja pojedinačnih kolona nema, i to je odluka a ne propust. Šifrovanje u bazi štiti od
onoga ko dobije fajlove baze ali ne i aplikaciju — a ovde bi ključ morao da živi pored
aplikacije koja svaki red čita pri svakom prikazu, pa bi ko god dobije jedno dobio i drugo.
Ono što tu zaista pomaže je šifrovanje diska na mašini i zaštita rezervnih kopija, što je
posao okruženja a ne koda.

Podaci nisu ni posebno osetljivi u smislu u kom su to zdravstveni kartoni ili brojevi
kartica: najgore što otkrivaju je koliko neko diže i koliko je težak.

---

## Tajne i ključevi

*(stavke 1, 2)*

`.env` je u `.gitignore`; `.env.example` sadrži samo placeholder vrednosti. `appsettings.json`
ima **prazan** `Jwt:Key`. `docker-compose.yml` traži `POSTGRES_PASSWORD`, `APP_DB_PASSWORD` i
`JWT_KEY` kroz `:?` sintaksu, pa se bez njih ni ne pokreće. Lokalne vrednosti žive u .NET
user-secrets, van repozitorijuma.

API odbija da se pokrene ako je `Jwt:Key` kraći od 32 bajta — u **svakom** okruženju, jer
HS256 ispod 256 bita nije potpis nego ukras — i ako van razvoja liči na placeholder.
Provereno vrednošću iz `.env.example`: aplikacija odbija da se digne.

### Istorija

Kroz istoriju je jednom prošao ključ: `dev-only-super-secret-key-change-me-min-32-bytes-…`,
dakle vrednost koja i po imenu kaže da je razvojna, nikada korišćena van lokalne mašine.
Uklonjen je prepisivanjem istorije.

Provereno da ga nema: **nijedan ref na `origin` ne sadrži tu vrednost** — prošlo je kroz sve
grane na daljinskom, ne samo kroz `main`. Preživeo je još samo u tri lokalne pomoćne grane
napravljene pre prepisivanja (`backup-*`), koje nikada nisu bile poslate. Obrisane su, jer je
jedan `git push --all` bio dovoljan da ih vrati u opticaj. Posle brisanja pretraga po svim
dostupnim ref-ovima vraća nula pogodaka.

Da se ne oslanja na to da neko pazi, **gitleaks radi nad celom istorijom pri svakoj izmeni**
— vidi ispod.

---

## Zavisnosti

*(stavka 20)*

| | Rezultat |
|---|---|
| `dotnet list package --vulnerable` (svih 5 projekata) | **0** |
| `npm audit --omit=dev` (isporučeno) | **0** |
| `npm audit` (uključujući razvojne) | **0** |

Razvojne zavisnosti su ranije imale tri niske ranjivosti u lancu Angular alata
(`@babel/core`, `esbuild`). Nisu se isporučivale, ali su držale skener crvenim. Rešene su
`overrides` blokom koji podiže razrešene verzije unutar postojećeg lanca — provereno da
produkcijski build i svih 7 frontend testova i dalje prolaze.

**Bitnije od trenutnog nula je što se sada proverava samo.** Nalaz iz skeniranja stari sam od
sebe: paket koji je danas čist objavi ranjivost sledeće nedelje, bez ijedne izmene u ovom
repozitorijumu. Zato:

- **CI** (`.github/workflows/ci.yml`) na svaku izmenu i **ponedeljkom ujutru po rasporedu**
  radi build, testove, `dotnet list package --vulnerable`, `npm audit` i gitleaks nad celom
  istorijom. Korak sa NuGet-om čita ispis, jer ta naredba uvek izlazi sa nulom — bez toga bi
  bio zelen i kada nešto nađe. `npm audit --omit=dev` obara build; nalaz u razvojnim
  zavisnostima samo prijavljuje, da ranjivost u alatu ne zaustavi rad na aplikaciji.
- **Dependabot** (`.github/dependabot.yml`) donosi same nadogradnje — NuGet i npm nedeljno,
  GitHub akcije i Docker osnovne imidže mesečno. Bez toga nalaz stoji dok se neko ne seti da
  ga reši, a to je tačno stanje u kom je repozitorijum i bio.

---

## Šta ostaje otvoreno

Navedeno svesno, da ne izgleda kao da je pokriveno.

- **Ciljano zaključavanje tuđeg naloga.** Pet promašaja na pet minuta je oko jednog zahteva u
  minutu, ispod svakog upotrebljivog praga, pa ko zna tuđ email može da mu drži nalog
  zaključanim. Rešenja su postepeno usporavanje umesto tvrdog zaključavanja ili brojač vezan
  za par (nalog, adresa); oba menjaju ponašanje prijave.
- **Registracija odaje da li email već ima nalog** — ne porukom, koja je ujednačena, nego
  statusnim kodom. Pravo rešenje je potvrda email-om.
- **Nema oporavka zaboravljene lozinke**, jer nema slanja email-a.
- **Zatečene kratke lozinke ostaju.** Pravilo važi kad se lozinka postavlja, ne kad se
  proverava.
- **Odgovor koji stigne posle odjave može da napuni keš.** Nijedna komponenta ne otkazuje
  pretplatu pri uništavanju, pa odgovor koji nadživi odjavu upiše podatke u već ispražnjen
  keš. Usko (traži da odgovor kasni duže od cele prijave), ali stvarno; ispravka je
  otkazivanje pretplata na 26 mesta, što je zaseban posao.
- **Kontejnerske i TLS izmene nisu pokrenute.** Docker nije bio dostupan pri radu, pa su
  compose fajlovi, Caddyfile i skripta za bazu provereni statički — osim SQL dela skripte,
  koji jeste pokrenut nad pravim PostgreSQL-om. Pre oslanjanja uraditi
  `docker compose build && docker compose up` i proveriti da odgovor nosi
  `Strict-Transport-Security`.
- **Fontovi se povlače sa gstatic-a**, pa IP adresa korisnika odlazi trećoj strani pri
  svakom otvaranju stranice. Posluživanje sa istog porekla je jače i po privatnosti i po
  CSP-u.
- **Ograničenje broja zahteva deli budžet po IP adresi.** Više korisnika iza istog NAT-a
  deli isti budžet.

---

## Kako je proveravano

Većina nalaza je potvrđena **nad pokrenutom aplikacijom**, ne čitanjem koda: dva naloga i
pokušaji unakrsnog pristupa, filter isključen da bi se videlo šta se dešava bez njega,
falsifikovani tokeni, merenje vremena odgovora, slanje lažnog `X-Forwarded-For`, pokretanje
skripte za bazu nad pravim PostgreSQL-om sa neprijateljskom lozinkom, i posluživanje pravog
produkcijskog build-a iza CSP-a da bi se videlo šta politika zaista blokira. Taj poslednji
korak je uhvatio da bi prvobitni CSP obrisao sve ikone iz aplikacije.

Nekoliko puta je merenje oborilo očekivanje, i to je zabeleženo tamo gde stoji:
preusmeravanje na HTTPS koje nije preusmeravalo, i duga lozinka koja nije bila trošenje
procesora.

Testova ima **267** (`dotnet test` 260, `npm test` 7), od kojih dvadesetak čuva upravo
bezbednosna pravila opisana ovde.
