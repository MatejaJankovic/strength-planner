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
| 1 | 5 × 11–12, RIR 2 | 5 × 6–9, RIR 3 |
| 2 | 5 × 11–12, RIR 1 | 5 × 6–9, RIR 2 |
| 3 | 4 × 8–12, RIR 1 | 4 × 3–6, RIR 2 |
| 4 | 4 × 7–11, RIR 1 | 4 × 3–5, RIR 1 |
| 5 | 3 × 6–10, RIR 1 | 3 × 3–4, RIR 1 |
| 6 | 2 × 8–12, RIR 1 (deload) | 2 × 3–6, RIR 2 (deload) |

Dve granice su tvrde i obe postoje zbog sistema oko njih, ne zbog trenažne teorije:

- **Gornja granica ponavljanja je Epley granica (12).** Serija iznad nje ne daje procenu
  1RM-a, a tu procenu čitaju tri stvari: trend snage, prepoznavanje rekorda i član ocene
  umora. Nedelja volumena propisana na 11–15 izgledala bi uredno, a sistem bi u njoj
  prestao da meri.
- **Najniži ciljni RIR je 1, ne 0.** Umor se meri kao *manjak* u odnosu na cilj, a ispod
  nule manjka nema — ponavljanja u rezervi ne idu u minus. Nedelja propisana do otkaza tiho
  bi izgubila najteži član ocene umora (0.35) i nikada ne bi mogla da pokrene raniji deload.

Donja granica je tri ponavljanja. Blok snage već stoji na 3–6, pa fazu intenziteta kod
njega nosi **RIR, a ne još kraći opseg**. Kod hipertrofije je obrnuto: osnovni RIR je već 1,
pa tamo intenzitet nose ponavljanja i serije. Oba slučaja su pokrivena testovima.

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
- **Deload je morao da nauči za propis.** Provere oko deload-a rade sa relativnim brojevima
  nedelja, pa je *raspored* prošao bez izmene — ali *sadržaj* nije. Vidi „Šta je revizija
  našla" ispod; ovo je bila najozbiljnija greška u grani.

## Provera

- `dotnet build`, `dotnet test` (**208 testova**, bilo 182), `npm run build` — sve prolazi.
- Migracija `AddPeriodizationModel` napravljena **i primenjena** na lokalnu bazu. Zatečeni
  redovi dobijaju `Flat`, što odgovara njihovom stvarnom sadržaju — bez dopune podataka.
- End-to-end kroz pokrenutu aplikaciju, ista vežba i isti 1RM (čučanj 140 kg):

  | Model | Nedelja | Prva nedelja |
  |---|---|---|
  | Ravan | 4 | 4 × 8–12, RIR 1 @ 107.5 kg |
  | Linearan | 6 | 5 × 11–12, RIR 2 @ 97.5 kg |
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
- **Auto-deload usred periodizovanog bloka, odigran nad bazom.** Nedelja 1 linearnog bloka
  odrađena kroz otkaz → nedelja 2 (bila 5 × 11–12) postaje deload sa **2 × 8–12 RIR 1**, a
  nedelja 6 se oslobađa i preuzima propis žrtvovane nedelje: **5 × 11–12 RIR 1**. Blok i
  dalje ima tačno jedno rasterećenje.
- **Ista provera na ravnom bloku:** nedelja 2 → 2 × 8–12 RIR 1, nedelja 4 oslobođena kao
  4 × 8–12 RIR 1. Ponašanje nepromenjeno u odnosu na zatečeno.

## Nađeno usput

Kontroler za `POST /mesocycles` je pravio dugoročan plan sa jednim blokom i pri tome
**gubio izabrani model** — svaki zahtev bi završio kao ravan blok. Uhvaćeno prvim
end-to-end prolazom, jer su sva tri modela vratila isti četvoronedeljni raspored.

## Šta je revizija koda našla

Dve greške su bile ozbiljne i obe su iz istog korena: deload logika je pisana kad su sve
nedelje bloka imale **isti** propis, pa je taj propis prećutno smatrala datim.

### Auto-deload je zadržavao propis faze — deload do otkaza

`ApplyDeloadAsync` je polovila serije i spuštala opterećenje, ali **nije dirala rep-opseg
ni RIR**. Na ravnom bloku to nije smetalo jer su već bili ciljni. Na periodizovanom je
značilo da nedelja faze intenziteta, pretvorena u „rasterećenje", zadrži svoj RIR. Uz
prvobitni raspored (koji je išao do RIR 0) to je bio deload sa serijama do otkaza.

Sada deload uvek dobija rep-opseg i RIR **cilja**, a serije se polove u odnosu na polazni
broj bloka — ne u odnosu na ono što ta nedelja nosi, jer bi nedelja volumena (serija više)
dala preobiman deload.

### Oslobođena nedelja je dobijala propis koji ne postoji ni u jednom modelu

Kada umor povuče deload ranije, planirani deload na kraju se vraća u trenažnu nedelju.
Broj serija se uzimao kao `Max` preko ostalih nedelja — što je na ravnom bloku tačno, a na
periodizovanom uvek bira fazu volumena. Rep-opseg i RIR se nisu vraćali uopšte, pa su
ostajali na deload vrednostima. Rezultat: najveći volumen u bloku spojen sa osnovnim
intenzitetom, i to kao poslednja nedelja bloka koji bi trebalo da *završi* intenzitetom.

Sada oslobođena nedelja preuzima **ceo propis one nedelje koja je žrtvovana za deload** —
taj deo bloka nije izgubljen nego pomeren za nedelju dana. Polazni broj serija se izvodi iz
zatečenog plana, a ne iz profila: korisnik koji je usred bloka promenio nivo iskustva ne
sme time da promeni oblik već napravljenog plana.

### Ostalo

- **Opterećenje se izvodilo iz rekorda bez vremenskog prozora.** Generator namerno gleda
  samo skorašnjih 56 dana; preračun nije, pa je rekord od pre pola godine mogao da postane
  ciljno opterećenje naredne nedelje. Sada koristi isti prozor.
- Model periodizacije se **nije proveravao** pri upisu; nepoznata vrednost bi se ponašala
  kao ravan blok, ali bi se upisala u bazu i vraćala klijentu.
- Promena cilja bloka u čarobnjaku nije povlačila model, iako ga dodavanje bloka i predlog
  sa servera povlače.

## Poznata ograničenja

- **Zatečeni blokovi ostaju ravni.** Model se ne može promeniti na već generisanom
  mezociklusu; bira se pri pravljenju. Menjanje usred bloka bi prepisalo propis nedelja
  koje korisnik možda već delimično radi.
- **Deload zadržava rep-opseg cilja** i kod periodizovanih modela. Rasterećenje nosi
  opterećenje (90%) i polovinu serija; menjanje i opsega bi promenilo sam trenažni zadatak,
  a ne samo njegovu težinu.
- **Automatski deload i dalje troši planirani.** Blok nosi jedno rasterećenje bez obzira na
  dužinu, pa šestonedeljni blok koji rano uđe u auto-deload završava sa pet trenažnih
  nedelja i jednim deload-om — isto pravilo kao i ranije, samo na dužem bloku. Redosled faza
  se pri tome pomera: žrtvovana nedelja se odrađuje na kraju, posle onih koje su je u
  originalnom rasporedu sledile.
- **Kod hipertrofije RIR ne opada kroz blok.** Osnovni RIR je 1, a niže se ne ide (vidi
  gore), pa intenzifikaciju nose ponavljanja i serije. Kod snage (osnovni RIR 2) pad
  postoji.
