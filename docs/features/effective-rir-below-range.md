# Otkaz se prepoznaje i iz brojeva, ne samo iz kvačice

Pitanje iz stvarne upotrebe, i tačno: *„Ako je opseg 8–12 sa RIR 2, a ja uradim 6
ponavljanja sa RIR 0, zar to ne znači da sam ja odradio 6 ponavljanja do otkaza? Čemu onda
postoji checkbox 'do otkaza'?"*

Odgovor je bio delimičan pre ove izmene: **da, u pravu si — i to je bila rupa.**

## Šta se dešavalo

`WorkingSet.EffectiveRir` je gledao isključivo `IsFailure`:

```csharp
if (!IsFailure)
{
    return Rir;
}
```

Ko ručno klikne RIR dugme „0" na 6 od 8 ponavljanja, bez da dotakne checkbox, dobija
efektivni RIR **0** — identično kao da je uredno završio 12 ponavljanja sa RIR 0. Sistem
nije znao da su 2 ponavljanja promašena ispod donje granice.

To je tačna rupa koju je `feature/failed-reps-logging` (runda 1) trebalo da zatvori: cilj
je bio da se korekcija naniže izjednači sa korekcijom naviše baš za ovakve slučajeve. Ali
zatvorena je samo za onog ko se seti checkbox-a — a lako se zaboravi baš u trenutku otkaza,
ne pre njega.

## Zašto je RIR 0 ispod opsega uvek otkaz

Nula rezerve znači „nisam mogao još jedno ponavljanje". Ispod donje granice opsega, to je
istovetno sa otkazom — nema kombinacije brojeva u kojoj je „nula rezerve, ali nisam
otkazao" smisleno. **RIR iznad nule** ostaje drugačiji, stvaran signal: vežbač je stao
namerno sa rezervom (bol, vreme, forma), ne zato što nije mogao dalje — to se ne dira.

## Šta je promenjeno

`WorkingSet.ImpliesFailure(reps, rir, repRangeMin, explicitlyMarked)` je nova, javna
metoda u Domain sloju:

```csharp
return explicitlyMarked || (rir == 0 && reps < repRangeMin);
```

`EffectiveRir` je poziva umesto da gleda samo zastavicu. Metoda je javna i deljena
namerno — `SetLogService` je poziva i pri upisu serije, da `SetLog.IsFailure` u bazi ne bi
tvrdio „RIR 0" za nešto što je izračunato kao otkaz. Da definicija „šta je otkaz" postoji
na dva mesta (algoritam i servis koji piše u bazu), razišla bi se prvi put kad neko izmeni
jedno a zaboravi drugo — tačna vrsta greške koju je ovaj projekat već nekoliko puta hvatao
(dužina naziva dana, string za pol, pre uvođenja enuma).

Checkbox ostaje, i i dalje nešto radi:

- **Tokom unosa** — dok je uključen, RIR dugmad su onemogućena i tekst uživo broji koliko
  je ponavljanja promašeno, pre nego što se serija uopšte upiše.
- **U istoriji** — na vrhu opsega (npr. 12 od 8–12) broj ispada isti bilo da je checkbox
  uključen ili ne (oba daju efektivni RIR 0), ali samo checkbox upisuje da je to bio pravi
  otkaz, a ne udoban RIR 0. Zapis time ostaje pošten prema onome što se stvarno desilo.

## Provereno u živoj aplikaciji

Tačno prijavljeni scenario, kroz pravi API poziv, opseg 8–12:

- `{"reps": 6, "rir": 0, "isFailure": false}` → server vraća **`"isFailure": true"`**
- tri takve serije na 100 kg → sledeća težina **90 kg** (puni −10% plafon, isto kao da su
  sve tri čekirane kao otkaz; pre popravke bi ovo bilo najviše −3%)
- auto-deload se pokrenuo kao posledica — sistem je stvarno tretirao ovo kao tri otkaza,
  ne kao tri blage RIR 0 serije
- u bazi sve tri serije upisane sa `IsFailure = true`, iako je zahtev slao `false`
- kontrolni slučaj, `{"reps": 6, "rir": 2, "isFailure": false}` → server vraća
  **`"isFailure": false"`** — namerno zaustavljanje sa rezervom ostaje netaknuto

Testovi: `dotnet test` 350 prolazi (bilo 338, +12 novih — direktno na
`WorkingSet.EffectiveRir` i `WorkingSet.ImpliesFailure`, i na `ProgressionEngine.ComputeNext`
koji dokazuje da opterećenje pada isto bez obzira na checkbox). Provereno da testovi hvataju
regresiju: privremeno vraćanje stare formule (`ImpliesFailure` da gleda samo zastavicu) obara
četiri testa, uključujući tačan prijavljeni slučaj.
