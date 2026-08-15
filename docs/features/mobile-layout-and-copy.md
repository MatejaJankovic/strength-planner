# Ispravke rasporeda na telefonu i teksta u interfejsu

**Grana:** `fix/mobile-layout-and-copy`

Sve što je ovde ispravljeno vidi se **samo na telefonu**. Na desktopu su isti ekrani
izgledali uredno, pa su greške preživele sve dosadašnje runde: donja navigacija je imala
dovoljno mesta, izbor nedelje je stajao u jednom redu, a naziv plana nije imao gde da se
prelomi. Interfejs je pravljen za usku širinu, ali se proveravao na širokoj.

## Šta je bilo pokvareno

### 1. Četvrti tab je padao u drugi red

Donja navigacija je bila mreža sa **tri** kolone, a stavki ima **četiri**:

```scss
grid-template-columns: repeat(3, minmax(0, 1fr));
```

"Trening", "Plan" i "Analitika" su popunjavali prvi red, a "Profil" je počinjao drugi.

Posledica nije bila samo ružna traka. **Izmereno u pregledaču na 375px, sa stvarnim
prevedenim stilovima:**

| | Visina trake | `.app-shell` rezerviše | Prekriva sadržaj |
|---|---|---|---|
| Tri kolone (zatečeno) | 127px | 82px | **45px** |
| Četiri kolone | 69px | 82px | 0 |

Poslednjih 45px svakog ekrana je stajalo ispod navigacije. Dugme je visoko 48px, pa je
dugme na dnu ekrana bilo praktično nedodirljivo - klik je pogađao traku. Zbog toga je ovo
ispravka i za jednu grešku sa drugog spiska; vidi
[`profile-fields`](profile-fields.md).

Kolona je sada četiri, i to je cela ispravka. Naziv taba uz to više ne sme sam da se
prelomi (`white-space: nowrap`), kao osigurač da se viša traka ne vrati.

Prva verzija je uz to skupljala i slova (`clamp(0.66rem, 3vw, 0.76rem)`), pod
pretpostavkom da "Analitika" ne staje u četvrtinu trake na uskom ekranu. **Merenje je tu
pretpostavku oborilo.** Sa stvarnim fontom (Inter, težina 800) na 320px:

| Naziv | Širina na 0.76rem | Raspoloživo |
|---|---|---|
| Analitika | 54px | 61px |
| Trening | 46px | 61px |
| Profil | 32px | 61px |
| Plan | 26px | 61px |

Najduži naziv ima 7px viška i pri **nepromenjenoj** veličini slova. Skupljanje teksta je
zato uklonjeno: rešavalo je problem koji ne postoji, a uvodilo je ponašanje (tekst koji se
menja sa širinom prozora) koje niko nije tražio. Ostao je samo broj kolona.

Provereno na živoj aplikaciji: na 320px traka daje četiri kolone od po 71px, sve četiri
stavke stoje u jednom redu i nijedan naziv se ne skraćuje.

### 2. Šestonedeljni blok je lomio izbor nedelje na dva reda

Izbor nedelje u analitici je bio zakucan na četiri kolone od po 44px:

```scss
grid-template-columns: repeat(4, 44px);
```

Četiri nedelje su bile tačna pretpostavka dok je blok trajao četiri nedelje. Otkako
[`periodization-models`](periodization-models.md) donosi šestonedeljne blokove, nedelje 5 i
6 su padale u drugi red i čitale su se kao zasebna grupa dugmadi, a ne kao nastavak istog
izbora.

Sada je red jedan, koliko god nedelja blok imao: `grid-auto-flow: column` sa
`grid-auto-columns: minmax(0, 1fr)`. Visina dugmeta ostaje 44px (cilj za prst), širina sme
da se skupi. Da bi `1fr` kolone imale šta da podele, omotač je dobio `flex: 1 1 100%` —
bez određene širine mreža bi se skupila na širinu cifre umesto da popuni red.

### 3. Naziv aktivnog plana je zauzimao pola ekrana

