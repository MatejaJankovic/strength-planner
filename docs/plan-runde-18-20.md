# Plan za runde 18, 19 i 20

Ovaj dokument je radni nalog za model koji implementira. Nastao je iz pregleda aplikacije i
razgovora sa vlasnikom, i sadrži samo ono što je vlasnik odobrio. Pravila rada (grane, PR,
pregled agentom, provera pre commit-a, beleška u `docs/features/`) su u `CLAUDE.md` i ovde se
ne ponavljaju.

## Polazište

- Aplikaciju koristi samo vlasnik, na telefonu, a ona radi **lokalno** na njegovom računaru.
  Cilj ove etape je **svakodnevna upotreba**, ne odbrana teze.
- Algoritamsko jezgro je zrelo (17 rundi, 930 testova na serveru). Tanko je sve oko njega:
  trening ekran je čist unos serija, analitika je vezana za tekući blok, završen trening se ne
  može ispraviti.
- Runda 18 je razrađena do PR-ova. Runde 19 i 20 su date u grubim crtama i razrađuju se kad
  dođu na red.

## Šta je provereno, a šta nije

Pre pisanja plana pretragom koda je potvrđeno da sledeće **ne postoji**: tajmer odmora, beleške
uz seriju ili trening (`SetLog` i `WorkoutSession` nemaju takvo polje), wake lock, manifest i
service worker, tamna tema, izvoz podataka, veza između `OneRepMaxRecord` i treninga iz kog je
procena nastala (zapis nosi samo `ExerciseId` i `RecordedAt`). Volumen i tonaža u
`AnalyticsController` primaju `mesocycleId`.

**Nije čitano**: `VolumeLandmarkService`, `DeloadService`, veći deo `SessionService` posle
petlje po vežbama, klijentski kod trening ekrana. Sve što plan tvrdi o tim delovima je
pretpostavka i označeno je kao takvo. Implementator prvo čita, pa meri, pa tek onda gradi,
kao u prethodnim rundama.

---

## Runda 18: ispravka završenog treninga i istorija

Redom, svaka grana se spaja pre nego što se sledeća odvoji od `main`.

### 18.1 `refactor/session-completion-steps`

**Zašto.** `SessionService.CompleteAsync` (fajl ima 1034 linije) u jednom prolazu radi sve:
upis procene maksimuma, progresiju i ciljeve za isti dan sledeće nedelje, a kad trening zatvori
nedelju i ocenu umora, auto-deload, učenje granica volumena, balansiranje serija i otvaranje
sledećeg bloka. Ispravka završenog treninga mora deo toga da pokrene ponovo, a danas to nije
moguće bez kopiranja.

**Šta.** Bez promene ponašanja. Obračun se deli na imenovane korake koji se mogu pozvati i iz
završetka i iz ispravke. Redosled „sačuvaj pa balansiraj" je nosiv (runda 5) i ostaje.

