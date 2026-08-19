# Registracija kao čarobnjak, i visina

Registracija je bila jedan formular sa šest polja. Sada je niz ekrana, po jedno pitanje na
svakom — kao u aplikacijama koje rade isto (uzor je bio HEVY). Uz to je dodata visina,
jedino novo polje u ovoj rundi.

Izgled je ostao u postojećoj svetloj paleti aplikacije. Uzor je taman, ali tamna tema je
odluka o celoj aplikaciji, ne o jednom ekranu; menjati je samo ovde značilo bi da se prvi
utisak i sve posle prijave ne poklapaju.

## Šta korisnik vidi

Osam koraka, redom:

1. Ime
2. Email i lozinka
3. Pol
4. Telesna masa
5. Visina
6. Uzrast
7. Nivo iskustva
8. Poznati maksimumi (1RM)

Iznad svakog stoji strelica nazad i traka od osam segmenata. Dugme na dnu piše „Nastavi",
osim na sedmom koraku gde piše „Napravi nalog" i na osmom gde piše „Nastavi na plan".

Mere (masa, visina, uzrast) unose se velikim brojem, klizačem ispod njega i poljem za
precizan unos. Klizač sam ne bi bio dovoljan: na opsegu od 30 do 300 kg jedan piksel vredi
skoro pola kilograma, pa se tačna vrednost prstom ne pogađa. Polje samo ne bi bilo
dovoljno jer traži tastaturu za nešto što je u suštini izbor sa skale.

Pol i nivo iskustva biraju se karticama na celu širinu ekrana umesto padajućim menijem: na
telefonu sistemski meni prekrije pitanje na koje se odgovara. Kartice se biraju i
strelicama, i tada fokus prati izbor.

## Odluke koje su oblikovale izvedbu

**Nalog nastaje tek posle sedmog koraka, jednim zahtevom.** Odgovori se do tada drže u
pregledaču. Da nalog nastaje na prvom koraku, odustajanje na petom ekranu ostavilo bi nalog
sa pola profila koji korisnik ne može ni da dovrši ni da obriše. Cena je što se zauzet
email vidi tek posle sedmog koraka — zato je taj korak drugi po redu, da se do njega dođe u
dva dodira.

**Osmi korak (1RM) živi na svojoj ruti, `/onboarding`.** Taj ekran traži katalog vežbi sa
servera, dakle postojeći token, pa mora da bude posle registracije. Prva verzija ga je
držala unutar `/register` kao osmi `@switch` slučaj — i to je bila greška, opisana niže.
Sada čarobnjak posle registracije prelazi na `/onboarding?wizard=1`, a taj parametar ekranu
kaže da nosi opremu čarobnjaka (traku napretka, „Korak 8 od 8", dugme „Nastavi na plan")
umesto svog samostalnog zaglavlja. Bez parametra isti ekran je obična izmena maksimuma,
dostupna sa profila.

**Ukupan broj koraka stoji na jednom mestu.** `REGISTRATION_STEP_COUNT` u
`auth.models.ts`, jer ga čitaju dva ekrana na dve rute i traka napretka mora da pokazuje
isti ukupan broj na oba.

**Visina ne ulazi ni u jedan algoritam.** Stoji uz pol kao evidencija, i opciona je na
serveru kao i pol. Nalozi napravljeni pre ove promene je nemaju i nema odakle da im se
izvede.

**Ime je obavezno pri registraciji, ali kolona je nullable.** Nalozi stariji od polja
nemaju ime. Da je i izmena profila traži, takav korisnik ne bi mogao da promeni ni telesnu
masu dok ne postavi ime. `ProfileFieldTests` drži obe strane te razlike.

## Greške izmerene tokom rada, sve zapisane

Prve dve su nađene pri prvom prolasku kroz aplikaciju, ostale u kod-rivjuu istog PR-a.

**`canContinue` je bio `computed()` nad `form.controls.*.valid`.** `computed` prati samo
signale, a `AbstractControl.valid` nije signal — izračunao se jednom, nad praznom formom,
i ostao `false` zauvek. Nijedan test forme to ne bi uhvatio; videlo se tek na ekranu, gde
je prvi prolaz kroz čarobnjaka stao na prvom pitanju sa mrtvim dugmetom „Nastavi". Rešeno
tako što `toSignal(form.valueChanges)` uspostavlja zavisnost koju `computed` ume da prati.

**`PUT /api/auth/profile` je potpuna zamena, a ekran profila nije slao nova polja.** Polje
koje se ne pošalje server upisuje kao prazno. Pošto profil pre ove runde nije imao ni ime
ni visinu, prvo čuvanje telesne mase brisalo bi oboje — a korisnik bi video samo poruku da
je profil sačuvan.

To je popravljeno na dva nivoa. Ekran profila sada nosi oba polja, i dva testa u
`profile-home.spec.ts` drže i slučaj kada su popunjena i slučaj kada su prazna (prazno ide
kao `null`, jer je `Number('')` nula, a nulu `[Range]` odbija). Ali ekran nije bio pravi
uzrok — uzrok je što endpoint dopušta nepotpun zahtev bez ijedne greške. Zato
`ProfileReplacementTests` proverava odrazom da **svako** polje profila koje korisnik može
da menja postoji i u `UpdateProfileDto`. Provereno da test radi: privremeno dodata kolona
`RestingHeartRate` u `Profile` obara ga sa imenom polja u poruci. Sledeća kolona zaboravljena
u DTO-u pada na buildu, a ne tiho briše nečije podatke.

