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

Simulacija (Python, `docs/simulations/fatigue_signal_noise.py`, 40.000 blokova po scenariju),
model šuma iz #94: nivo snage po nedelji ima sd 2.47%, a čitanje je razlika dve nedelje.
Rezultat zavisi od toga kako vežbač bira kad da stane, pa su izmerena oba krajnja slučaja:

- **(A)** drži zadat broj ponavljanja i iskreno prijavljuje rezervu, pa slab dan spušta RIR;
- **(B)** staje kad oseti ciljni RIR, pa slab dan spušta broj ponavljanja, a RIR se pomera
  samo pogrešnom procenom.

Pod (B) problema nema: lažan deload u 0% blokova na MAV-u i 0.2% na MRV-u.

Pod (A) broj zavisi i od toga koliko nedelja bloka ocena uopšte može da promeni. Čitanje snage
postoji od 2. nedelje, a ocena nedelje deluje na sledeću, pa ne pomaže ako je sledeća već
deload. U periodizovanom bloku od šest nedelja deluju ocene posle 2., 3. i 4. nedelje, a u ravnom
bloku od četiri samo ona posle 2. Udeo blokova sa deload-om iz ocene umora, srednji nivo
(prag 0.60):

| Blok | Pravilo | +1%/ned., MAV | +1%, MRV | −2%/ned., MAV | −3%, MRV |
|---|---|---|---|---|---|
| periodizovan, 6 nedelja | jedno čitanje (staro) | **12.5%** | **52.8%** | 53.2% | 98.5% |
| periodizovan, 6 nedelja | potvrda (novo) | **0.3%** | **3.0%** | 12.5% | 55.4% |
| ravan, 4 nedelje | jedno čitanje (staro) | 4.4% | 19.3% | 19.5% | 57.8% |
| ravan, 4 nedelje | potvrda (novo) | 0% | 0% | 0% | 0% |

Prve dve kolone su lažni deload (vežbač napreduje), a druge dve uhvaćen stvaran pad.

Simulacija pretpostavlja da svaka nedelja ima uporediv par serija. Runda 11 je izmerila da ga
ima oko dve trećine nedelja, a novo pravilo traži dva zaredom. Obe stope su zato u praksi niže
od tabele, i novo pravilo gubi više, jer mu trebaju dva para zaredom.

Izbor pravila je prvo napravljen na modelu bez strukture bloka (pet nedelja, svaka sa
čitanjem), gde su razlike veće: 20% → 0.5% na MAV-u i 73% → 6% na MRV-u. Te brojeve su
navodile prva verzija ove beleške i poruka commit-a. Pregled je pokazao da ih nijedan blok u
aplikaciji nema, pa tabela iznad koristi stvarnu strukturu.

Razmotrene varijante, na modelu bez strukture (pod (A), srednji nivo):

| Varijanta | +1%/ned., MAV | +1%, MRV | −2%/ned., MAV | −3%, MRV |
|---|---|---|---|---|
| jedno čitanje (staro) | 20.1% | 73.1% | 73.6% | 100% |
| oba čitanja ≥ 5% (manji pad od dva) | 0.0% | 3.1% | 3.2% | 72.0% |
| prosek dva čitanja | 0.1% | 11.8% | 14.5% | 98.9% |
| prosek dva čitanja, puna težina na 3% | 3.4% | 25.3% | 65.9% | 99.8% |
| prethodno čitanje negativno | 1.1% | 10.5% | 33.5% | 90.8% |
| **prethodno čitanje ≤ −1% (izabrano)** | **0.5%** | **6.0%** | **24.7%** | **83.3%** |

(Ova tabela je iz prve, neobjavljene verzije skripte, koja je imala čitanje i u 1. nedelji;
skripta u repozitorijumu daje strukturisane brojeve iznad.)

## Rešenje

**Pad snage ulazi u ocenu samo kad je i prethodna uporediva nedelja bila pad**
(`FatigueEvaluator.ConfirmedStrengthDrop`). „Pad" kod prethodne nedelje znači bar 1%
(`VolumeAdaptation.StrengthChangeThreshold`), isto kao kod granica volumena. Veličinu nosi
tekuća nedelja, bez praga, a prethodna samo potvrđuje smer; granice volumena, za razliku od
ovoga, traže da obe nedelje pređu prag. Dva čitanja dele nedelju između sebe, pa ih šum razilazi
po znaku, a stvaran pad ih slaže.

- `WeeklyFatigue` nosi i `PreviousE1RmChangeShare`: prethodnu trenažnu nedelju naspram one pre
  nje, nulu kad poređenja nema. Parametar je obavezan, jer bi podrazumevana nula tiho isključila
  član snage svakom pozivaocu koji ga zaboravi.
- `DeloadService` ga računa istim pravilom uporedive nedelje (`ComparableWeek`) kao i tekuće
  čitanje, iz serija uporedive nedelje koje je već učitao.

**Cena, rečena otvoreno:**

- Stvaran pad se hvata ređe i kasnije: pad od 3% nedeljno na MRV-u u 55% periodizovanih blokova
  umesto 98%, a pad od 2% na MAV-u u 12.5% umesto 53%. Planirani deload na kraju bloka ostaje, pa
  propušten auto-deload kasni najviše do njega.
