# Šta je procena, a šta merenje

**Grana:** lokalno, na istoj grani kao korekcija po Epley-u i ocena umora.

Treća grana petnaestog kruga, nalazi G3 i G4 iz revizije posle runde 14. Po tvojoj odluci samo
komentar i uputstvo, bez izmene pravila.

## G3 — stimulativni volumen

`StimulativeVolume` broji seriju celu do RIR 3, upola na RIR 4, a od RIR 5 nimalo. Komentar je
to obrazlagao modelom „efektivnih ponavljanja" (tenzija samo u poslednjih nekoliko ponavljanja)
kao da je mehanizam izmeren.

Novija literatura je glatkija od te stepenice:

- Robinson i sar. (2024), meta-regresije bliskosti otkazu: rast mišića opada sa procenjenom
  rezervom postepeno, a ne stepenicom; snaga skoro ne zavisi od blizine otkazu.
- Refalo i sar. (2023), meta-analiza: uz izjednačen volumen, otkaz i serija blizu otkaza daju
  sličan rast. Serije bez otkaza u tim studijama su uglavnom bile blizu otkaza, pa o seriji sa
  RIR 4 ili 5 ne govore mnogo.

Pravilo ostaje. Priručnik eksplicitno kaže „minimalno RIR 4", odluka D1 je da priručnik važi
osim gde je literatura jasno na drugoj strani, a ovde je samo glatkija. Trenažne nedelje ne idu
preko RIR 3, pa se serija po planu tamo uvek broji cela. Deload bloka snage propisuje RIR 4, i
tamo se i serija po planu broji upola. Komentar u kodu i uputstvo sada to kažu. (Prva verzija je
tvrdila da stepenica pogađa samo serije lakše od plana; pregled je našao deload snage.)

## G4 — granice volumena

MEV, MAV i MRV su polazna procena iz priručnika (RP pristup) koju aplikacija uči, a uputstvo
ih je u odeljku „Analitika" predstavljalo kao činjenice („minimum ispod kog nema stimulusa",
„plafon iznad kog nema oporavka"). Te dve tačke sada kažu „procena", a ispod njih stoji zašto.
Pelland i sar. (2025), meta-regresije doze volumena: hipertrofija raste sa nedeljnim serijama
uz opadajući prinos, bez jasnog plafona u ispitanom opsegu, a snaga se zasiti mnogo ranije. Blok
snage već cilja ispod MAV-a, što se sa tim slaže. Uputstvo sada kaže da je MRV zaštita
oporavka, a ne granica preko koje rast prestaje. (Prva verzija je dodala samo citat ispod, a
tačke su i dalje govorile suprotno; pregled je to uhvatio.)

## Provera

- Nema izmene algoritma, pa nema ni novog testa algoritma.
- `dotnet test`: 835, bez izmene. `GuideTemplateTableTests` čita uputstvo i ostaje zelen.
- Klijent nije menjan.
