# Zatvaranje runde 14

**Grana:** `docs/round-14-closure`

Poslednja grana plana iz revizije nauke o treningu (odeljak F): nalazi F8 (ostatak), F18 i F19.
Kod se ne menja, osim dva teksta na ekranu.

## F8: „ispod MEV-a nema stimulusa"

MEV je procena iz priručnika, a ne prag ispod kog rasta nema (#95, [evidence-notes.md](evidence-notes.md)).
Dva teksta su to i dalje tvrdila:

- legenda na ekranu volumena: „ispod MEV-a nema stimulusa, iznad MRV-a nema oporavka". Sada:
  „ispod MEV-a je rast mali, iznad MRV-a oporavak ne stiže";
- napomena dvodnevnog šablona: „ostaju ispod minimalnog volumena za rast". Sada: „ostaju ispod
  minimalnog efektivnog volumena, pa je rast mali".

Test `TwoDayTemplate_SitsBelowMevByDesign` ostaje: on proverava da je napomena tu, a ne njen
tekst.

## F18: ocena umora je heuristika

Uputstvo je opisivalo težine signala (0.35 / 0.25 / 0.25 / 0.15) i pragove (0.60 i 0.50)
kao da su utvrđeni. Ne potiču iz literature: nijedno istraživanje ne daje formulu po kojoj se ti
signali sabiraju u odluku o deload-u. Uputstvo sada ima pasus „Ocena umora je pravilo ovog
sistema, ne merenje", sa uputom na simulaciju (`docs/simulations/fatigue_signal_noise.py`).

Usput je ispravljena jedna zastarela rečenica: „padom od bar 1% (ista mera kao kod granica
volumena)". Od #97 granice volumena sude po proseku dve nedelje, uz prag od 2.5%, pa mera nije
ista.

## F19: teza

[`docs/teza-uskladjivanje.md`](../teza-uskladjivanje.md) skuplja svako mesto u tezi v2.1 koje
više ne opisuje aplikaciju: četrnaest odeljaka, od trajanja bloka i koraka opterećenja do
listinga, zaključka, ostalih mesta i literature. Za svako je dat navod iz teze (proveren nad tekstom dokumenta), šta
aplikacija radi danas sa PR-om, i predlog novog teksta na ćirilici. Pet rečenica iz #93 je
uključeno. Tezu menja vlasnik.

Dva navoda iz prvobitnog spiska F19 su ispravljena pri proveri:

- registracija ima sedam pitanja i osmi ekran za maksimume, a ne „osam pitanja";
- zaključak teze ima ograničenja u jednom pasusu (¶575) i pravce razvoja u sledećem (¶576), a ne
  „pet ograničenja koja su rešena još u rundi 1". Četiri jesu rešena u rundi 1. Peto, da je RIR
  „kod početnika nepouzdan", ne slaže se sa Halperin i sar. 2022.

## Posle pregleda

Pregled je proverio svaki navod skriptom nad tekstom `.docx`; svi su doslovni na navedenom
mestu osim jednog (šabloni u ¶557, sada ispravljen). Ispravljeno je i:

- „Svih pet ograničenja je rešeno" u istom odeljku u kome tabela kaže da peto nije;
- „više od sto PR-ova": spojenih je nešto više od osamdeset;
- predlozi teksta za deload su važili i za početnika, koji automatski deload nema, a ravan blok
  ni planirani;
- uputstvo i predlog su govorili o „šumu iz literature", a deo šuma je pretpostavka modela;
- dodat je odeljak „Ostala mesta": preskakanje treninga, model podataka, završetak treninga,
  opsezi ponavljanja i stranica mezociklusa, kao i navodi iz uvoda, 4.4, slika 7.6/7.7, zaključka
  i teksta uz listing 6.5;
- dve gramatičke greške u predlozima, korekcija po RIR-u opisana po propisu (a ne po
  ponavljanjima sesije), „пол (опционо)" i test-serija kao jedini unos za vežbu sa sopstvenom
  masom.

## Provera

- `dotnet test` i `npm test` prolaze, bez novih testova: menja se samo tekst.
- Uživo, ekran volumena na 375 px: legenda glasi „ispod MEV-a je rast mali, iznad MRV-a oporavak
  ne stiže".
