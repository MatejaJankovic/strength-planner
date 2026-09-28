# Šta kartica vežbe zaista zna: predlog težine i ciljna ponavljanja

Nalazi D21 i D23b iz pregleda logike treninga. Oba su o istoj stvari — šta ekran za trening
kaže da zna.

## D21 — „Nema 1RM za ovu vežbu", a 1RM postoji

Generator upisuje cilj opterećenja **samo za prvu nedelju**
(`GetInitialTargetWeight` je vraćao `null` za svaku drugu). Kasnije nedelje puni progresija,
kada se **isti dan** prethodne nedelje završi. Dok se to ne desi, polje je prazno — a ekran
je prazno polje čitao kao „ne znamo ništa o ovoj vežbi" i pisao *„Nema 1RM za ovu vežbu"*.

Dva puta na koja to pogađa vežbača koji **ima** upisan maksimum:

1. **Nedelja 2 i dalje**, dok isti dan prethodne nedelje nije odrađen. Treninzi se ne moraju
   raditi redom, pa je to obična situacija.
2. **Unutar iste nedelje.** Sveža procena sa ponedeljka ne stiže do četvrtka, jer se propis
   prenosi samo sa istog dana prethodne nedelje. Isto važi i kad tek uneseš maksimum za
   vežbu koja ga nije imala: plan je već napravljen.

Izmereno nad razvojnom bazom:

| | Redova |
|---|---|
| planova vežbi bez upisanog cilja | 12 802 |
| **od toga onih čiji vlasnik ima maksimum za tu vežbu** | **3 068** |
| od toga u treninzima koji još nisu završeni | 2 322 |

## Rešenje

Pravilo po kome generator izvodi prvo opterećenje preseljeno je u domen kao
`StartingLoad`, i ekran ga sada pita za svaki plan bez upisanog cilja. Dva pozivaoca, jedno
pravilo — kao `ComparableWeek` i `BlockExperienceLevel` iz prethodnih rundi.

**Odgovor se računa pri čitanju, ne upisuje.** Tako uvek polazi od najsvežijeg maksimuma, a
u nedelje do kojih vežbač nije stigao se ne upisuju brojevi koje bi progresija ionako
prepisala.

Razlika se **prikazuje**, jer nije ista tvrdnja:

| Šta kartica piše | Odakle predlog |
|---|---|
| ništa posebno | iz onoga što je stvarno digao isti dan prethodne nedelje |
| *„Predlog je izveden iz tvog maksimuma…"* | iz poznatog 1RM-a |
| *„Nema 1RM za ovu vežbu"* | zaista ga nema |

Vežba sa telesnom masom i dalje kreće od **0 dodatnih kilograma** kad maksimuma nema — to je
tačan broj („sopstvenom masom"), a ne nepoznanica.

## D23b — ciljna ponavljanja koja se računaju a ne koriste

Rad (slučaj korišćenja 6) obećava da sistem prikazuje „предложену тежину **и циљни број
понављања за сваку серију**". Predložena težina se prikazivala; ciljna ponavljanja nisu.

Polje koje je to trebalo da nosi, `ProgressionResult.NextTargetReps`, bilo je na **sva tri**
mesta gde nastaje doslovno `repRangeMin` — ulaz vraćen kao da je rezultat — i nijedan čitalac
van testova ga nije čitao. Tri testa su ga proveravala, i sva tri su tvrdila
`Assert.Equal(repRangeMin, result.NextTargetReps)`, dakle da se ulaz vratio nepromenjen.

Polje je obrisano, a cilj je stavljen tamo gde vežbač radi: iznad polja za ponavljanja sada
piše **„Ponavljanja · cilj N"**, gde je N vrh opsega — broj ka kome dupla progresija radi i
koji, kad ga sve serije dostignu, diže opterećenje. Kod propisa sa tačnim brojem ponavljanja
(5×5) dno i vrh su isti broj, pa je i cilj taj broj.

## Usput ispravljeno

Tri čitanja sesije su sama sastavljala isti poziv ka `ToDto`. To je tačno oblik greške iz
devete runde (`SetLogDto`, građen na tri mesta, gde je nova kolona stigla do dva): ručno
sastavljanje ne mora da bude potpuno, pa se prevodi i tipizira i kad jedno mesto zaostane.
Sada postoji jedan put, `ToDtoAsync`.

## Šta je provereno

Uživo, blok napravljen bez ijednog maksimuma, pa naknadno upisan 1RM od 160 kg za čučanj:

| | Pre | Posle |
|---|---|---|
| Nedelja 1, Lower A | „Nema 1RM za ovu vežbu", polje 0.0 kg | **122.5 kg**, uz napomenu da je iz maksimuma |
| Nedelja 2, Lower A (nedelja 1 nije odrađena) | isto | **122.5 kg**, ista napomena |
| Ostale vežbe u tom danu (bez maksimuma) | „Nema 1RM" | **„Nema 1RM"** — nepromenjeno, i tačno |

122.5 je Epley nad 9 efektivnih ponavljanja (8 na RIR 1) iz 160 kg, zaokruženo na korak od
2.5 kg.

## Poznato ograničenje

Kao i kod nivoa iskustva u ovoj rundi: **samo pravilo** je pokriveno testovima
(`StartingLoadTests`, 6 tvrdnji), a to što ga ekran zaista poziva nije — projekat nema test
harness za servise. Živa provera gore je jedini dokaz za taj deo.

**Stilski fajl ekrana za trening je na 7.98 kB, a granica je 8 kB.** Bio je preko granice
upozorenja od 6 kB i pre ove runde (7.6 kB na `main`-u pre runde 12). Ovo je preporuka, ne
usputna napomena: **sledeća izmena tog ekrana mora da počne od rasterećenja tog fajla** —
ili da svesno podigne `anyComponentStyle` budžet u `angular.json`. Nova napomena ovde deli
selektor sa postojećim (`.card__hint--volume, .card__hint--bodyweight, .card__hint--estimate`)
baš zato.

Ukupno: 597 → 603 testa na serveru, 139 na klijentu (bez izmena).
