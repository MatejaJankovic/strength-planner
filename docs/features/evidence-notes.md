# Šta je procena, a šta merenje

**Grana:** lokalno, na istoj grani kao korekcija po Epley-u i ocena umora.

Treća grana petnaestog kruga, nalazi G3 i G4 iz revizije posle runde 14. Po tvojoj odluci samo
komentar i uputstvo, bez izmene pravila.

## G3 — stimulativni volumen

`StimulativeVolume` broji seriju celu do RIR 3, upola na RIR 4, a od RIR 5 nimalo. Komentar je
to obrazlagao modelom „efektivnih ponavljanja" (tenzija samo u poslednjih nekoliko ponavljanja)
kao da je mehanizam izmeren.

Novija literatura je glatkija od te stepenice:

- Robinson i sar. (2024), meta-regresije bliskosti otkazu: rast mišića opada sa rezervom
  postepeno, a serija sa umerenom rezervom nije bez efekta; snaga skoro ne zavisi od blizine
  otkazu.
- Refalo i sar. (2023), meta-analiza: uz isti volumen, otkaz i serija pre otkaza daju sličan
  rast.

Pravilo ostaje. Priručnik eksplicitno kaže „minimalno RIR 4", odluka D1 je da priručnik važi
osim gde je literatura jasno na drugoj strani, a ovde je samo glatkija. Planirane nedelje ne
idu preko RIR 4 (`Periodization.MaxRir`), pa stepenica pogađa samo serije koje su ispale lakše
od plana. Komentar u kodu i uputstvo sada to kažu.

## G4 — granice volumena

MEV, MAV i MRV su polazna procena iz priručnika (RP pristup) koju aplikacija uči, a uputstvo
ih je u odeljku „Analitika" predstavljalo kao činjenice („minimum ispod kog nema stimulusa", „plafon iznad
kog nema oporavka"). Pelland i sar. (2025), meta-regresije doze volumena: hipertrofija raste sa
nedeljnim serijama uz opadajući prinos, bez jasnog plafona u ispitanom opsegu, a snaga se
zasiti mnogo ranije. Blok snage već cilja ispod MAV-a, što se sa tim slaže. Uputstvo sada kaže
da je MRV zaštita oporavka, a ne granica preko koje rast prestaje.

## Provera

- Nema izmene algoritma, pa nema ni novog testa algoritma.
- `dotnet test`: 835, bez izmene. `GuideTemplateTableTests` čita uputstvo i ostaje zelen.
- Klijent nije menjan.
