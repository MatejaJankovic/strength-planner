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
- Granice uče sporije: kratak blok daje jednu ili dve odluke.
- Vežbač koji stoji i dalje gubi oko dve serije MAV-a za godinu dana, jer „ravno" zahteva da
  oba čitanja budu u ±1%, što je uz šum retko, a pad zaredom se ipak desi.

## Kasnije (runda 15)

Ocena umora sada računa pad snage tek kad ga potvrdi i prethodna uporediva nedelja. Jedan slab
dan spušta i RIR i procenu maksimuma, pa ta dva signala nisu bila nezavisna: vežbač koji
napreduje 1% nedeljno dobijao je lažan deload u 20% blokova na MAV-u i 73% na MRV-u (simulacija
sa šumom iz runde 14). Vidi [fatigue-signal-noise.md](fatigue-signal-noise.md).
