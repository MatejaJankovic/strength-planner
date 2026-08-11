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
