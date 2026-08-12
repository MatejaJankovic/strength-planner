# Makrociklusi

**Grana:** `feature/macrocycles`

## Problem iz rada

Zaključak navodi među pravcima daljeg razvoja: *"макроциклуси који би више мезоциклуса
повезали у дугорочан план са смењивањем циљева снаге и хипертрофије"*.

Do sada je svaki mezociklus stajao sam za sebe. Sistem je znao da planira četiri nedelje
unapred i ništa dalje, pa je smenjivanje ciljeva — standardan način da se blokovi vežu,
gde hipertrofija gradi tkivo na umerenim opterećenjima, a snaga koja sledi uči vežbača
da ga ispolji na teškim — ostajalo na korisniku da pamti i ručno pokreće.

## Rešenje

Nov entitet **`Macrocycle`** drži uređen niz blokova (`MacrocycleBlock`). Svaki blok
nosi svoj cilj i šablon; kada blok dođe na red, iz njega se generiše mezociklus.

Ključna odluka: **blokovi se ne generišu unapred.** Blok postoji kao *namera* od trenutka
pravljenja plana, a mezociklus se pravi tek kad prethodni bude gotov — da bi krenuo od
1RM vrednosti koje važe **tada**, a ne od procena starih nekoliko meseci. Plan koji bi
sve blokove izračunao unapred bio bi zastareo pre nego što se do njih stigne.

### Sve je makrociklus

Pojedinačan mezociklus je makrociklus sa jednim blokom — nema posebnog slučaja u modelu.
Postojeći ekran "Novi plan" i dalje radi isto, samo sada pravi plan sa jednim blokom, pa
se kasnije može produžiti. Migracija prevodi sve zatečene mezociklase u takve planove.

### Prelazak na sledeći blok

Kada se završi poslednji trening bloka, sledeći se generiše odmah i postaje aktivan.
Preuzimanje bloka ide uslovnim `UPDATE`-om nad `GeneratedAt`, pa dva istovremena
završetka poslednjeg treninga ne mogu da naprave dva mezociklusa za isti blok. Sve je u
istoj transakciji kao i završetak treninga.

Odgovor na završetak treninga nosi `nextBlock`, pa ekran odmah objasni šta se desilo.

## Šta je urađeno

- `Macrocycle` i `MacrocycleBlock` — nove tabele; blok ima jedinstven redni broj u planu
  i najviše jedan mezociklus (parcijalni jedinstveni indeks).
- `MacrocyclePlanner` — domenski deo koji gradi smenjujući niz ciljeva i čuva granice
  (1–6 blokova).
- `MacrocycleService` — pravljenje plana, čitanje, i automatski prelazak na sledeći blok.
- `MesocycleGenerator` sada radi i unutar već otvorene transakcije, pa se generisanje
  novog bloka odvija zajedno sa završetkom treninga koji ga je pokrenuo.
- `POST /api/macrocycles`, `GET /api/macrocycles/active`, `GET /api/macrocycles/{id}`.
- `POST /api/mesocycles` (postojeći put) sada pravi plan sa jednim blokom.
- Migracija `AddMacrocycles` prevodi zatečene mezociklase u planove sa jednim blokom.
  Id plana je namerno isti kao Id mezociklusa, pa je veza jednoznačna bez privremenih
  tabela, a šablon se prepoznaje po nazivima dana koje je generator upisao
  (`Push` → push-pull-legs, `Upper A` → upper-lower, inače full-body).
- Frontend: nova kartica **Plan** u donjoj navigaciji — vremenska traka blokova sa
  statusom i napretkom, i čarobnjak za pravljenje plana u kome dodavanje bloka samo
  smenjuje cilj prethodnog.
- Ekran treninga: poruka kada se blok zaokruži i otvori sledeći.

## Provera

- `dotnet build`, `dotnet test` (100 testova, bilo 89), `npm run build` — sve prolazi.
- `MacrocyclePlannerTests` pokriva smenjivanje iz oba cilja, plan sa jednim blokom,
  odbijanje broja blokova van opsega i to da se isti cilj nikada ne ponovi dvaput zaredom.
- Migracija nad zatečenom bazom: **33 mezociklusa → 33 plana i 33 bloka, nijedan
  nevezan**, a šabloni prepoznati tačno (full-body 4, push-pull-legs 15, upper-lower 14).
- End-to-end, plan od tri bloka: samo prvi je generisan; nepoznat šablon i sedam blokova
  odbijeni sa 400; posle svih 16 treninga prvog bloka odgovor nosi
  `nextBlock: {blockOrder: 2, blockCount: 3, goal: "Strength"}`, drugi blok postaje
  aktivan, a treći i dalje čeka.
- End-to-end, prenos opterećenja: Bench Press kreće od 77.5 kg (1RM 100), kroz blok
  e1RM naraste na 118.2 kg, i **drugi blok kreće od 90 kg** — dakle od postignutog, ne
  od početnog 1RM.
- U pretraživaču: kartica Plan je u navigaciji, prazno stanje nudi pravljenje plana,
  čarobnjak sam smenjuje ciljeve pri dodavanju bloka (Hipertrofija → Snaga →
  Hipertrofija) i računa ukupno trajanje, dugme je onemogućeno bez naziva, a napravljen
  plan se prikazuje kao *"3 bloka · 12 nedelja"* sa blokovima "U toku" / "Na čekanju".

## Ispravke posle revizije koda

