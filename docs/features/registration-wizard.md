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
telefonu sistemski meni prekrije pitanje na koje se odgovara.

## Odluke koje su oblikovale izvedbu

**Nalog nastaje tek na kraju, jednim zahtevom.** Odgovori se do tada drže u pregledaču.
Da nalog nastaje na prvom koraku, odustajanje na petom ekranu ostavilo bi nalog sa pola
profila koji korisnik ne može ni da dovrši ni da obriše. Cena je što se zauzet email vidi
tek posle sedmog koraka — zato je taj korak drugi po redu, da se do njega dođe u dva
dodira.

**1RM je posle registracije, ali unutar čarobnjaka.** Taj ekran traži katalog vežbi sa
servera, a njemu se pristupa tek sa tokenom. Zato `POST /api/auth/register` ide između
sedmog i osmog koraka. Osmi korak nema strelicu nazad: nalog je do tada već napravljen, pa
bi povratak vodio na pitanja koja više nemaju gde da se pošalju.

**Postojeći ekran za 1RM se ne duplira.** Dobio je ulaz `embedded`, koji sakriva njegovo
zaglavlje i njegovo dugme za nastavak — čarobnjak ih obezbeđuje. Provereno na ekranu:
jedan naslov, jedno dugme, nijedna strelica nazad.

**Visina ne ulazi ni u jedan algoritam.** Stoji uz pol kao evidencija, i opciona je na
serveru kao i pol. Nalozi napravljeni pre ove promene je nemaju i nema odakle da im se
izvede.

**Ime je obavezno pri registraciji, ali kolona je nullable.** Nalozi stariji od polja
nemaju ime. Da je i izmena profila traži, takav korisnik ne bi mogao da promeni ni telesnu
masu dok ne postavi ime. `ProfileFieldTests` drži obe strane te razlike.

## Dve greške izmerene tokom rada, obe zapisane ovde

**`canContinue` je bio `computed()` nad `form.controls.*.valid`.** `computed` prati samo
signale, a `AbstractControl.valid` nije signal — izračunao se jednom, nad praznom formom,
i ostao `false` zauvek. Nijedan test forme to ne bi uhvatio; videlo se tek na ekranu, gde
je prvi prolaz kroz čarobnjaka stao na prvom pitanju sa mrtvim dugmetom „Nastavi". Rešeno
tako što `toSignal(form.valueChanges)` uspostavlja zavisnost koju `computed` ume da prati.

**`PUT /api/auth/profile` je potpuna zamena, a ekran profila nije slao nova polja.** Polje
koje se ne pošalje server upisuje kao prazno. Pošto profil pre ove runde nije imao ni ime
ni visinu, prvo čuvanje telesne mase brisalo bi oboje — a korisnik bi video samo poruku da
je profil sačuvan. Nađeno pri prolasku kroz živu aplikaciju, ne u testu. Ekran profila sada
nosi oba polja, a dva testa u `profile-home.spec.ts` drže i slučaj kada su popunjena i
slučaj kada su prazna (prazno ide kao `null`, jer je `Number('')` nula, a nulu `[Range]`
odbija).

## Provereno u živoj aplikaciji

Prošao ceo čarobnjak od prvog do osmog koraka na širini telefona (375 px):

- `POST /api/auth/register` → 200
- `GET /api/auth/me` vraća `displayName: "Mateja"`, `heightCm: 183`, `bodyweightKg: 82.5`,
  `age: 27`, `sex: 0`, `experienceLevel: 1` — dakle svaki korak je stigao do baze
- unos visine od 999 cm sečen je na 250
- izmena samo telesne mase (82.5 → 84) ostavila ime i visinu netaknute
- bez grešaka u konzoli
