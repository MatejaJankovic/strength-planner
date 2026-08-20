# Ko odlučuje o broju serija

Prijavljeno iz stvarne upotrebe: „uneo sam svoj šablon, a prilikom početka treninga za prvu
nedelju serije i ponavljanja su izmenjeni". Očekivanje je bilo jasno — ako sam uneo opsege
koje želim, aplikacija ne bi trebalo da ih dira dok ne napredujem.

Merenje je pokazalo da to nije jedna stvar nego dve, i da samo jedna od njih jeste
periodizacija.

## Šta se stvarno dešavalo

Šablon: **3 serije, 8–12 ponavljanja.**

Ravan model:

| Nedelja | Serije | Ponavljanja |
|---|---|---|
| 1–3 | **5** | 8–12 |
| 4 (deload) | 2 | 8–12 |

Linearan model:

| Nedelja | Serije | Ponavljanja | RIR |
|---|---|---|---|
| 1 | 6 | **11–12** | 3 |
| 2 | 6 | 11–12 | 2 |
| 3 | 5 | **8–12** | 2 |
| 4 | 5 | 7–11 | 1 |
| 5 | 4 | 6–10 | 1 |
| 6 (deload) | 2 | 8–12 | 2 |

**Ponavljanja** menja samo periodizacija, i to po definiciji. Uneti opseg nije propis za
prvu nedelju nego **sidro**, a nedelje su pomaci od njega — kod linearnog sidro pada na
treću nedelju. Da je prva nedelja jednaka trećoj, model ne bi bio linearan. Ravan model
ponavljanja uopšte ne dira.

**Serije** je menjalo nešto drugo: MAV alokator iz runde 5, koji se pokreće odmah po
generisanju plana (`MesocycleGenerator`) i bira serije tako da nedelja padne u ciljnu zonu
volumena svakog mišića. To radi u **svakom** modelu, pa i u ravnom — zato je 3 postalo 5 i
onda kada periodizacija ništa ne pomera.

Za ugrađene šablone je to ispravno: oni nose raspored vežbi, ne nameru o volumenu. Kod
ličnog šablona nije — otkucano „3 serije" je namera, a menjala se tiho.

## Šta je promenjeno

**Broj serija je sada izbor po bloku**, uz model periodizacije:

- *Prilagodi ciljnom volumenu* — zatečeno ponašanje, i dalje podrazumevano.
- *Prati moj šablon* — ostaje tačno onoliko serija koliko je uneto. Nedeljni volumen tada
  može da ostane ispod ciljne zone i sistem ga neće ispravljati; to je i smisao izbora.

Bira se po bloku, a ne po nalogu, jer isti plan sme da ima blok koji prati lični šablon i
blok koji cilja volumen — isto kao što se model periodizacije bira po bloku.

**Modeli sada pišu šta rade unetim brojevima.** Uz svaki izbor stoji rečenica: ravan model
zadržava opseg svake nedelje, linearan kreće sa više ponavljanja i uneti opseg donosi u
trećoj nedelji, obrnuti kreće sa manje. Algoritam se nije menjao — menjalo se to što nigde
nije pisalo.

## Odluke koje su oblikovale izvedbu

**Zatečeno ponašanje je nulta vrednost enuma.** Migracija dodaje kolonu sa podrazumevanom
nulom, pa svaki plan napravljen pre ovog izbora nastavlja da cilja volumen. Da je
`FollowTemplate` nula, svi zatečeni blokovi bi tiho prestali da se balansiraju.
`SetAllocationChoiceTests` to drži.

**Izbor stoji i na bloku i na mezociklusu.** Blok ga pamti jer se generiše tek kada dođe na
red — mesecima posle pravljenja plana. Mezociklus ga pamti jer se serije **ponovo**
balansiraju posle svakog završenog treninga (`SessionService.CompleteAsync`), a tamo se
blok ne čita. Da izbor živi samo na bloku, važio bi tačno do prvog odrađenog treninga.
Izmereno: sa gašenjem samo u generatoru, prvi završen trening vraća nedelje 2 i 3 na pet
serija.

**Deload i dalje prepolovljava serije u oba izbora.** To radi `Periodization.DeloadSets`, ne
alokator — rasterećenje jeste smisao te nedelje, i nije stvar volumenske raspodele.

**Test proverava odrazom da zahtev za generisanje nosi svaki izbor koji blok pamti.**
Nov izbor dodat samo na blok se kompajlira bez greške i tiho pada na podrazumevanu
vrednost kad blok dođe na red. Provereno da test radi: privremeno dodat `SecondaryGoal` na
`MacrocycleBlock` obara ga sa imenom polja u poruci.

## Provereno u živoj aplikaciji

Isti šablon (3 serije, 8–12), oba izbora, na rebuild-ovanom stacku:

| Izbor | Serije (ned. 1–3) | Ponavljanja |
|---|---|---|
| Prilagodi ciljnom volumenu | 5 | 8–12 |
| Prati moj šablon | **3** | 8–12 |

Zatim završen trening prve nedelje u planu koji prati šablon: nedelje 2 i 3 i dalje stoje
na **3** serije, dakle izbor je preživeo balansiranje pri završetku.

- `dotnet build`, `dotnet test` — 338 prolazi
- `npm run build`, `npm test` — 111 prolazi
- Migracija primenjena na lokalnu bazu i na Docker