`.feature-header h1` je zajednički za sve ekrane i namerno je velik:
`clamp(2rem, 12vw, 3rem)`. Za kratke etikete ekrana ("Postavke", "Napravi mezociklus") to
je u redu, jer su fiksne dužine i pisane su uz taj stil.

Ali na ekranu "Trening" isti stil nosi **korisnikov naziv plana**, koji nije kratak. Naziv
koji generiše dugoročni plan — `Zima 2026 - blok 1 (Push/Pull/Legs x2)` — pada u pet
redova od po 47px pre nego što se vidi ijedan trening.

Dodat je modifikator `feature-header--dynamic` za naslove koji nose korisnički sadržaj:
`clamp(1.5rem, 7vw, 2.25rem)`, `line-height: 1.08` i `overflow-wrap: break-word`. Isti
naziv sada staje u dva reda. Ostali ekrani nisu dirani.

### 4. Tri padajuća menija u jednom redu bloka makrociklusa

Red za blok u čarobnjaku dugoročnog plana je bio mreža od pet kolona: redni broj, cilj,
raspored, šablon, dugme za brisanje. Tri `select`-a su delila jednu širinu telefona, pa je
od sadržaja ostajalo `Hipe…`, `Obrn…`, `Upp…`. Šta blok zapravo radi nije se moglo
pročitati bez otvaranja sva tri menija — a oznake polja su bile `sr-only`, dakle ni one
nisu pomagale.

Blok je sada kartica sa poljima jedno ispod drugog i **vidljivim** oznakama ("Cilj",
"Raspored", "Šablon treninga"). Dugme za brisanje je otišlo u zaglavlje kartice, pored
oznake "Blok N". Klasa `.sr-only` je uklonjena iz `plan-home.scss` jer je posle ovoga
nigde nije koristila.

## Izmene teksta

| Gde | Bilo | Sada |
|---|---|---|
| Zaglavlje aplikacije | "Planiraj mirno. Napreduj tačno." | "Planiraj. Kidaj. Napreduj." |
| Napravi mezociklus | "Blok treninga sa jednim ciljem; poslednja nedelja je planirani deload." | "Blok treninga sa jednim ciljem." |
| Analitika | "e1RM raste, volumen ostaje u zoni - backend računa, ovde vidiš rezultat." | *(uklonjeno)* |

## Crtice

Sve **duge crte** (`—`) u tekstu koji korisnik vidi zamenjene su običnom crticom (`-`):
26 mesta u Angular šablonima i dva niza u `create-mesocycle.ts`. Komentari u kodu, XML
dokumentacija i `docs/` nisu dirani — njih korisnik aplikacije ne vidi.

**Kratke crte u opsezima ostaju kratke crte** (`3–12 ponavljanja`, `MEV 7–14`, `RIR 0–3`).
To je drugi znak i u opsegu se čita ispravno; zamena bi značila `3-12`, što se lako pomeša
sa oduzimanjem.

Jedna duga crta nije bila u interfejsu nego u **bazi**. Nazive blokova dugoročnog plana
sastavlja `MacrocycleService.BuildBlockName`:

```csharp
var name = $"{planName} — blok {order} ({templateName})";
```

Generator sada piše crticu, ali to ne popravlja planove koji već postoje. Zato ide i
migracija `NormalizeGeneratedBlockNames`, koja jednim `UPDATE`-om prevodi zatečene nazive.
Njen `Down` je namerno prazan: vraćanje crtica u duge crte pogodilo bi i svaku crticu koju
je korisnik sam ukucao u naziv plana.

## Provera

- `dotnet build`, `dotnet test`, `npm run build`, `npm test` — sve prolazi.
- Migracija primenjena na lokalnu bazu; provereno upitom da nijedan naziv mezociklusa
  više ne sadrži dugu crtu (126 redova).
- **Navigacija izmerena u pregledaču** na 375px i 320px, nad stvarnim prevedenim stilovima:
  četiri jednake kolone, sve stavke na istoj visini (jedan red), nijedan naziv skraćen,
  visina trake 69px naspram 82px koje `.app-shell` rezerviše.

Izbor nedelje, naslov plana i kartica bloka su iza prijave, pa ostaju za prolaz kroz
aplikaciju sa prijavljenim nalogom.