### Neuspeh generisanja je mogao trajno da blokira završetak treninga

Prelazak na sledeći blok radi u istoj transakciji kao i završetak treninga. Generator
odbija šablon kome neka vežba više ne postoji (obrisana korisnička vežba, promenjen
seed) — a taj izuzetak bi srušio celu transakciju: status sesije, e1RM zapise, progresiju
i deload. Svaki sledeći pokušaj bi pucao isto, pa korisnik svoj trening ne bi mogao da
završi **nikada**, zbog usputne pogodnosti.

Prelazak je sada ograđen `SAVEPOINT`-om: neuspeh se vraća samo do njega, trening se
uredno završava, a blok ostaje negenerisan. Sam `catch` ne bi bio dovoljan — da je pukla
neka SQL naredba, transakcija bi u PostgreSQL-u bila u prekinutom stanju i commit bi
svejedno pao.

### Napušten plan je mogao da preotme aktivni mezociklus

`AdvanceIfFinishedAsync` nije proveravao da li je plan aktivan. Scenario: plan P1 ima
zaostalu sesiju, korisnik napravi novi plan P2, pa se vrati i dovrši tu sesiju — P1 bi
generisao svoj sledeći blok, a generator bi ga postavio kao aktivan mezociklus. Ekran
"Plan" bi prikazivao P2, a "Trening" bi vodio kroz P1. Sada samo aktivan plan napreduje.

### Brisanje mezociklusa je zaglavljivalo plan

FK je `SetNull`, pa je brisanje mezociklusa ostavljalo blok bez njega, ali sa upisanim
`GeneratedAt`. Ekran ga je prikazivao kao *"Na čekanju — generiše se kad prethodni blok
bude gotov"*, a to se nikada ne bi desilo: jedini okidač je završetak prethodnog
mezociklusa, koji više ne postoji. Plan bi ostao aktivan i prazan zauvek.

Dodato je samoisceljenje: `GET /api/macrocycles/active` proverava da li plan ima tekući
blok i generiše ga ako nema. Isti uslovni `UPDATE` čuva od dvostrukog generisanja, pa je
bezbedno pozvati sa čitanja. To usput rešava i slučaj u kome dva istovremena završetka
poslednje dve sesije nedelje ne vide jedan drugog, pa nijedan blok ne prepozna kao gotov.
Status bloka sada razlikuje *"Na čekanju"* od *"otkazan"* (mezociklus obrisan).

### Plan sa jednim blokom je preimenovao korisnikov mezociklus

Postojeći ekran "Novi plan" je slao naziv "Base Hypertrophy", a nazad je stizalo
*"Base Hypertrophy — blok 1 (Full Body)"* — što je i prikazivano kao naslov na dashboardu.
Dokumentacija je tvrdila da taj put radi isto kao ranije; sada zaista radi: plan sa jednim
blokom zadržava naziv koji je korisnik uneo.

### Pravilo smenjivanja ciljeva živelo je na dva mesta

`MacrocyclePlanner.AlternatingGoals` — jedini domenski algoritam ove funkcionalnosti —
nije bio pozvan nigde osim iz testova, dok je smenjivanje bilo ponovo napisano u
Angular komponenti. Šest testova je pokrivalo kod koji aplikacija nikada ne izvršava, što
oslabljuje tvrdnju rada o jedinično testiranom algoritamskom jezgru umesto da je potvrdi.
Dodat je `GET /api/macrocycles/suggested-blocks`, iz koga čarobnjak uzima početni raspored.

### Sitnije

- Transakcija se nije oslobađala kada se iz metode izađe izuzetkom (`await using`
  zamenjeno običnom promenljivom); vraćeno na `await using`, koji je na `null`-u no-op.
- `BlockCount` u poruci o prelasku računao se preko `macrocycle.Blocks.Count` sa
  rezervnom granom koja je davala pogrešan broj; sada se uvek broji upitom.
- Novi blok je mogao da bude zakazan ceo u prošlosti ako je prethodni razvučen; početak
  se sada ne pomera pre današnjeg dana.
- Pet upita nije bilo ograničeno po korisniku (bez stvarnog curenja, jer su ulazi već
  bili provereni, ali suprotno pravilu iz `CLAUDE.md`).
- 404 na planu nije praznio keš, pa je na ekranu ostajao stari plan a prazno stanje se
  nije prikazivalo; pravljenje plana nije poništavalo keširani mezociklus.
- Uklonjena mrtva konstanta `BlockDurationWeeks`.

### Provera posle ispravki

- `dotnet build`, `dotnet test` (100 testova), `npm run build` — sve prolazi.
- Plan sa jednim blokom kroz stari put: naziv ostaje **"Base Hypertrophy"**.
- Napušten plan: posle pravljenja novog plana i dovršavanja zaostale sesije starog,
  aktivan plan i aktivan mezociklus ostaju novi, `nextBlock` je `null`.
- Brisanje mezociklusa: posle brisanja i običnog čitanja plana blok 1 je ponovo
  generisan i aktivan, a blok 2 i dalje čeka.
- `GET /macrocycles/suggested-blocks`: 4 bloka → H/S/H/S, 3 bloka od snage → S/H/S,
  1 blok → H, a 9 blokova → 400.
- U pretraživaču: čarobnjak se puni sa servera (Hipertrofija, Snaga) i plan se pravi
  ispravno — *"2 bloka · 8 nedelja"*.
