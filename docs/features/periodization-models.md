# Nedelje unutar bloka više nisu iste

**Grana:** `feature/periodization-models`

Četvrta izmena izvedena iz priručnika (vidi [analizu](../analiza-prirucnika.md), stavka 3).

## Problem

Mezociklus je imao četiri nedelje sa **identičnim propisom**: isti broj serija, isti
rep-opseg, isti ciljni RIR. Jedina razlika bio je deload u četvrtoj. Priručnik na to ima
direktan odgovor:

> *"Ne možeš isto trenirati svake nedelje i očekivati da napreduješ — telo se prilagodi.
> Zato se kroz blok menja odnos volumena i intenziteta."*

Napredak je nosila isključivo dupla progresija (prvo ponavljanja, pa opterećenje) unutar
istog propisa. To je legitimno, ali je **jedan** način vođenja bloka, a aplikacija nije
nudila nijedan drugi. Blok koji vodi ka snazi i blok koji vodi ka hipertrofiji izgledali su
identično.

## Rešenje

### Tri modela

| Model | Nedelja | Kako teče |
|---|---|---|
| **Ravan** | 4 | Isti propis svake nedelje, deload na kraju. |
| **Linearan** | 6 | Volumen → intenzitet: počinje sa više ponavljanja i lakšim serijama, završava sa manje ponavljanja i bliže otkazu. |
| **Obrnut** | 6 | Intenzitet → volumen: teško dok si svež, volumen pred kraj. |

**Ravan je podrazumevan i identičan je ponašanju koje je sistem imao ranije.** To nije
kompromis nego namera: zatečeni korisnici ne smeju da dobiju drugačiji plan zato što je
dodata mogućnost koju nisu tražili. Test to izričito zaključava.

Trajanje bloka **zavisi od modela** — periodizovan blok traži šest nedelja da bi uopšte
imao mesta da pomeri odnos volumena i intenziteta. Četiri nedelje nisu dovoljne za dve faze
plus prelaz plus deload.

### Propis se izražava kao pomeraj, ne kao apsolutni broj

Ovo je ono što čini da jedan raspored radi za oba cilja. Linearni blok za hipertrofiju
(osnova 8–12, RIR 1, 4 serije) i za snagu (3–6, RIR 2) izgledaju ovako:

| Nedelja | Hipertrofija | Snaga |
|---|---|---|
| 1 | 5 × 11–15, RIR 2 | 5 × 6–9, RIR 3 |
| 2 | 5 × 11–15, RIR 1 | 5 × 6–9, RIR 2 |
| 3 | 4 × 8–12, RIR 1 | 4 × 3–6, RIR 2 |
| 4 | 4 × 8–12, RIR 0 | 4 × 3–6, RIR 1 |
| 5 | 3 × 6–10, RIR 0 | 3 × 3–4, RIR 1 |
| 6 | 2 × 8–12, RIR 1 (deload) | 2 × 3–6, RIR 2 (deload) |

Donja granica je tri ponavljanja. Blok snage već stoji na 3–6, pa fazu intenziteta kod
njega nosi **RIR, a ne još kraći opseg** — ispod tri ponavljanja to prestaje da bude isti
trenažni zadatak. Test pokriva i to.

RIR opada kroz blok kod oba periodizovana modela: zamor raste, pa se serije vode sve bliže
otkazu — do deload-a, koji vraća polazni RIR i polovi serije.

### Opterećenje se preračunava kada se propis promeni

Ovo je bio pravi posao ove grane. Progresija je do sada uzimala težinu iz prethodne nedelje
i dodavala korak. Kada naredna nedelja traži **drugačiji broj ponavljanja**, ta težina više
ne znači ništa — nedelja koja pada sa 12 na 8 ponavljanja mora da bude teža, a ne ista plus
2.5 kg.

Zato završetak treninga sada bira između tri slučaja:

1. **Deload** — 90% *stvarno* korišćene težine, bez progresije (nepromenjeno).
2. **Naredna nedelja ima drugačiji propis** — opterećenje se izvodi iz najsvežije procene
   1RM-a i propisa te nedelje, istom formulom kojom se računa i prva nedelja.
3. **Isti propis** — obična dupla progresija (nepromenjeno).

