# Dve stvari o polju za težinu koje aplikacija nije govorila

Nalazi E4 i E5 iz pregleda logike treninga.

## E4 — jedna bučica ili obe?

Nigde nije pisalo. A odluka je postojala, i videla se u koraku: **2 kg**.

Stalak sa bučicama ide 10, 12, 14… — dakle 2 kg **po bučici**. Par se pomera po 4 kg. Korak
od 2 kg zato može da znači samo jedno: upisuje se težina **jedne**.

Vežbač koji je unosio zbir dobijao je predloge kojih na stalku nema: 42 kg tamo gde postoje
40 i 44. Uz to su mu procena maksimuma i tonaža bile na dvostrukoj skali u odnosu na sve
ostalo.

Rečeno je sada na tri mesta: uz polje za težinu dok se serija unosi, na ekranu „Vežbe" gde
se korak podešava, i u uputstvu. Komentar uz `EquipmentWeightStep` objašnjava zašto je broj
2 kg i sam po sebi bio ta odluka.

**Konvencija se nije menjala, samo izgovorila.** Prebacivanje na zbir bi zvučalo tačnije za
tonažu, ali bi tiho promenilo značenje svake već upisane serije sa bučicama — a koja je od
dve konvencije korišćena, po zapisu se ne vidi.

## E5 — 0 kg na vežbi koja se opterećuje

Polje je podrazumevano stajalo na 0 kg kada predloga nema, i ništa nije reagovalo. Prazna
šipka je oko 20 kg, pa je nula tu greška u kucanju, ne stanje.

Cena te greške nije mala: serija na nuli ne prolazi `CanEstimateFrom`, pa nema procene
maksimuma; tonaža joj je nula; a progresija na `usedTotalKg <= 0` javlja `LoadFloorReached`
i sledeći put predlaže — nulu. Jedna pogrešna serija tiho isključi vežbu iz svih računica.

Sada stoji upozorenje uz polje.

### Izuzetak koji je morao da se pogodi tačno

Upozorenje gleda **spravu**, a ne udeo telesne mase.

Plank ima spravu `Bodyweight` ali udeo **0**, pa u DTO-u `isBodyweight` stoji na `false` —
vezati upozorenje za njega značilo bi da plank dobija crveno na svakoj seriji, iako se
legitimno loguje na nuli. Zato je `Equipment` dodat u `ExercisePlanDto` i pravilo glasi:
ćuti za sve što je `Bodyweight`, javi za `Barbell`, `Dumbbell`, `Machine` i `Cable`.

Isto polje služi i nalazu E4 — jedna izmena DTO-a nosi obe napomene.

## Poredak napomena

Nula je hitnija od podsetnika, pa kod bučice na nuli ide upozorenje, a podsetnik na jednu
bučicu tek kada težina postoji. To drži i test.

## Šta je izmereno

Uživo, trening „Upper A" (napredni nivo):

| Vežba | Sprava | Težina | Šta piše |
|---|---|---|---|
| Bench Press | Barbell | 92.5 kg | ništa — ima predlog |
| Cable Fly | Cable | 0 kg | upozorenje na nulu |
| Lateral Raise | Dumbbell | 0 kg | upozorenje na nulu |
| Lateral Raise | Dumbbell | 6 kg | *„Unosi težinu jedne bučice, ne zbir obe."* |

Bez horizontalnog prelivanja na 375 px.

## Usput: stilski fajl je rasterećen

Runda 12 je zabeležila da je `workout-session.scss` na 7.98 kB od tvrde granice od 8 kB i da
**sledeća izmena tog ekrana mora da počne od rasterećenja**. Ova izmena ga je i prekoračila
(8.05 kB, build pao), pa je preporuka izvršena: četiri odvojena bloka koja su `mat-icon`
oblikovala na isti način spojena su u jedan.

Rezultat je **7.93 kB** — niže nego pre ove grane, uprkos dve nove napomene. (`.retry` je
bio na 1.15rem umesto 1.2rem; razlika od 0.05rem se ne vidi, a četiri bloka jesu.)

## Testovi

`load-input-note.spec.ts` (6 tvrdnji): podsetnik samo za bučice, upozorenje za sve što se
opterećuje spolja, ćutanje za `Bodyweight` uključujući plank na nuli, ćutanje kada sprava
nije poznata, i da nula pobeđuje podsetnik.

Ukupno: 613 testova na serveru (bez izmena), 144 → 150 na klijentu.

## Ispravka (runda 14)

Ekran je od ove grane pa do runde 14 uz **svaku** spravu na nuli pisao „prazna šipka je već oko
20 kg" — i uz sajlu, mašinu i bučicu, koje šipku nemaju. Beleška runde 13 u `CLAUDE.md` navodi
da je prva verzija upozorenja bila netačna baš zbog te rečenice i da je ispravljena, ali
ispravka nije stigla u kod: `git log -S "prazna šipka"` pokazuje jedan jedini commit, ovaj. Nađeno
tek na E2E pregledu granice serija po treningu, na Cable Fly i Triceps Pushdown. Sada upozorenje
ima dve verzije (`zero-on-barbell` i `zero-on-loaded`), i test traži da rečenica o šipci stoji
samo uz šipku. Ni kod šipke se više ne tvrdi 20 kg kao činjenica o toj vežbi: Skull Crusher je
u katalogu `Barbell`, a radi se najčešće sa EZ šipkom od oko pola toga — tekst sada kaže da i
prazna šipka ima težinu, uz olimpijsku kao primer.
