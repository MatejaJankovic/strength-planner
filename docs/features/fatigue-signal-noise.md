# Pad snage u oceni umora traži dve nedelje

**Grana:** lokalno `fix/fatigue-signal-noise`, na istoj grani kao korekcija po Epley-u jer
okruženje sesije dozvoljava samo jednu.

Druga grana petnaestog kruga, nalaz G2 iz revizije posle runde 14.

## Problem

Ocena umora pokreće deload kad zbir četiri signala pređe 0.60. Nijedan signal ga ne nosi sam
(najteži, RIR, nosi 0.35), pa „bar dva moraju da se slože". To štiti samo ako su signali
nezavisni. Runda 11 je zato odvojila otkaz od RIR-a, a runda 14 (#94) je pokazala da je jedno
nedeljno čitanje snage pretežno šum. Za granice volumena #94 je tražio dve nedelje, a za ocenu
umora je jedno čitanje ostavio, uz obrazloženje da ocena ionako traži dva signala.

Ta dva signala nisu nezavisna. **Jedan slab dan spušta oba:** vežbač donese istu težinu i isti
cilj ponavljanja, prijavi manje u rezervi, i procena maksimuma te nedelje padne zajedno sa
tim. Čitanje snage ima sd oko 3.5% (#94), a jedno ponavljanje rezerve u 8–12 vredi oko 2.4%,
pa slab dan od 5% istovremeno puni i RIR (0.35) i pad snage (0.25): tačno 0.60.

## Merenje

Simulacija (Python, 30.000–40.000 blokova po scenariju), model šuma iz #94: nivo snage po
nedelji sd 2.47%, čitanje je razlika dve nedelje. Rezultat zavisi od toga kako vežbač bira
kad da stane, pa su izmerena oba krajnja slučaja:

- **(A)** drži zadat broj ponavljanja i iskreno prijavljuje rezervu, pa slab dan spušta RIR;
- **(B)** staje kad oseti ciljni RIR, pa slab dan spušta broj ponavljanja, a RIR se pomera
  samo pogrešnom procenom.

Udeo blokova od pet nedelja u kojima ocena umora povuče deload, vežbač koji stvarno napreduje
1% nedeljno:

| | Nedelje na MAV-u | Nedelje na MRV-u |
|---|---|---|
| (A), jedno čitanje | **20%** | **73%** |
| (B), jedno čitanje | 0% | 0.1% |

Pod (B) nema problema. Pod (A) svaki peti blok gubi nedelju treninga na deload koji ništa nije
izazvalo, a u nedeljama na MRV-u skoro svaki.

Razmotrene varijante, pod (A), udeo blokova sa deload-om:

| Varijanta | +1%/ned., MAV | +1%, MRV | −2%/ned., MAV | −3%, MRV |
|---|---|---|---|---|
| jedno čitanje (staro) | 20.1% | 73.1% | 73.6% | 100% |
| oba čitanja ≥ 5% (manji pad od dva) | 0.0% | 3.1% | 3.2% | 72.0% |
| prosek dva čitanja | 0.1% | 11.8% | 14.5% | 98.9% |
| prosek dva čitanja, puna težina na 3% | 3.4% | 25.3% | 65.9% | 99.8% |
| prethodno čitanje negativno | 1.1% | 10.5% | 33.5% | 90.8% |
| **prethodno čitanje ≤ −1% (izabrano)** | **0.5%** | **6.0%** | **24.7%** | **83.3%** |

## Rešenje

**Pad snage ulazi u ocenu samo kad je i prethodna uporediva nedelja bila pad**
(`FatigueEvaluator.ConfirmedStrengthDrop`). „Pad" znači isto što i kod granica volumena: bar
1% (`VolumeAdaptation.StrengthChangeThreshold`), pa reč u aplikaciji ima jedno značenje.
Veličinu nosi tekuća nedelja, a prethodna samo potvrđuje smer. Dva čitanja dele nedelju između
sebe, pa ih šum razilazi po znaku, a stvaran pad ih slaže.

- `WeeklyFatigue` nosi i `PreviousE1RmChangeShare`: prethodnu trenažnu nedelju naspram one pre
  nje, nulu kad poređenja nema.
- `DeloadService` ga računa istim pravilom uporedive nedelje (`ComparableWeek`) kao i tekuće
  čitanje.

Cena: stvaran pad se hvata kasnije i ređe. Pad od 3% nedeljno u nedeljama na MRV-u se i dalje
uhvati u 83% blokova (bilo 100%), a pad od 2% na MAV-u u četvrtini (bilo 74%). Planirani
deload na kraju bloka ostaje, pa propušten auto-deload kasni najviše do njega. Lažan deload
odnosi celu nedelju treninga svakom petom vežbaču koji napreduje.

Posledica u bloku: prva nedelja nema čitanje, a druga ima samo jedno, pa snaga prvi put sme da
doprinese oceni u trećoj nedelji, kao i kod granica volumena.

## Provera

- `dotnet test`: **835** (bilo 828).
- Postojeći testovi koji zadaju pad zadaju ga sada za obe nedelje (pomoćne funkcije
  `Hypertrophy` i `Strength`), pa zadržavaju značenje. Bez toga su padala četiri, i sva četiri
  su zadavala pad bez prethodnog čitanja.
- Novi testovi: pad bez potvrde ne doprinosi (prethodna nedelja +1%, 0 i −0.5% nisu pad, −1% i
  −3% jesu); potvrđen pad nosi veličinu tekuće nedelje; test sa šumom drži ishode iz tabele
  (staro pravilo iznad 12% i 60%, novo ispod 3% i 12%, a pad od 3% na MRV-u iznad 70%).
- Merenje vraćanjem (commit, potvrda uvek tačna, rebuild): obara **4** od 835.
- **Spoj u servisu ne vidi nijedan test.** Uživo u oba smera, sa restartom API-ja između.
  Srednji nivo, Upper/Lower, ravan blok. Bench press u Upper A, ostali treninzi zatvoreni bez
  serija: 1. nedelja 100 kg × 10 sa RIR 1, 2. nedelja 94 kg × 11 sa RIR 0 (ista efektivna
  ponavljanja, pad od 6%, RIR jedan ispod cilja), 3. nedelja 88 kg × 11 sa RIR 0.

  | | Ocena posle 2. nedelje | 3. nedelja | Ocena posle 3. nedelje |
  |---|---|---|---|
  | Staro pravilo | **0.60** | automatski deload; planirani u 4. oslobođen | — |
  | Novo pravilo | **0.35** | trenažna | **0.60** (4. je već deload) |

## Ograničenja

- Ponašanje vežbača nije izmereno, nego su izmerena oba kraja. Stvaran vežbač je negde između,
  a pravilo je izabrano da bude ispravno i pod (A).
- Pod (B) RIR se ne pomera ni kad snaga stvarno pada, pa ocena umora takvom vežbaču retko
  pokreće deload i pre i posle ove izmene. Tu signal nose otkazi i serije ispod opsega.
