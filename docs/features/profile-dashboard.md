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
- bez grešaka u konzoli

Jedna sitnica nađena na snimku i popravljena: u redu sa dugmetom „Vrati X kg" naziv vežbe se
lomio u dva reda, jer kontrole traže oko 200px od dostupnih 343px. Ispod 480px kontrole sada
idu u svoj red pod nazivom.