**Gotovo je kad:** ceo postojeći skup testova prolazi bez izmene ijednog očekivanja; za svaki
izdvojeni korak postoji bar jedan test na service harness-u (runda 17, #104); završetak treninga
je uživo proveren u oba smera na istom bloku pre i posle (isti brojevi u rezimeu).

**Usput.** `workout-session.scss` je na 7,61 kB od tvrdih 8. Runde 18 i 19 dodaju na taj ekran,
pa se fajl prvo stanjuje ili deli po komponentama. Budžet se ne podiže prećutno.

### 18.2 `feature/edit-completed-session`

**Pravilo koje je vlasnik izabrao: ispravka važi dok posledice nisu potrošene.**

- Serije završenog treninga mogu da se menjaju, dodaju i brišu dok trening **istog dana
  sledeće nedelje nije započet** (nema upisanu seriju i nije završen ni preskočen).
- Posle ispravke se ponovo računaju: procena maksimuma tog treninga, oznaka rekorda i ciljne
  težine za isti dan sledeće nedelje.
- Ako je nedelja već zatvorena, ponovo se računaju i ocena umora i granice volumena.
- **Auto-deload koji je već povučen ostaje.** Ako nova ocena umora ne bi prešla prag, ekran to
  kaže, ali nedelja se ne vraća. Isto važi za već otvoren sledeći blok.
- Kad je prozor zatvoren, ekran kaže zašto ispravka više nije moguća.

**Model podataka.** `OneRepMaxRecord` dobija kolonu koja pokazuje na trening iz kog je procena
nastala (nullable). Ispravka tada zamenjuje procenu umesto da doda drugu. Pre migracije
prebrojati u razvojnoj bazi koliko `Estimated` zapisa postoji i da li se neki može pouzdano
upariti po vremenu; runda 9 i runda 12 su obe pokazale da se backfill prvo meri.

**Odluka vlasnika.** Treninzi završeni pre ove migracije nemaju vezu sa svojom procenom, pa se
ispravljaju samo kao istorija: menja se zapis i analitika, bez preračuna. Ekran to kaže.

**Rizici koje treba izmeriti pre gradnje:**

1. **Da li se korak granice volumena može poništiti.** Ako `UserVolumeLandmark` pamti samo
   trenutnu vrednost, ponovni obračun zatvorene nedelje traži da se pamti i pomeraj po nedelji.
   Granice čitaju prosek dve uporedive nedelje (#97), pa ispravka jedne nedelje može da promeni
   i odluku sledeće.
2. **Ciljevi za sledeću nedelju koje je korisnik već video.** Ako se posle ispravke promene,
   rezime to treba da kaže.
3. **Stavka #106** (preračun ciljeva na čekanju posle novog zapisa maksimuma) već radi nešto
   srodno. Proveriti da li se može ponovo iskoristiti umesto drugog puta kroz isti posao.
4. **Zamenjena vežba** (#111) i **kalibraciona serija** (#110): ispravka ne sme da promeni
   koja je serija kalibraciona ni da vrati staru vežbu.

**Gotovo je kad:** domensko pravilo „da li je trening još ispravljiv" ima svoje testove;
harness test pokriva ispravku sa preračunom i ispravku zatvorene nedelje; revert merenje je
uzeto na commit-ovanom stablu; uživo, na 375 px: pogrešna serija se ispravi, sledeća nedelja
pokaže novu težinu, a uputstvo više ne kaže da je „jedina opcija brisanje celog plana".

### 18.3 `feature/training-history`

Nov ekran sa spiskom svih odrađenih i preskočenih treninga kroz **sve planove**, najnoviji
prvi, sa straničenjem. Otvaranje treninga pokazuje serije, samo za čitanje, a dugme za ispravku
se nudi tamo gde pravilo iz 18.2 dozvoljava. Svaki upit je ograničen po `userId`.

Proveriti pre gradnje: šta se dešava sa treninzima plana koji je obrisan. Brisanje plana briše
mezocikluse eksplicitno (runda 7), pa „cela istorija" znači istoriju planova koji nisu obrisani.
To treba da piše u uputstvu.

### 18.4 `feature/full-history-analytics`

Tonaža i nedeljni volumen dobijaju pregled po vremenskom opsegu, preko granica blokova. Pregled
po bloku ostaje, jer cilj nedelje i granice imaju smisla samo unutar bloka. Uputstvo već
upozorava da tonaža kroz blok prati model periodizacije; pregled kroz blokove mora da označi
granice blokova i deload nedelje da se pad ne bi čitao kao nazadovanje.

Posle ove grane iz odeljka „Šta aplikacija ne radi" u `HowToUseApp.md` izlazi stavka o
analitici, a iz spiska „out of scope" u `CLAUDE.md` „full-history analytics".

---

## Runda 19: telefon

| Grana | Šta radi | Napomena |
|---|---|---|
| `chore/local-https` | Sertifikat za lokalnu mrežu (mkcert) za API i Angular dev server, i uputstvo kako se jednom instalira na telefon | Preduslov za sledeću granu: service worker i wake lock rade samo u bezbednom kontekstu, a adresa računara u lokalnoj mreži to nije. Privatni ključ ne ulazi u repozitorijum; gitleaks već radi u CI. |
| `feature/installable-app` | Manifest, ikone, instalacija na početni ekran, ekran koji se ne gasi dok je trening u toku | **Bez rada van mreže.** Unos serija bez mreže traži red čekanja i idempotentan upis na serveru i nije deo ove runde. |
| `feature/workout-aids` | Tajmer odmora posle upisane serije, beleške uz vežbu i trening vidljive sledeći put, serije zagrevanja iz radne težine, prikaz ploča za šipku | Tajmer je štoperica, ne pravilo: intervali odmora kao deo modela ostaju van opsega (runda 15, G5). Računica za zagrevanje i ploče ide u domen i dobija testove. Ako postane preveliko, deli se na dve grane. |
| `feature/dark-theme` | Prvo boje kao promenljive na svim ekranima, pa tema | Poslednja u rundi, kad ekrani iz prethodne grane prestanu da se menjaju. |

---

## Runda 20: model treninga

Od najmanje ka najviše odluka. Svaka stavka počinje merenjem, kao rundе 13 do 17.

1. **Uređivanje bloka u toku.** Dodavanje i uklanjanje vežbe u tekućem bloku. Zamena vežbe već
   postoji (#111) i pokazala je gde pravila vezana za par (dan, vežba) pucaju kad se vežba
   promeni. Promena modela periodizacije već generisanog bloka ostaje van opsega.
2. **Subjektivni ulaz za umor.** Jedno pitanje posle treninga. Pre gradnje simulacija, po
   uzoru na `docs/simulations/fatigue_signal_noise.py`: da li novi signal popravlja pogađanje
   deload-a, i da li je nezavisan od postojećih (runda 15 je pokazala da „dva signala" mogu biti
   jedan uzrok brojan dvaput).
3. **Leđa po glavama** (latovi, gornja leđa, opružači), po uzoru na rame (#109): vrednosti
   granica po glavi, migracija naučenih granica, analitika, šabloni. Meriti i mišiće koje
   promena istiskuje, brojeći netrenirani mišić kao nula serija.
4. **Treći cilj: održavanje.** Blok sa minimalnim volumenom koji čuva postignuto, za periode
   kad nema vremena ili energije: volumen oko MEV-a, težine se ne guraju. `Goal` danas ima
   `Strength` i `Hypertrophy`. Pre gradnje proveriti šta svako pravilo koje čita cilj radi sa
   trećom vrednošću (cilj nedeljnog volumena, učenje granica, ocena umora, kalibracija), i
   da li učenje granica u takvom bloku treba da ćuti. Peaking nije izabran.

---

## Odloženo, sa razlogom

- **Hosting, backup, health check, email i oporavak lozinke** idu zajedno, kad aplikacija krene
  na server. Dok je lokalna, lozinka se može vratiti u bazi.
- **Rad bez mreže** čeka da se vidi koliko instalirana aplikacija sa lokalnim sertifikatom
  zaista smeta u praksi.
- **Playwright testovi i dalji refaktor** nisu u mapi, osim preduslova 18.1.
- **Dnevnik telesne mase, izveštaj na kraju bloka, izvoz podataka, opisi vežbi**: predloženi,
  vlasnik ih nije izabrao.
- I dalje van opsega: više jezika, talasasta periodizacija, deljenje plana i uloga trenera.

## Otvorene odluke vlasnika

Nema ih. Sve tri koje su postojale su donete i upisane na svom mestu: stari treninzi se
ispravljaju samo kao istorija (18.2), tamna tema ostaje u rundi 19, treći cilj je održavanje.
