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

## Kontrola pristupa

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

**Nijedan nije prošao.** Dva zahteva vratila su 200 sa praznom listom — parametar je bio id
*sistemske* vežbe, iste za sve, pa je B video svoju praznu istoriju. To je ispravno i usput
ne odaje ni da tuđi zapis postoji.

---

## Nalozi i tokeni

### Lozinke

Najmanja dužina je **10 znakova**, na jednom mestu (`PasswordPolicy.MinimumLength`) koje
poštuju i Identity i validacija zahteva i ekran za registraciju. Ranije je bilo šest bez
ijedne dodatne provere, što je prihvatalo `123456` i `aaaaaa` — izmereno, ne pretpostavljeno.

Uslovi složenosti su namerno isključeni: dužina je jedina mera koja stvarno otežava
pogađanje, dok pravila o velikim slovima i znakovima uglavnom teraju ljude na predvidive
obrasce. Lozinke hešira Identity (PBKDF2); sopstvenog heširanja nema.

### Prijava

- Ista poruka za pogrešnu lozinku, nepostojeći nalog i zaključan nalog.
- **Isto i vreme odgovora.** Nepostojeći nalog se ranije vraćao pre heširanja, pa je razlika
  sama po sebi odavala koji email ima nalog. Sada se i na promašaju troši isti posao:
  razlika je pala sa desetina milisekundi na ~7 ms.
- Zaključavanje posle 5 promašaja na 5 minuta.
- Ograničenje broja zahteva na auth rutama (20/min po adresi), sa telom odgovora i
  `Retry-After`.

### Tokeni

JWT, HS256, važenje 60 minuta. Token nosi `sub`, `email`, `jti` i **security stamp** iz
Identity-ja, koji se proverava pri svakom zahtevu. Zato promena lozinke **odmah poništava
sve ranije izdate tokene** — provereno: stari token pređe sa 200 na 401 preko promene, a
pozivalac dobija nov da ga izmena ne izbaci iz aplikacije.

Provereno i da se token ne da falsifikovati:

| Pokušaj | Odgovor |
|---|---|
| ispravan token | 200 |
| bez tokena | 401 |
| izmenjen potpis | 401 |
| izmenjen `sub`, zadržan stari potpis | 401 |
| `alg=none` | 401 |

Ključ mora da bude bar 32 bajta u **svakom** okruženju, a van razvoja se odbijaju i
placeholder vrednosti — aplikacija odbija da se pokrene, provereno vrednošću iz
`.env.example`.

### Promena lozinke

Traži trenutnu lozinku, prolazi kroz isto zaključavanje kao prijava (inače bi bila
neograničen orakl za pogađanje onome ko se domogne tuđeg tokena), i traži potvrdu nove.

Potvrda nije formalnost: **u sistemu nema oporavka lozinke**, pa bi greška u kucanju značila
trajan gubitak naloga i svih podataka u njemu. Ekran to i piše.

---

## Podaci na klijentu

Servisi koji keširaju korisničke podatke žive koliko i aplikacija, pa promena identiteta bez
osvežavanja stranice ostavlja podatke prethodnog korisnika u memoriji. To je jednom i
promaklo — `OneRepMaxService` nije imao `reset()`, pa su na deljenom računaru maksimumi
prethodnog korisnika ostajali vidljivi sledećem.

Sada se pri odjavi **i pri prijavi** prazne svi keševi, a `auth.service.spec.ts` to drži:
puni svaki keš, prolazi kroz pravi `login()` i `register()`, pa proverava da su prazni.
Testovi su i dokazani — uklanjanje bilo kog `reset()` poziva obara ih.

---

## Injekcija i ulazni podaci

Nema nijednog `FromSqlRaw`, `ExecuteSqlRaw` ni `NpgsqlCommand` u celom repozitorijumu. Svih
sedam `migrationBuilder.Sql(...)` blokova su statični literali bez interpolacije. Svi
`ExecuteUpdateAsync` pozivi koriste tipizirane `SetProperty` lambde. Connection string dolazi
iz konfiguracije i ne dodiruje korisnički unos.

Nema ni mass assignmenta: nijedan zahtev-DTO ne nosi `UserId`, `Id`, `IsCustom`,
`CreatedByUserId`, `IsActive`, `Status` ni `Source` — sve se postavlja na serveru.

Granice ulaza prate bazu: `Equipment` je ograničen na 32 znaka koliko ima i kolona (ranije
64, pa je ispravan-na-oko zahtev padao kao 500 umesto 400), a broj mišićnih grupa na onoliko
koliko ih sistem poznaje. Oba su pokrivena testovima, uključujući test koji čuva da se
granica ne razmimoiđe sa katalogom.

---

## Isporuka

Detalji u [`deployment-security.md`](deployment-security.md). Ukratko:

- nginx šalje CSP, `nosniff`, `DENY` okvir, referrer i permissions politiku; verzija se ne
  objavljuje. Zaglavlja se uključuju u svaki `location`, jer ih nginx ne nasleđuje u blok
  koji ima svoj `add_header`.
- `index.html` ide sa `no-cache`, da nadogradnja ne ostavi korisnika na staroj verziji.
- Nijedan kontejner ne radi kao root.
- API je objavljen samo na petlji; spolja se do njega dolazi kroz nginx.
- Aplikacija se povezuje na bazu nalogom bez superkorisničkih prava.
- Prosleđena zaglavlja se uzimaju u obzir samo sa privatnih adresa. Ovo je bitno: dok su
  liste poverenja bile prazne, ograničenje broja zahteva se zaobilazilo slanjem drugog
  `X-Forwarded-For` po zahtevu. Provereno u oba smera — 26 zahteva sa različitim vrednostima
  prošlo je bez ijednog 429 pre ispravke, a šest ih je odbijeno posle.

---

## Zavisnosti

| | Rezultat |
|---|---|
| `dotnet list package --vulnerable` (svih 5 projekata) | **0** |
| `npm audit --omit=dev` | **0** |
| `npm audit` (uključujući razvojne) | 3 niske, ne isporučuju se |

---

## Šta ostaje otvoreno

Navedeno svesno, da ne izgleda kao da je pokriveno.

- **TLS nije podešen u isporuci.** Najvažnija preostala stavka, i jedina koju kod ne može da
  reši umesto vlasnika. Uputstvo je u `deployment-security.md`.
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
- **Kontejnerske izmene nisu pokrenute.** Docker nije bio dostupan pri radu, pa su promena
  osnovnog imidža, `USER` direktive i skripta za bazu provereni statički. Pre oslanjanja
  uraditi `docker compose build && docker compose up`.
- **Fontovi se povlače sa gstatic-a**, pa IP adresa korisnika odlazi trećoj strani pri
  svakom otvaranju stranice. Posluživanje sa istog porekla je jače i po privatnosti i po
  CSP-u.

---

## Kako je proveravano

Većina nalaza je potvrđena **nad pokrenutom aplikacijom**, ne čitanjem koda: dva naloga i
pokušaji unakrsnog pristupa, falsifikovani tokeni, merenje vremena odgovora, slanje lažnog
`X-Forwarded-For`, i posluživanje pravog produkcijskog build-a iza CSP-a da bi se videlo šta
politika zaista blokira. Taj poslednji korak je uhvatio da bi prvobitni CSP obrisao sve
ikone iz aplikacije.

Testova ima **224** (`dotnet test` 217, `npm test` 7), od kojih devet čuva upravo
bezbednosna pravila opisana ovde.
