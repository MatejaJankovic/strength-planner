# Šum u signalu snage za granice volumena

**Grana:** `fix/landmark-signal-noise`

Deveta grana iz revizije nauke o treningu (odeljak F), nalaz F11.

## Problem

Od runde 11 MAV i MEV uče iz promene snage, a MRV čita pad snage kao znak umora. Prag je
1%, i odluka se donosila iz **jednog** nedeljnog čitanja. Nalaz F11 je tvrdio da je prag manji
od šuma. Proba je pokazala da je posledica gora od toga: granice vežbača koji napreduje
klizile su naniže.

Šum jednog čitanja:

- Pogrešno procenjena rezerva za jedno ponavljanje pomera e1RM za 1 / (30 + efektivna
  ponavljanja), oko **2.4%** za seriju 8–12 uz RIR 1–2. Vežbači u proseku potcenjuju broj
  ponavljanja do otkaza za 0.95, uz veliku heterogenost (Halperin i sar. 2022).
- Sam maksimum varira iz dana u dan: medijana koeficijenta varijacije ponovljenog testa 1RM je
  **4.2%** (Grgic i sar. 2020).
- Čitanje poredi dve nedelje, pa nosi šum obe. Stvaran nedeljni napredak srednjeg nivoa je
  ispod 1%.

Razvojna baza nema stvarne dugačke istorije (jedini nalog sa dužom istorijom je E2E nalog sa
veštačkim serijama), pa je šum modelovan iz literature, sa opreznim pretpostavkama: greška
procene od jednog ponavljanja i dnevno variranje od 2%, ispod te medijane.

## Šta je proba pokazala

Simulacija (Python, 200.000 nedelja po scenariju) za vežbača koji stvarno napreduje 1%
nedeljno:

| Čitanje kaže | Jedno čitanje | Dva uzastopna se slažu |
|---|---|---|
| napredak | 50% nedelja | 17% |
| ravno | 20% | 5% |
| **pad** | **30%** | **3%** |
| ništa (ne slažu se) | - | 76% |

Svaka nedelja koja je pročitala pad spuštala je MRV (a MAV ako je nedelja bila blizu njega), a
MRV raste samo blizu sebe i uz rezervu ponavljanja. Plafon oporavka vežbača koji napreduje
zato je klizio naniže.

Provereno i nad pravom funkcijom `VolumeAdaptation.Adjust`: godinu dana treninga na MAV-u,
šum po nedelji tako da čitanje ima sd 3.5%, prosek preko 500 semena (seme grudi: MEV 10,
MAV 16, MRV 22):

| Vežbač | Staro pravilo | Novo pravilo |
|---|---|---|
| napreduje 1% nedeljno | MAV 11.0, MRV 12.0 - na podu | MAV 16.3, MRV 20.6 |
| napreduje 0.5% nedeljno | MAV 11.0, MRV 12.0 | MAV 15.5, MRV 19.7 |
| stoji | MAV 11.0, MRV 12.0 | MAV 14.4, MRV 18.2 |
| slabi 2% nedeljno | MAV 11.0, MRV 12.0 | MAV 11.0, MRV 12.0 |

I sa upola manjim šumom (sd čitanja 1.8%) staro pravilo vežbaču koji napreduje 1% spušta MRV na
14.8, a novo ga drži na 21.9.

## Rešenje

- **Napredak, pad i ravna nedelja traže dva uzastopna uporediva čitanja koja se slažu**
  (`VolumeAdaptation.Agree`). `VolumeResponse` nosi i prethodno čitanje: prethodnu trenažnu
  nedelju naspram one pre nje.
- Prag ostaje 1%. Sa dva čitanja lažni padovi padaju sa 30% na 3% nedelja. Cena je sporija
  reakcija: stvaran pad od 2% nedeljno se uhvati u 29% nedelja umesto 61%. Granice se ionako
  pomeraju najviše za jednu seriju nedeljno, a prema tabeli iznad, pravilo i dalje do kraja
  godine dovodi granice vežbača koji slabi do poda.
- Odbačene varijante iz probe: prag od 3% (lažni padovi i dalje 14%) i prosek dva čitanja sa
  pragom od 1% (lažni padovi 14%).
