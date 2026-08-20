# Profil kao raskrsnica: dashboard, podešavanja, vežbe i brisanje naloga

Ekran profila je bio spisak formi jedna pod drugom: osnovni podaci, lozinka, korak
opterećenja sa svim vežbama, šabloni, sopstvene vežbe i odjava. Sada je pregled i
raskrsnica — nosi ono što se o vežbaču čita, i odvodi na ekrane od kojih svaki radi jednu
stvar.

## Šta korisnik vidi

**`/profile`** — slika, ime, email, i **dve** ikonice desno: olovka vodi na izmenu
podataka, zupčanik na podešavanja naloga. Ispod toga pročitani podaci, pa mreža sa tri
dugmeta: **Statistika**, **Vežbe**, **Šabloni**.

**`/settings`** (zupčanik) — lozinka, odjava i brisanje naloga. Sve tri stvari tiču se
pristupa nalogu, a ne trenažnih podataka, pa stoje zajedno i odvojeno od profila.

**`/exercises`** — ceo katalog, jedan red po vežbi: naziv, bedž „tvoja" ako je korisnikova,
bedž „izmenjeno" ako je korak opterećenja prilagođen, mišićne grupe, i select za korak sa
dugmetom za vraćanje podrazumevanog. „+" u zaglavlju otvara formu za novu vežbu.

## Odluke koje su oblikovale izvedbu

**Lista vežbi i korak opterećenja spojeni su u jedan red.** Bile su dve kartice na profilu
koje su govorile o istoj stvari sa dva mesta — jedna je nabrajala samo korisničke vežbe,
druga sve. Praktična posledica spajanja: pre dodavanja nove vežbe vidi se da li već
postoji, jer je pretraga nad istim spiskom u koji se dodaje.

**Dashboard su podaci, ne prepisani blokovi.** Tri dugmeta stoje kao niz u komponenti, pa
dodavanje četvrtog ne dira raspored. Mreža je `auto-fit` sa minimumom od 140px: dva u redu
na telefonu, treće se prelama pod njih.

**Zajednički stilovi su izašli iz `features/profile/`.** Kartice, polja, kontrole, dugmad i
poruke su nastale uz profil kada je izmena izdvojena na svoju rutu, i stajale su u
`features/profile/_profile-shell.scss`. Kada ih je i ekran vežbi počeo da koristi, fajl je
preseljen u `shared/styles/_form-shell.scss`: pravila više nisu profilna, a uvoz
`../profile/profile-shell` iz druge oblasti bi to krio. Pravila spiska vežbi (`exlist`,
`step-controls`, `chips`, `badge`) preselila su se zajedno sa markupom koji ih koristi.

## Brisanje naloga

Traže se **dve** stvari: trenutna lozinka i otkucana reč `OBRIŠI`. Lozinka štiti od nekoga
kome je telefon ostao otključan; otkucana reč štiti od samog korisnika, jer je ovo jedina
operacija u aplikaciji koja se ne može vratiti — nema oporavka lozinke, nema rezervne
kopije, i nema drugog naloga sa istim podacima. Reč se poredi bez obzira na velika i mala
slova; dijakritika je obavezna, jer je to reč koja na ekranu i piše.

Dugme je zaključano dok oba polja nisu ispravna, i reč se proverava i u pregledaču: dugme
koje se može pritisnuti sa pogrešnom rečju traži od korisnika da pravilo otkrije iz poruke
o grešci.

Endpoint nosi isto ograničenje broja poziva kao prijava i promena lozinke — zahtev proverava
lozinku, pa je i ovo put kojim se lozinka može pogađati.

### Red brisanja nije stvar ukusa

Posledica je stranih ključeva, i zapisan je u kodu:

1. **Lični šabloni** idu prvi. Njihove stavke drže vežbe preko
   `UserWorkoutTemplateExercise → Exercise` sa `Restrict`, pa dok šablon postoji,
   korisnikova sopstvena vežba se ne može obrisati.
