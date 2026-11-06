# Varijante u izduženom položaju

**Grana:** `feature/lengthened-variants`

Korak 8 plana posle revizije (6. 11. 2026), stavka A3. Odluka vlasnika: Seated Leg Curl i Incline
Dumbbell Curl idu u katalog, a u šablone samo tamo gde probe pokažu da MEV i MRV ne trpe.

## Dokazi

Trening u izduženom položaju je bar jednako dobar kao trening kroz pun opseg (metaanaliza
delimičnog naspram punog opsega, Wolf i sar. 2023). Sistematski pregled o uzdužnom rastu mišića
(Wolf i sar. 2025, *Sports Medicine and Health Science*, doi 10.1016/j.smhs.2025.03.001) zaključuje
da može biti i bolji, ali da su dokazi mešoviti. Za dve vežbe iz ovog plana dokaz nije isti:

- **Sedeći pregib nogu.** Uz isti volumen dao je veći rast zadnje lože od ležećeg (Maeo i sar.
  2021, *Med Sci Sports Exerc* 53(4)). Studija je jednu nogu trenirala sedeći, a drugu ležeći, na
  20 netreniranih ispitanika, 12 nedelja; zadnja loža je porasla oko 14% naspram 9%. Prednost je
  bila u dvozglobnim delovima zadnje lože, ne u kratkoj glavi bicepsa butine. U sedećem pregibu je
  kuk savijen, pa zadnja loža radi izdužena. To je direktno poređenje dve varijante iste vežbe.
- **Pregib na kosoj klupi.** Rame je iza tela, pa biceps radi izdužen. Jedino direktno poređenje
  koje sam našao je sa pregibom na Scott klupi (*Int J Sports Med* 2025, PMID 39809454; 8 nedelja,
  63 netrenirane žene). Našlo je samo regionalne razlike (kosa klupa više proksimalno, Scott
  distalno), a ne ukupnu prednost.

Po odluci D1 je priručnik podrazumevan, a literatura menja izbor samo gde je jasno na drugoj
strani. Zato sedeći pregib ulazi u šablone, a pregib na kosoj klupi samo u katalog.

## Šta je urađeno

- **Katalog.** `Seated Leg Curl` (mašina, zadnja loža 1.0) i `Incline Dumbbell Curl` (bučice,
  biceps 1.0). Seeder ih upisuje pri startu, kao svaku novu vežbu kataloga, pa migracija nije
  potrebna. Izmereno na razvojnoj bazi: obe su upisane sa koracima 5 kg i 2 kg.
- **Šabloni.** Svih 14 mesta na kojima je stajao `Leg Curl` sada nosi `Seated Leg Curl`. `Leg
  Curl` ostaje u katalogu zbog zatečenih planova i ličnih šablona. Zatečeni blokovi zadržavaju
  stari pregib, a zamena vežbe (#111) im nudi upravo sedeći, jer je isti mišić, tip i opseg.
- **Sledeći blok postojećeg plana** se generiše iz šablona kakav je tada, pa dobija sedeći pregib,
  i to **bez maksimuma**. Istorija ležećeg se ne prenosi, jer se opterećenja dve sprave
  razlikuju. Napomena o vežbama bez maksimuma ga tada navodi, a prvo opterećenje se unosi ili
  upisuje po osećaju, kao za svaku novu vežbu. Istorija pregiba se u analitici deli na dva imena
  (uočio pregled).
- **`Incline Dumbbell Curl`** nije ni u jednom šablonu. Dostupan je kroz zamenu vežbe (za Dumbbell
  Curl, Barbell Curl i Hammer Curl) i kroz lične šablone.

## Proba: MEV i MRV

Udeli nove vežbe su isti kao kod one koju menja, pa se volumen po mišiću ne može pomeriti. To je
izmereno, a ne samo zaključeno: posle zamene u šablonima svih 929 postojećih testova prolazi bez
izmene. Tu spadaju zabeleženi spiskovi parova ispod MEV-a za naprednog, test referentnog nivoa,
test MRV-a i spisak mišića bez serija. Vraćanje šablona na Leg Curl ostavlja tih 929 zelenim.

Zato postoji test same odluke, `BuiltInTemplates_CurlTheHamstringsSeated`: nijedan ugrađeni
šablon ne sme da koristi Leg Curl ni Incline Dumbbell Curl, a obe vežbe moraju da ostanu u
katalogu. Vraćanje šablona ga obara (1 od 930).

## Provera

- `dotnet test`: **930** (bilo 929); `npm test`: 213; `npm run build` prolazi (klijent nije menjan).
- Uživo, na 375 px, nalog za testiranje:
  - u postojećem bloku snage (Lower A) zamena za Leg Curl nudi Seated Leg Curl;
  - čarobnjak za nov plan pokazuje Seated Leg Curl na oba dana za noge Upper/Lower šablona, a
    Leg Curl-a nema;
  - nema prelivanja.
