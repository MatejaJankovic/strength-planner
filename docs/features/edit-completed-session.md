# Ispravka završenog treninga

Runda 18, grana 2 od 4. Do sada je pogrešno upisana serija u završenom treningu značila
brisanje celog plana: uputstvo je to tako i govorilo.

## Pravilo

Završen trening se ispravlja dok ništa nije izgrađeno na njegovim serijama (domen:
`SessionCorrection`, čist kod sa testovima):

- Trening istog dana u najbližoj kasnijoj nedelji **nije počeo** (planiran ili preskočen). Kad
  je počeo, njegov cilj je izračunat iz ovih serija.
- Za trening u poslednjoj nedelji bloka, koji nema kome da preda cilj: sledeći blok **nije
  generisan**. Blok kreće od procena, pa ih generisanje troši.

Ispravka ima dva režima. **Preračun** (trening završen posle ove promene) čuva procenu, cilj
sledeće nedelje, ocenu umora nedelje i pomeraj granica volumena iznova. **Samo istorija**
(trening završen pre nje): menja se zapis serija i ono što se iz njega čita (istorija,
analitika), ništa se ne preračunava. Tako je vlasnik odlučio; u razvojnoj bazi je to 425
treninga.

## Kako radi

Serije se menjaju postojećim krajnjim tačkama. Za završen trening one proveravaju prozor i
samo označe da ispravka čeka (`WorkoutSession.RevisionPending`). `POST /api/sessions/{id}/revise`
je sama ispravka: uzima nazad ono što je završetak upisao i piše ga iznova iz ispravljenih
serija, kroz iste korake kojima se trening završava (`SessionOutcomes` iz #117 i
`WeekConsequences`, koji je sada izdvojen iz `SessionService`). Idempotentna je: bez izmena
upisuje iste vrednosti.

Šta je trebalo da se veže za trening, a nije bilo:

| Šta | Kako |
|---|---|
| procena maksimuma | `OneRepMaxRecord.WorkoutSessionId` (nullable, `SET NULL`): ispravka procenu **zameni**, ne dodaje drugu |
| trenutak završetka | `WorkoutSession.CompletedAt`; prazno znači „završen pre ove promene" |
| ocena umora | već je na nedelji; ispravka je poništi (`null`) i nedelja se ocenjuje iznova |
| pomeraj granica volumena | nov `VolumeLandmarkChange` (nedelja, mišić, razlika po MEV/MAV/MRV) |

**Granice volumena.** `UserVolumeLandmark` pamti samo trenutne vrednosti, pa nedelja nije mogla
da se „odradi unazad". Svaka obračunata nedelja sada zapisuje koliko je pomerila, a
`VolumeAdaptation.Undo` to oduzima i vraća rezultat kroz isto normalizovanje kojim se završava
`Adjust` (blizina seed-u, širina pojasa, MAV unutar pojasa). Ako se granice vrate na seed, red
se briše: ne treba red koji samo ponavlja seed.

## Šta nije tačan inverz

Nedelje obračunate posle ispravljene su se oslanjale na granice koje su već sadržale njen
pomeraj, a neke njihove pomeraje su zadržala ista ograničenja (lutanje oko seed-a, širina
pojasa). Zato je poništavanje tačno za poslednju obračunatu nedelju (test prolazi na 180
tipova nedelja i 4 početne tačke), a za ranije daje uvek **dozvoljen** pojas, ne nužno isti
broj. Prozor to ublažava: ispravka je moguća samo dok sledeći trening istog dana nije počeo, a
tada nedelja posle ispravljene ne može biti zatvorena.

Poznata ivica: „Vrati podrazumevane granice" briše i pomeraje nedelja (oni se odnose na granice
koje više ne postoje). Trening ispravljen posle toga računa svoju nedelju iznova nad seed-om, pa
joj granice pomeri jednom.

## Deload

- Ocena umora se računa iznova. Ako je **nova ocena iznad praga**, a nedelja posle nije počela i
  blok još nema deload, uvodi se deload kao da je tako bilo od početka. Odgovor to nosi.
- Ako je deload **već povučen**, ostaje, čak i kad ispravljene serije ne bi dale toliki umor:
  sledeća nedelja je prepisana i vežbač je mogao da je vidi. Odgovor nosi `deloadKept`, a ekran
  to kaže. Dva testa pokrivaju oba smera.

## Ekran

Završen trening dobija „Ispravi trening", koji otključava unos kao u treningu koji traje.
Izmene se upisuju odmah, a dugme „Sačuvaj ispravku" (ili „Gotovo" za istoriju) zatvara režim.
Posle čuvanja se isti rezime prikazuje pod naslovom „Ispravka sačuvana", uz promenu ocene umora
i, ako treba, napomenu o deload-u. Zatvoren prozor kaže zašto je zatvoren. Nesačuvane izmene
preživljavaju osvežavanje ekrana (server pamti oznaku). Poruke su u `correction-note.ts` uz spec,
jer zavise od tri stanja koja se lako pomešaju.

Uputstvo je ispravljeno na dva mesta koja su tvrdila da se završen trening ne menja, i dobilo
odeljak „Ispravka završenog treninga".

## Merenja

- Testovi: 939 → 1327 na serveru (većina su slučajevi teorije za `Undo`: 180 tipova nedelja × 2
  provere), 213 → 223 na klijentu.
- Vraćanje pravila na committed stablu (pre dopune testova za deload): granice se ne poništavaju
  1 od 1325 pada; procena se ne uklanja 2; ocena se ne poništava 1; prozor se ignoriše pri
  izmeni serije 1; pomeraj se ne beleži 2. Posle dopune testova za deload: oznaka „deload ostaje" uvek `false` ruši
  1, a ispravka koja ne proverava deload ruši 3.
- **Dva moja testa za deload su pala iz pogrešnih razloga**, pa su ispravljena umesto da se
  tvrdnja oslabi. Nedelja sa dvadeset otkaza nije prešla prag od 0,60, jer je imala jedan jak
  signal, a prag traži dva. A serija sa RIR 0 koja je bila upisana direktno u bazu prošla je kao
  „nije otkaz", dok je ista serija ispravljena preko servisa postala otkaz
  (`ImpliesFailure`), pa je ispravljena teška nedelja izgubila signal rezerve. Servis je u
  pravu; test sada ne koristi RIR 0 za seriju koja nije otkaz.
- Uživo, 375 px: drugi trening nedelje 1 završen kroz API, pa ispravljen kroz ekran (prva
  serija Overhead Press sa 12 na 8 ponavljanja). „Sledeće" za Overhead Press je bilo
  47,5 kg (rast) i posle ispravke je 45 kg; cilj naredne nedelje u bazi je 45; procena je
  zamenjena (2 zapisa za trening pre i posle, ne 4); `RevisionPending` je opet `false`.
  Stari trening iste nedelje pokazuje poruku za istoriju i nema „Sačuvaj ispravku".
  `scrollWidth` 375 = `clientWidth` 375.

## Šta nije pokriveno

- Dva istovremena zahteva za čuvanje ispravke nisu probana; sve je u jednoj transakciji, ali
  probni sklop ne pravi konkurentne zahteve.
- `RevisionPending` se postavlja i kad serija nije stvarno promenjena (npr. izmena istim
  vrednostima): sledeće čuvanje tada upiše iste vrednosti, što je bezopasno.
- Ispravka serije u treningu koji je u međuvremenu zamenjen (`Zameni vežbu`) nije posebno
  proveravana; zamena je moguća samo na vežbi bez serija, pa se dva ne preklapaju.
