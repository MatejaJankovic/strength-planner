# Pol koji se konačno prikazuje, i polje koje ničemu nije služilo

**Grana:** `fix/profile-fields`

## Problem 1: izabrani pol se nije video na profilu

Na ekranu "Profil" je padajući meni za pol stajao prazan, bez obzira na to što je pol bio
upisan pri registraciji. Uzrok je bio banalan i zato dugo nevidljiv - **dva ekrana su imala
svaki svoj spisak vrednosti:**

```html
<!-- registracija -->
<option value="male">Muški</option>
<option value="female">Ženski</option>

<!-- profil -->
<option value="M">Muški</option>
<option value="F">Ženski</option>
```

Kolona `Profiles.Sex` je bila slobodan tekst (`varchar(16)`), pa je server bez pogovora
primao i jedno i drugo. Kada bi profil dobio `"male"`, nijedna od njegovih opcija se ne bi
poklopila; Angular u tom slučaju postavlja `selectedIndex = -1` i meni ostaje prazan. Bez
greške, bez poruke - polje jednostavno izgleda kao da nije popunjeno.

Koliko se to razišlo, vidi se u zatečenoj bazi. Tri različita zapisa za istu vrednost:

| Zapisano | Redova |
|---|---|
| `M` | 21 |
| `Male` | 3 |
| `male` | 1 |
| *(prazno)* | 108 |

`Male` sa velikim slovom nije pisao nijedan ekran - stigao je iz provera nad API-jem
tokom bezbednosnih rundi. To je i poenta: dok je polje slobodan tekst, svako ko piše u
njega izmišlja svoj zapis.

### Rešenje: enum, kao i za nivo iskustva

Pol je sada `Domain.Enums.Sex` (`Male = 0`, `Female = 1`, nullable jer je polje opciono),
sa `[DefinedEnum]` na DTO-ima - isti postupak koji `ExperienceLevel` već koristi otkako je
u četvrtoj rundi zatvoreno `"experienceLevel": 999`. Ovo je popravka **klase greške**, a ne
samo ovog slučaja: dva spiska stringova više ne mogu da se raziđu jer stringova nema.

Uz to, oba ekrana sada čitaju isti spisak opcija (`SEX_OPTIONS` u `auth.models.ts`), pa se
ni prikaz ne može razdvojiti od drugog.

Migracija `SexAsEnumAndDropTrainingDays` **ne koristi ono što je EF sam napisao.** Njegov
`AlterColumn` iz `varchar` u `integer` PostgreSQL odbija - `'male'` se ne može pretvoriti u
broj, a ni `USING` tu ne pomaže. Konverzija zato ide kroz privremenu kolonu i preslikava
sve zatečene zapise (`lower()`, pa i `M`, `Male` i `male` završe na istom mestu). Vrednost
koju nijedan ekran nije umeo da prikaže postaje prazno polje, a ne nagađanje.

Posle migracije: 25 redova sa `0` (Male), 108 praznih. Ništa nije izgubljeno.

### Napomena o simptomu

Korisnik je prijavio da pol ne ostaje označen ni **posle čuvanja na profilu**. Kod za tu
putanju je ispravan: `loadMe()` je običan GET bez keširanja, a `"M"` sačuvan sa profila bi
se pri povratku poklopio sa opcijom `M`.

Objašnjenje je druga greška sa istog spiska, i ono **više nije pretpostavka nego merenje**.
Na priloženom snimku ekrana dugme "Sačuvaj profil" stoji ispod donje navigacije. Izmereno
na 375px sa zatečenim stilom (tri kolone za četiri stavke): traka je visoka **127px**, a
`.app-shell` ispod sadržaja rezerviše **82px** - dakle poslednjih **45px** ekrana je pod
trakom. Dugme je visoko 48px.

Klik na "Sačuvaj profil" je pogađao navigaciju, profil se nije ni čuvao, i pol se pri
povratku očekivano nije video. Sa četiri kolone traka je 69px i ne prekriva ništa; vidi
[`mobile-layout-and-copy`](mobile-layout-and-copy.md).

## Problem 2: "Treninga nedeljno" nije imalo šta da radi

Polje se tražilo pri registraciji i moglo se menjati na profilu, a služilo je jednoj jedinoj
stvari: da server označi šablon oznakom **"predlog za tebe"**. Korisnik na sledećem ekranu
ionako bira šablon od koliko dana hoće, pa je isti podatak unosio dvaput - jednom kao broj,
jednom kao izbor šablona - i drugi unos je uvek pobeđivao.

Uklonjeno je do kraja, a ne samo sa ekrana:

- polje sa registracije i sa profila,
- `TrainingDaysPerWeek` iz `RegisterDto`, `UpdateProfileDto`, `CurrentUserDto` i entiteta
  `Profile` (kolona je uklonjena migracijom),
- `WorkoutTemplateCatalog.SuggestedFor` i njegovi testovi,
- `WorkoutTemplateDto.IsSuggested` i oznaka "predlog za tebe" u čarobnjaku.

Ostalo bez posledica: `TrainingWeekSchedule.OffsetFor` raspoređuje treninge po nedelji iz
**broja dana u šablonu**, ne iz profila, pa datumi treninga nisu dirani.

Umesto predloga, oba ekrana sada biraju prvi šablon iz spiska kao polaznu vrednost - samo
da dugme ne bi bilo zaključano na otvaranju.

## Provera

- `dotnet build`, `dotnet test`, `npm run build`, `npm test` - sve prolazi.
- Dva nova xUnit testa: `[DefinedEnum]` odbija pol koji enum ne definiše, i propušta
  prazan pol.
- **Šest novih testova komponente** (`profile-home.spec.ts`) idu kroz ceo put koji je bio
  pokvaren: odgovor servera → označena opcija → telo zahteva pri čuvanju. Pokriveni su i
  slučajevi koji se lako previde:
  - vrednost koju enum ne definiše (zatečeno `"Male"`) ostavlja polje prazno umesto da
    obori ekran,
  - "ne želim da navedem" šalje `null`, a ne nulu - `Number('')` je nula, a nula je
    `Male`, pa se prazan string proverava pre pretvaranja,
  - `trainingDaysPerWeek` se više ne pojavljuje u telu zahteva.
- Migracija primenjena na lokalnu bazu; broj redova pre i posle proveren upitom.
- **Prolaz kroz aplikaciju sa prijavljenim nalogom**, tačno onim redosledom koji je bio
  prijavljen kao pokvaren:
  1. nalog napravljen sa polom "Muški" → server vraća `sex: 0`, a `trainingDaysPerWeek`
     više nije ni u odgovoru;
  2. ekran "Profil" pokazuje **"Muški"** - dakle pol izabran pri registraciji se konačno
     vidi, što je izvorna greška;
  3. promena na "Ženski" → "Sačuvaj profil" (`PUT /api/auth/profile` → 200) → odlazak na
     drugi ekran → povratak na profil → **i dalje "Ženski"**.
- Provereno i da dugme "Sačuvaj profil" više ne stoji ispod donje navigacije, što je i bio
  razlog zbog kog čuvanje ranije nije ni polazilo.
- Polja "Treninga nedeljno" nema ni na registraciji ni na profilu.

## Poznato ograničenje

`Sex` je namerno ostao na dve vrednosti, koliko ih je i ranije bilo ponuđeno. Polje ne
ulazi ni u jedan algoritam - stoji uz uzrast i telesnu masu kao evidencija - i "ne želim da
navedem" je i dalje ponuđeno i podrazumevano.