2. **Nalog** ide drugi. Kaskade iz `ApplicationUserConfiguration` odnose profil, mezocikluse
   (a s njima nedelje, treninge, planove vežbi i serije), maksimume, podešavanja vežbi,
   orijentire volumena i dugoročne planove. Time se puštaju i `Restrict` veze koje
   `ExercisePlan` i `OneRepMaxRecord` drže na vežbama.
3. **Sopstvene vežbe** idu poslednje, kada ih ništa više ne referiše.

Sve u jednoj transakciji: da treći korak padne bez nje, nalog bi bio obrisan a njegove vežbe
bi ostale u katalogu bez vlasnika.

**Dva korisnička entiteta nose samo `Guid`, bez stranog ključa ka nalogu** —
`UserWorkoutTemplate` i `Exercise.CreatedByUserId`. Zato ih kaskada ne dohvata i zato se
brišu ručno. To je i najveći rizik ovakve operacije: greška se ne vidi, jer posle brisanja
ekrani izgledaju ispravno a red koji je ostao u bazi nema odakle da se primeti.

Zato `AccountDeletionTests` ne testira ponašanje nego **oblik modela**: prolazi kroz svaki
entitet u modelu, nalazi one koji nose vlasnika, i traži da svaki bude ili u kaskadi ka
nalogu ili imenovan u spisku onih koje servis briše ručno. Nov korisnički entitet koji nije
ni jedno ni drugo obara build. Drugi test radi obrnuto: ako neki od ručno brisanih kasnije
dobije kaskadu, ručno brisanje postaje suvišan kod koji krije pravo ponašanje, i to takođe
pada.

Provereno da testovi rade: uklanjanje kaskade za `Macrocycle` iz
`ApplicationUserConfiguration` obara prvi test sa imenom `Macrocycle.UserId` u poruci.

## Šta je rivju našao

**Potvrdna reč se poredila po kulturi hosta.** `CurrentCultureIgnoreCase`, a reč se
završava slovom „I" — na hostu sa turskim jezikom „i" i „I" nisu isto slovo, pa bi server
odbijao „obriši" prema „OBRIŠI" i nalog se ne bi mogao obrisati, uz poruku da korisnik
otkuca ono što je već otkucao. Kultura nigde nije zakucana: ni `Program.cs`, ni `csproj`,
ni Dockerfile je ne postavljaju, dakle bila je stvar hosta. Sada se poredi ordinalno.

Dva testa to drže: prvi da reč prolazi pod `tr-TR`, `sr-Latn-RS`, `en-US` i `az-Latn-AZ`;
drugi da poređenje po kulturi **stvarno pada** na turskom, pa je opasnost zapisana kao
izmerena, a ne pretpostavljena. Ekran je istovremeno prebačen sa
`toLocaleUpperCase('sr')` na obično `toUpperCase()`, pa obe strane sada odlučuju isto za
isti unos.

**Lozinka se proveravala pre potvrdne reči.** Dve provere daju dve različite poruke, pa je
onaj ko se domogne tuđeg tokena mogao da pogađa lozinku sa namerno pogrešnom rečju:
„pogrešan email ili lozinka" znači da pogodak nije, poruka o reči znači da jeste — a nalog
se pri tome ne briše. Reč sada ide prva, pa pogrešna reč ne odaje ništa o lozinci, a skupo
heširanje se ne troši na zahtev koji ne može da uspe.

**Ispravna lozinka nije brisala ranije neuspele pokušaje.** `LoginAsync` i
`ChangePasswordAsync` oba zovu `ResetAccessFailedCountAsync`, ovaj metod nije — pa bi dve
greške u kucanju na ovom ekranu ostale na nalogu i kasnija obična greška pri prijavi ga
zaključala. Dodato.

**Zaglavlje podekrana bilo je prepisano tri puta.** `.edit-top` sa dugmetom od 44px i
centriranim naslovom stajalo je u `profile-edit.scss`, `settings-page.scss` i
`exercise-catalog.scss`, i kopije su se već razišle — jedna je grupisala dugme nazad sa
dugmetom za dodavanje, druga nosila prazan `<span>` samo da naslov ostane u sredini. To je
isti razlog zbog kog su kartice i polja izvučeni u `_form-shell.scss`, samo jedan nivo
iznad. Sada je `shared/components/subscreen-header`, sa jednom radnjom desno kroz
projekciju; sve tri kopije su obrisane, a prazan `<span>` više ne treba jer bočne kolone
imaju istu širinu.