- **U ravnom bloku od četiri nedelje pad snage više ne može da povuče deload uopšte.** Druga
  nedelja nema potvrdu (prva nema čitanje), a ocena treće deluje na četvrtu, koja je već deload.
  Otkazi, rezerva i volumen i dalje mogu.
- Potvrda važi za svaki par u kome je snaga, ne samo za RIR i snagu, iako je argument o
  zavisnosti napravljen samo za taj par. Nedelja u kojoj je svaka serija išla do otkaza, uz
  volumen na MRV-u, u drugoj nedelji bloka sada daje 0.40 umesto 0.65, pa vežbač koji se dva puta
  iscrpi deload dobija nedelju kasnije (vidi `independent-fatigue-signals.md`).
- **Napredni nivo (prag 0.50) od ovoga dobija malo.** Na MRV-u RIR (0.35) i volumen (0.15) sami
  dostižu 0.50, bez snage. Lažan deload vežbača koji napreduje na MRV-u: 71% → 65% periodizovanih
  blokova. To je zaseban problem (volumen na MRV-u je u nedeljama volumena propis, a ne zamor) i
  ovde nije rešavan.

Posledica u bloku: prva nedelja nema čitanje, a druga ima samo jedno, pa snaga prvi put sme da
doprinese oceni u trećoj nedelji, kao i kod granica volumena.

## Provera

- `dotnet test`: **835** (bilo 828), na kraju grane 838.
- Postojeći testovi koji zadaju pad zadaju ga sada za obe nedelje (pomoćne funkcije
  `Hypertrophy` i `Strength`), pa zadržavaju značenje. Bez toga su padala četiri, i sva četiri
  su zadavala pad bez prethodnog čitanja.
- Novi testovi: pad bez potvrde ne doprinosi (prethodna nedelja +1%, 0 i −0.5% nisu pad, −1% i
  −3% jesu); potvrđen pad nosi veličinu tekuće nedelje; test sa šumom drži ishode iz tabele
  na periodizovanom bloku od šest nedelja (staro pravilo iznad 8% i 40%, novo ispod 2% i 8%, a
  stvaran pad od 3% na MRV-u i dalje iznad 40%).
- Merenje vraćanjem (commit, potvrda uvek tačna, rebuild): obara **4** od 835.
- **Spoj u servisu ne vidi nijedan test.** Uživo u oba smera, sa restartom API-ja između.
  Srednji nivo, Upper/Lower, ravan blok. Bench press u Upper A, ostali treninzi zatvoreni bez
  serija: 1. nedelja 100 kg × 10 sa RIR 1, 2. nedelja 94 kg × 11 sa RIR 0 (ista efektivna
  ponavljanja, pad od 6%, RIR jedan ispod cilja), 3. nedelja 88 kg × 11 sa RIR 0.

  | | Ocena posle 2. nedelje | 3. nedelja | Ocena posle 3. nedelje |
  |---|---|---|---|
  | Staro pravilo | **0.60** | automatski deload; planirani u 4. oslobođen | — |
  | Novo pravilo | **0.35** | trenažna | **0.60** (4. je već deload) |

Druga i treća kolona žive tabele su i posledica iz „Cene": u ravnom bloku ocena posle 3. nedelje
nema na šta da deluje.

## Posle pregleda

Pregled (agent, radio na kopiji; radno stablo posle njega čisto) je potvrdio da servis čita prave
nedelje, da je svaki upit ograničen na korisnika, da je `DeloadService` jedino mesto koje gradi
ocenu, i ponovio i Python i C# brojeve. Ništa nije blokiralo, ali je beleška tvrdila više nego što
izmena radi u aplikaciji, i to je ispravljeno:

- brojevi su sada po stvarnoj strukturi bloka, a ne po modelu od pet nedelja sa čitanjem u
  svakoj (gore);
- nije pisalo da u ravnom bloku pad snage više ne može da povuče deload, ni da je napredni nivo
  od ovoga dobio malo, ni da je merenje „0.65 u drugoj nedelji" iz runde 11 time zastarelo; sada
  piše, i u `independent-fatigue-signals.md` i `auto-deload.md`;
- uputstvo je tvrdilo da nedelja sa svim otkazima pokreće deload uz pad snage **ili** volumen na
  MRV-u (to je bilo netačno i pre ove grane: 0.50 i 0.40, tek oba daju 0.65), a primer „ista
  takva nedelja posle još jednog pada ga pokreće" važi samo za hipertrofiju;
- `PreviousE1RmChangeShare` je obavezan, test sa šumom poredi staro pravilo tačno (prethodna
  nedelja koja uvek potvrđuje), a servis više ne učitava istu nedelju dvaput;
- skripta je u repozitorijumu (`docs/simulations/fatigue_signal_noise.py`).

Merenje vraćanjem na kraju (potvrda uvek tačna, rebuild): obara **4** od 838.

## Ograničenja

- Ponašanje vežbača nije izmereno, nego su izmerena oba kraja. Stvaran vežbač je negde između,
  a pravilo je izabrano da bude ispravno i pod (A).
- Pod (B) RIR se ne pomera ni kad snaga stvarno pada, pa ocena umora takvom vežbaču retko
  pokreće deload i pre i posle ove izmene. Tu signal nose otkazi i serije ispod opsega.
