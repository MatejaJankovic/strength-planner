# Analitika: bez prelivanja, i bez taba na dnu

Dve prijave sa istog spiska: ekran Analitike se prelivao van širine telefona, i tražilo se
da se Analitika ukloni sa trake na dnu — na nju se dolazi samo preko dugmeta „Statistika"
u profilu.

## Prelivanje

Slika je pokazivala izbor mezociklusa odsečen na desnoj ivici ekrana. Uzrok: `.control__select`
u `volume.scss` i `tonnage.scss` nije imao `width: 100%`, iako ga `e1rm-trend.scss` na istom
ekranu već ima — ista popravka je stigla samo do jednog od tri selekta na stranici.

Izmereno pre popravke, dugačak naziv bloka ("Upper/Lower (4x nedeljno, sopstveni raspored
vežbi)"): dokument širok **633px na viewportu od 375px**, select širok 583px. Uzrok je
`<select>` koji se ne skuplja ispod širine najdužeg naziva mezociklusa, u `.controls`
redu koji nema `min-width: 0` na svojoj `.control` stavci — ista klasa greške koja je
popravljena drugde na ovoj aplikaciji (registracija, izmena profila), samo je ovde dvaput
promakla.

Popravka: `width: 100%` na `.control__select`, i `flex: 1 1 220px; min-width: 0` na
`.control`, u oba fajla. Izmereno posle: **375 = 375**, sva tri selekta na ekranu 310px.

## Analitika van trake na dnu

`navItems` u `app.ts` ima sada tri stavke umesto četiri. Broj CSS kolona u `.bottom-nav`
je bio hardkodiran na `repeat(4, ...)` — sa komentarom koji upravo objašnjava da manje
kolona nego stavki gura poslednju u drugi red. Trebalo je spustiti na `repeat(3, ...)`
ručno; broj kolona ne prati dužinu niza sam od sebe.

Ruta `/analytics` ostaje netaknuta i i dalje radi — dugme „Statistika" na dashboardu
profila je sada jedini ulaz, kako je i traženo.

`app.spec.ts` dobija test koji drži tačan spisak od tri taba, da sledeći dodatak u
`navItems` mora svesno da uskladi i broj kolona.

## Provereno u živoj aplikaciji

- dugačak naziv bloka na ekranu Analitike: `docScrollW` jednako `viewport` na sva tri
  podekrana (e1RM trend, volumen, tonaža)
- traka na dnu: tri taba, jedan red, bez preloma (`Trening`, `Plan`, `Profil`)
- dugme „Statistika" na profilu vodi na `/analytics` i ekran se otvara ispravno
- `dotnet test` nepromenjeno (izmena je samo na frontendu), `npm test` 112 prolazi
  (bilo 111), `npm run build` čist