- Ocena umora i dalje čita jedno čitanje, jer za deload već traži dva nezavisna signala.
  *(Ne važi od runde 15 - vidi „Kasnije (runda 15)".)*
- `VolumeLandmarkService` računa prethodno čitanje preko nove
  `ComparableWeek.PreviousTrainingWeekIdAsync`. Nedelje deload-a se preskaču kao i do sada.

Posledica: prva nedelja bloka nema čitanje, a druga ima samo jedno. Snaga zato prvi put sme
da pomeri granice u trećoj nedelji. Ravan blok sa deload-om u četvrtoj nedelji ima jednu
takvu nedelju, a linearan tri.

## Provera

- `dotnet test`: **814** (bilo 808); `npm test` i `npm run build` bez izmena na klijentu.
- Postojeći testovi koji su zadavali jedno čitanje sada zadaju dva ista, pa zadržavaju značenje.
- Novi testovi:
  - jedno čitanje ne pomera nijednu granicu;
  - čitanja koja se ne slažu ne pomeraju ništa;
  - dva pada zaredom spuštaju MRV, a jedan ne;
  - test sa šumom (100 semena, godinu dana) drži oba ishoda iz tabele: novo pravilo čuva
    granice vežbača koji napreduje, a staro ih spušta na pod.
- Merenje vraćanjem (commit, `Agree` čita samo tekuće čitanje, rebuild): obara **6** od 814.
- **Spoj u servisu ne vidi nijedan test.** Merenje uživo, u oba smera, sa rebuild-om i
  restartom između. Srednji nivo, Upper/Lower, ravan blok, sve serije 10 ponavljanja uz RIR 2,
  a opterećenje 100%, 95% pa 90%:

  | Posle | Staro pravilo (grudi / leđa) | Novo pravilo |
  |---|---|---|
  | 1. nedelje | MAV 16 / 18, MRV 22 / 25 | isto |
  | 2. nedelje | MAV 15 / 17, MRV 21 / 24 (jedno čitanje) | bez promene |
  | 3. nedelje | MAV 14 / 16, MRV 20 / 23 | MAV 15 / 17, MRV 21 / 24 (dva čitanja se slažu) |

## Ograničenja

- Šum je modelovan iz literature, a ne izmeren na stvarnim korisnicima. Pravilo je izabrano
  tako da bude ispravno i uz upola manji šum.
- Granice uče sporije: kratak blok daje jednu ili dve odluke. *(Ispravljeno posle review-a: u
  stvarnim blokovima je bilo oko tri odluke godišnje u ravnom bloku - vidi ispod.)*
- Vežbač koji stoji i dalje gubi oko dve serije MAV-a za godinu dana, jer „ravno" zahteva da
  oba čitanja budu u ±1%, što je uz šum retko, a pad zaredom se ipak desi. *(Rešeno posle
  review-a, vidi ispod.)*

## Kasnije (runda 15)

Ocena umora sada računa pad snage tek kad ga potvrdi i prethodna uporediva nedelja. Jedan slab
dan spušta i RIR i procenu maksimuma, pa ta dva signala nisu bila nezavisna: vežbač koji
napreduje 1% nedeljno dobijao je lažan deload u 12.5% šestonedeljnih blokova na MAV-u i 52.8% na
MRV-u, a sa potvrdom 0.3% i 3.0% (`docs/simulations/fatigue_signal_noise.py`; ranije navedenih
20% i 73% je bio međuzbir koji skripta ne daje). Vidi [fatigue-signal-noise.md](fatigue-signal-noise.md).

## Posle review-a #94: prosek dva čitanja

Review #94 je izmerio pravilo „oba čitanja preko 1%" u strukturi blokova kakvu aplikacija zaista
ima. Čitanje postoji tek od druge nedelje bloka, deload nema ni čitanje ni odluku, a svaki blok
kreće ispočetka. Tabele iznad su pretpostavljale čitanje u svakoj od 52 nedelje, pa su govorile
o modelu, a ne o aplikaciji. U stvarnim blokovima to pravilo:

- odlučuje oko **3** puta godišnje u ravnom bloku i **6** u linearnom (1.4 i 2.8 kad čitanje
  postoji u dve trećine nedelja, kako je runda 11 izmerila);
- vežbaču koji stoji spušta cilj (15.7 / 15.3) umesto da ga digne, kako tabela pravila kaže;
- slab pad hvata slabo: vežbač koji godinu dana gubi 2% nedeljno ostaje na MRV 18 u ravnom i
  14.5 u linearnom bloku, a ne „na podu";
- ni staro pravilo, sa jednim čitanjem, u stvarnim blokovima ne spušta granice „do poda".
  Napredujućem vežbaču ostavlja MAV 12.7 i MRV 14.7 u ravnom bloku, odnosno 11.6 i 12.9 u
  linearnom. Šteta je bila stvarna, samo manja nego što su tabele iznad pokazivale.

`docs/simulations/landmark_signal_noise.py` tačno prenosi `VolumeAdaptation.Adjust` i poredi
pravila na ravnim i linearnim blokovima (godinu dana na MAV-u, seme 16 / 22, 400 semena). Izbor
je **prosek dva uzastopna čitanja, prag 2.5%** (`VolumeAdaptation.TwoWeekChangeThreshold`).
Prosek dva uzastopna čitanja je pola promene kroz dve nedelje, jer se nedelja u sredini skrati,
pa ima upola manji šum od jednog čitanja. Rezultat je MAV / MRV posle godinu dana:

| Blok | Pravilo | napreduje 1%/ned. | stoji | slabi 2%/ned. | odluka godišnje | lažnih padova godišnje (napredak 0.5%) |
|---|---|---|---|---|---|---|
| ravan | oba preko 1% (#94) | 16.2 / 21.6 | 15.7 / 21.0 | 12.6 / 17.9 | 3.1 | 0.7 |
| ravan | prosek, 2.5% | 17.7 / 21.7 | 17.2 / 21.0 | 14.3 / 16.8 | 13.0 | 0.6 |
| linearan | oba preko 1% (#94) | 16.4 / 21.3 | 15.3 / 20.1 | 11.4 / 14.5 | 6.3 | 1.1 |
| linearan | prosek, 2.5% | 17.5 / 21.4 | 16.7 / 20.1 | 11.6 / 12.7 | 26.0 | 1.1 |

- **Lažnih padova je isto malo**, a prag je izabran baš tako. Na 1.5% i 2% ih je dva do tri
  puta više, a na 3% pravilo slabije hvata stvaran pad (MRV 14.6 posle godinu dana pada u
  linearnom bloku).
- **Stvaran pad hvata jače**, bar za plafon (MRV 12.7 umesto 14.5 u linearnom bloku). U ravnom
  bloku cilj vežbača koji slabi pada sporije (14.3 umesto 12.6), a plafon brže.
- **Vežbač koji stoji dobija „cilj gore"**, a odluka ima četiri do osam puta više.
- **Cena:** vežbaču koji napreduje cilj blago raste, za oko 1.5 serije, do granice koju
  `ClampTarget` dozvoljava. Uz realan šum, „napreduje polako" i „stoji" se iz dve nedelje ne
  razlikuju, pa pravilo za oba bira više stimulusa. Plafon ostaje na mestu.
- **MEV** se ponaša ispravno (linearan blok, trening na minimumu): kod vežbača koji napreduje
  pada na 6.1, kod onog koji stoji ostaje 10.0, a kod onog koji slabi raste na 15.0. Odbačen je
  hibrid u kome pad traži da se oba čitanja slože, a ostalo se sudi po proseku od 1%: on je
  minimum vežbača koji stoji spuštao sa 10 na 5.9.

Prag od 1% (`StrengthChangeThreshold`) ostaje za potvrdu pada u oceni umora (runda 15).

### Provera

- `dotnet test`: **843** (bilo 838). Pravilo iz #94 vraćeno uz sve ostalo isto obara **5**:
  - prag se pomerio na 2.5% (2% je sada „stoji");
  - čitanja koja se ne slažu se uprosečavaju (+3% pa −3% je „stoji", cilj raste);
  - test sa šumom sada modeluje stvarne linearne blokove, sa vežbačem koji napreduje, stoji i
    slabi.
- **Uživo, u oba smera** (rebuild i restart između): srednji nivo, ravan Upper/Lower, sve serije
  10 × RIR 2, opterećenje 100% → 103% → 100%, dakle čitanja od oko +3% pa −3%. Po pravilu iz
  #94 se posle treće nedelje ne pomera ništa, jer se čitanja ne slažu. Po novom grudi idu
  MAV 16 → 17 i leđa 18 → 19, a MRV stoji.

### Šta ostaje

- Šum je i dalje model iz literature, a ne mera stvarnih korisnika. Koliko vežbačeva procena
  RIR-a varira iz nedelje u nedelju je pretpostavka modela, ne podatak iz Halperin i sar. 2022,
  koji meri prosečnu grešku (potcenjivanje od oko jednog ponavljanja).
- Preskočena nedelja ili nedelja bez poređenja ućutkuje i nedelju posle sebe. Uputstvo to sada
  kaže.
- Kad nema dovoljno uporedivih serija, odluka nema, ma koje pravilo bilo.

