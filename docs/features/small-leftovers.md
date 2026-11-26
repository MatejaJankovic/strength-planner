# Dve sitne ispravke iz starijih rundi

Posle runde 20 ostale su dve stvari koje su zapisane kao poznate, ne kao rešene.

## Završetak treninga koji ne traje

`ClaimForCompletionAsync` je proveravao samo „nije već završen", pa je izravan poziv API-ja završavao i **planiran**
trening (bez serija, pa bi iz ničega upisao ocenu umora nedelje, granice i cilj sledeće nedelje) i **preskočen**
(odgovor vežbača bi se pretvorio u odrađen trening mimo ekrana, koji nijedno ne nudi). Sada se završava samo trening u
toku (`InProgress`), i provera i uslovni `UPDATE` to kažu; ostalo je 409.

- Test: planiran i preskočen odbijeni, ništa nije upisano (status, procena maksimuma, ocena umora).
- Uživo: `POST /complete` na planiran trening daje 409, a status ostaje `Planned`.
- Nijedan od 1824 postojeća testa nije pao, što znači da nijedan nije oslanjao na staro ponašanje.
- Vraćanje na committed stablu: brza provera sama ili uslovni `UPDATE` sam ne ruše ništa (jedno čuva drugo, namerno:
  `UPDATE` je atomski, a brza provera daje poruku), oba vraćena zajedno ruše 2 testa. Prvi pokušaj je dao 0 za sva tri
  jer je API u pregledu držao DLL, pa se gradnja tiho nije izvršila i testovi su pokrenuti nad starim binarnim kodom;
  ponovljeno sa zaustavljenim API-jem.

## Engleski naslovi grešaka na ekranu

Server u produkciji šalje samo opšti naslov (`Detail` je isti kao `Title`), a `extractErrorMessage` ga je prikazivao:
„Request conflicts with current state." posle preskakanja ili zamene vežbe. Naslovi iz `Program.cs` (osam) sada imaju
srpsku poruku u `GENERIC_TITLES` (bez prevoda pada 7 testova); naslov koji nije na spisku prolazi kakav jeste, a poruka i spisak grešaka i dalje imaju
prednost. Izmena je na jednom mestu, pa važi za svih 65 poziva. `blockEditError` ostaje: njegova poruka kaže više od
statusa.

## Ostalo

- Test nalog u razvojnoj bazi: obrisan plan „Održavanje live", raniji plan je opet aktivan.
- `training-history.md` je imao pogrešnu početnu tačku broja testova (1327); izmereno na commit-u #118 je 1335.
- `docs/deployment-security.md`: dodat korak da se migracije pre prve prave isporuke izvrše nad kopijom stvarne baze.