Kod ravnog bloka je uvek slučaj 3 ili 1, pa se ponašanje ne menja ni u jednom koraku.

### Model se bira po bloku dugoročnog plana

Dugoročan plan i dobija smisao time što se raspored menja između blokova, pa je model
pojedinačan po bloku, a ne po planu. Predlog sa servera sada smenjuje i cilj i model: blok
hipertrofije dobija obrnuti raspored, blok snage linearni.

### Šta je još moralo da se pomeri

Promenljiva dužina bloka dodiruje više mesta nego što se čini:

- **Analitika** je imala zakucan izbor nedelja `[1, 2, 3, 4]`. Sada se izvodi iz samog
  mezociklusa, uz ograničavanje izabrane nedelje — prelazak sa šestonedeljnog na
  četvoronedeljni blok ne sme da ostavi izbor na nedelji koje tamo nema.
- **Ekran „Plan"** je računao trajanje kao `broj blokova × 4`. Sada sabira stvarna trajanja
  blokova, koja server šalje uz svaki blok.
- **Planirani deload** je bio „poslednja nedelja od četiri". Sve provere oko deload-a već
  su radile sa relativnim brojevima nedelja, pa su prošle bez izmene — što je i provereno.

## Provera

- `dotnet build`, `dotnet test` (**204 testa**, bilo 182), `npm run build` — sve prolazi.
- Migracija `AddPeriodizationModel` napravljena **i primenjena** na lokalnu bazu. Zatečeni
  redovi dobijaju `Flat`, što odgovara njihovom stvarnom sadržaju — bez dopune podataka.
- End-to-end kroz pokrenutu aplikaciju, ista vežba i isti 1RM (čučanj 140 kg):

  | Model | Nedelja | Prva nedelja |
  |---|---|---|
  | Ravan | 4 | 4 × 8–12, RIR 1 @ 107.5 kg |
  | Linearan | 6 | 5 × 11–15, RIR 2 @ 97.5 kg |
  | Obrnut | 6 | 3 × 6–10, RIR 2 @ 110.0 kg |

  Ista snaga, tri različita polazna opterećenja — jer se izvode iz propisa te nedelje.

- End-to-end preračun: u linearnom bloku nedelja 1 (RIR 2) → nedelja 2 (RIR 1) podigla je
  cilj sa 97.5 na 100.0 kg, iako je korisnik odradio tačno po planu.
- **Regresija ravnog bloka:** iste serije na gornjoj granici opsega → dupla progresija
  podiže težinu za 2.5 kg, propis ostaje isti. Ponašanje nepromenjeno.
- End-to-end dugoročan plan sa tri bloka i tri različita modela: 6 + 6 + 4 = 16 nedelja,
  prvi blok generisan sa svojim modelom.
- U pregledaču: čarobnjak nudi izbor rasporeda sa objašnjenjem i tačnim trajanjem, plan od
  6 nedelja se pravi i prikazuje, analitika prikazuje 6 nedelja i vraća izbor na 4 pri
  prelasku na kraći blok, a čarobnjak dugoročnog plana ima model po bloku i ukupno trajanje
  koje se menja sa izborom (12 → 10 nedelja).

## Nađeno usput

Kontroler za `POST /mesocycles` je pravio dugoročan plan sa jednim blokom i pri tome
**gubio izabrani model** — svaki zahtev bi završio kao ravan blok. Uhvaćeno prvim
end-to-end prolazom, jer su sva tri modela vratila isti četvoronedeljni raspored.

## Poznata ograničenja

- **Zatečeni blokovi ostaju ravni.** Model se ne može promeniti na već generisanom
  mezociklusu; bira se pri pravljenju. Menjanje usred bloka bi prepisalo propis nedelja
  koje korisnik možda već delimično radi.
- **Deload zadržava rep-opseg cilja** i kod periodizovanih modela. Rasterećenje nosi
  opterećenje (90%) i polovinu serija; menjanje i opsega bi promenilo sam trenažni zadatak,
  a ne samo njegovu težinu.
- **Automatski deload i dalje troši planirani.** Blok nosi jedno rasterećenje bez obzira na
  dužinu, pa šestonedeljni blok koji rano uđe u auto-deload završava sa pet trenažnih
  nedelja i jednim deload-om — isto pravilo kao i ranije, samo na dužem bloku.