**Polje za precizan unos pokazivalo je odbijenu vrednost.** Angular ne prepisuje `[value]`
kada se model nije promenio, a odsecanje na granicu upravo to daje kada je vrednost već
bila na granici. Izmereno: model na 300 kg, ukucano 999 — prikaz i klizač su pokazivali
300, a polje 999, i ništa nije govorilo koja od te dve vrednosti ide u zahtev. Ista greška
koju `profile-home.ts` već opisuje za korak opterećenja, ponovljena u novoj komponenti.
Rešeno ručnim vraćanjem vrednosti u polje, i pokriveno testom.

**Osvežavanje stranice na osmom koraku vraćalo je na prvo pitanje.** Dok je 1RM bio osmi
`@switch` slučaj unutar `/register`, korak je živeo samo u memoriji komponente: osvežavanje
ili vraćanje taba dovodilo je već prijavljenog korisnika na „Kako da te zovemo?", bez puta
nazad do maksimuma. Isti nedostatak je značio i da ulogovan korisnik koji otvori `/register`
može da napravi drugi nalog — token prvog bio bi prosto zamenjen, bez poruke. Rešeno
premeštanjem osmog koraka na `/onboarding` i novom stražom `guestGuard`, koja na prijavu i
registraciju pušta samo neprijavljenog posetioca.

**`/onboarding` je bio ostao bez ijednog puta do sebe.** Kada je čarobnjak preuzeo 1RM,
nijedan ekran više nije vodio na tu rutu, pa su se maksimumi mogli uneti samo jednom — pri
registraciji. Profil je dobio karticu „Poznati maksimumi" koja vodi tamo.

**Unos pola se nije mogao poništiti.** Kartice samo postavljaju vrednost, a formular koji
je čarobnjak zamenio imao je „Ne želim da navedem". Ko jednom dodirne „Muški" nije imao
načina da se vrati na neizjašnjeno, iako je pol na serveru nullable upravo zato da sme da se
ne navede. Dodata je treća kartica sa tim značenjem.

**Brisanje sadržaja polja skakalo je na donju granicu.** `Number('')` je nula, a nula se
onda odsecala na minimum — pa je selektovanje i brisanje mase radi ponovnog kucanja
postavljalo 30 kg. Prazno polje sada znači „bez izmene".

**Mere su bile unapred popunjene bez ijedne naznake o tome.** Klizač mora od nečega da
krene, pa masa, visina i uzrast startuju na 75 kg / 175 cm / 25 godina i dugme „Nastavi"
radi bez dodira — dok je formular pre čarobnjaka masu i uzrast tražio izričito. Ko brzo
prođe kroz tri ekrana dobije profil koji tvrdi tri mere koje nikada nije izgovorio. Uzor sa
slika radi isto, pa ekrani i dalje ne traže dodir, ali sada pišu da je vrednost unapred
popunjena.

**Identifikator polja gradio se od teksta oznake**, pa je ispadao `id` sa razmacima
(`measure-Telesna masa u kilogramima`) — što HTML ne dopušta — i dve mere sa istom oznakom
na jednom ekranu dobile bi isti `id`. Sada se traži izvana (`inputId`).

**Klizač je čitaču ekrana javljao samo broj**, bez jedinice. Dodat `aria-valuetext`.

**Stilovi polja bili su prepisani iz `_auth-shell.scss`**, sa sitnim odstupanjima u visini i
radijusu — pa bi izmena zajedničkog izgleda polja hvatala prijavu a promašivala registraciju.
Sada se zajednički fajl uvozi, a menja se samo ono što se stvarno razlikuje.

## Provereno u živoj aplikaciji

Dva puna prolaska kroz čarobnjaka na širini telefona (375 px), oba do baze:

- prvi nalog: `displayName "Mateja"`, `sex 0`, `age 27`, `bodyweightKg 82.5`, `heightCm 183`,
  `experienceLevel 1`
- drugi nalog, sa odbijenim odgovorom o polu: `displayName "Ivana"`, **`sex null`**,
  `age 22`, `bodyweightKg 68.5`, `heightCm 167`, `experienceLevel 1`
- ulogovan korisnik koji otvori `/register` završava na `/workout`
- registracija prelazi na `/onboarding?wizard=1`, „Korak 8 od 8", jedan naslov, bez strelice
  nazad, bez dupliranog dugmeta
- osvežavanje na osmom koraku ostaje na osmom koraku
- `/onboarding` bez parametra prikazuje svoje zaglavlje i svoje dugme, bez trake napretka
- dvaput ukucano 999 kg: polje, prikaz i klizač svi pokazuju 300
- obrisan sadržaj polja ostavlja 68.5, ne skače na 30
- strelica nadole pomera i izbor i fokus na istu ponudu
- bez grešaka u konzoli

**Snimak ekrana nije napravljen.** Browser panel u aplikaciji nije bio prikazan, pa je
snimanje padalo sa „the Browser pane is not displayed, so the page is not compositing
frames". Sve gore navedeno je provereno kroz stablo pristupačnosti, mrežne pozive i
`/auth/me`, ali vizuelni rezultat osam novih ekrana time nije potvrđen — a ovaj PR je
upravo o tome kako ti ekrani izgledaju.
