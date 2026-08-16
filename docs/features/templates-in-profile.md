# Lični šabloni se otvaraju iz profila

**Grana:** `chore/templates-in-profile`

## Zašto

Ekran `Moji šabloni` se do sada otvarao **iz čarobnjaka za mezociklus** - vezom u zaglavlju
sekcije „Šablon". To je bilo na pogrešnom mestu iz dva razloga:

1. Pravljenje šablona nije korak u pravljenju plana. Šablon je nešto što se napravi jednom
   pa se koristi više puta, kao i lične vežbe ili korak opterećenja - a sve to već stoji u
   profilu.
2. Ekran za mezociklus prestaje da postoji (vidi [`macrocycle-first`](macrocycle-first.md)),
   pa bi jedini ulaz u šablone nestao sa njim.

## Šta je urađeno

Profil je dobio sekciju **„Moji šabloni"**, odmah iznad „Moje vežbe" - dve stavke istog
reda, obe su korisnikov sadržaj koji ulazi u plan.

Sekcija nosi **ulaz u ekran, a ne sam uređivač**. Uređivač ima dane, vežbe u svakom danu i
po tri broja za svaku vežbu; ubačen u profil, koji već nosi četiri sekcije, dobio bi ekran
kroz koji se skroluje bez kraja. Ruta `/templates` ostaje ista, pa veza na nju i dalje radi
i može se otvoriti direktno.

Veza iz čarobnjaka za mezociklus je uklonjena, a sa njom i `block__head` i `block__link`
stilovi i `RouterLink` iz `create-mesocycle.ts`, koji posle toga nemaju korisnika.

## Šta nije urađeno, i zašto

Sekcija **ne pokazuje broj sačuvanih šablona.** To bi tražilo još jedan HTTP poziv pri
svakom otvaranju profila, i još jedno stanje greške na ekranu koji ih već ima nekoliko.
Broj se ionako vidi čim se ekran otvori.

## Provera

- `npm run build` i `npm test` prolaze.
- **Prolaz kroz aplikaciju na širini telefona.** Profil nosi sekcije ovim redom:
  `Osnovni podaci`, `Lozinka`, `Korak opterećenja`, **`Moji šabloni`**, `Moje vežbe` -
  dakle šabloni stoje tačno iznad vežbi, kako je i zamišljeno. Veza vodi na `/templates` i
  otvara ekran sa uređivačem.