**Brisanje je uvlačilo redove u praćenje promena samo da bi ih označilo.** Zamenjeno sa
`ExecuteDeleteAsync` — jedan `DELETE` po skupu, i kraća transakcija koja obuhvata sva tri
koraka.

**Dva ekrana nisu imala ni jedan test**, a jedan od njih nosi jedinu nepovratnu operaciju u
aplikaciji. `settings-page.spec.ts` dodaje 13 testova (kapija za brisanje u svim
kombinacijama, telo zahteva, odjava na 204, odbijeno brisanje koje ostavlja otkucanu reč,
promena lozinke), a `exercise-catalog.spec.ts` 14. Provereno da drugi radi: brisanje ručnog
vraćanja vrednosti u `<select>` obara test sa `expected '10' to be '2.5'` — dakle select bi
prikazivao vrednost koju je server odbio.

Jedan nalaz je namerno ostavljen: potvrdna reč stoji i na serveru i na frontu. Semantika je
izjednačena, pa se dve strane ne mogu raziđati na istom unosu; sam literal je duplikat kao i
sve druge konstante koje dve strane dele, a serviranje jedne reči kroz endpoint ne vredi
dodatne površine.

## Provereno u živoj aplikaciji

Napravljen je nalog za jednokratnu upotrebu sa podacima u osam tabela — profil, 1RM zapis,
slika, sopstvena vežba sa mišićnim grupama, i šablon koji **koristi upravo tu sopstvenu
vežbu**, dakle tačno onaj `Restrict` lanac koji red brisanja mora da poštuje.

Tri pokušaja brisanja:

| Pokušaj | Odgovor |
|---|---|
| pogrešna lozinka, tačna reč | 400, „Pogrešan email ili lozinka." |
| tačna lozinka, pogrešna reč (`DELETE`) | 400, „Za brisanje naloga otkucaj OBRIŠI." |
| oboje tačno (`obriši`, malim slovima) | 204 |

Posle brisanja: `GET /auth/me` sa istim tokenom vraća 401, ponovna prijava istim
kredencijalima vraća 401, i **sve osam tabela su na nuli** — uključujući proveru da u celoj
bazi nema osiročenih `ExerciseMuscles`, `UserWorkoutTemplateDays` ni
`UserWorkoutTemplateExercises`. Ostalih 138 naloga, 33 sistemske vežbe i 1085 1RM zapisa su
netaknuti.

Ostalo, na 375px:

- dashboard: tri dugmeta vode na `/analytics`, `/exercises`, `/templates`; zaglavlje nosi i
  olovku i zupčanik
- dugme za brisanje ostaje zaključano dok su prazna polja, dok je samo lozinka upisana, i
  dok je reč `obrisi` bez dijakritike; oživi na `obriši`
- `/exercises`: 33 vežbe, 33 selecta za korak; pretraga „bench" daje 3 reda; izmena koraka
  na 5 kg upisuje se i red dobija bedž „izmenjeno" i dugme „Vrati 2.5 kg"; dodata vežba
  „Hack Squat" zatvara formu, brojač ide 33 → 34 i red dobija bedž „tvoja"
- nijedan od osam ekrana se ne preliva, i svi zadržavaju traku i navigaciju
- posle popravki: sva tri podekrana koriste isto zaglavlje — `/settings` sa praznim mestom
  za radnju i naslovom izmerenim u sredini, `/profile/edit` sa dugmetom „Gotovo",
  `/exercises` sa ikonicom za dodavanje; nijedan se ne preliva
- bez grešaka u konzoli

Jedna sitnica nađena na snimku i popravljena: u redu sa dugmetom „Vrati X kg" naziv vežbe se
lomio u dva reda, jer kontrole traže oko 200px od dostupnih 343px. Ispod 480px kontrole sada
idu u svoj red pod nazivom.
