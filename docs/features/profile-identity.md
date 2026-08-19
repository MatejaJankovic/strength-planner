# Ime, slika profila i ekran za izmenu

Profil je do sada bio naslov „Postavke" sa email adresom pod njim i karticom „Osnovni
podaci" koja je držala formu. Sada je pregled: slika, ime, pročitani podaci o vežbaču i
dugme olovke koje vodi na `/profile/edit`, gde se sve to menja.

Ime je već postojalo od prethodne runde (registracija ga traži). Ovde je dobilo mesto na
ekranu i mogućnost izmene, a uz njega je došla i slika.

## Šta korisnik vidi

**`/profile`** — krug sa slikom (ili prvim slovom imena kada slike nema), ime kao naslov,
email pod njim, olovka desno. Ispod toga uzrast, telesna masa, visina, nivo iskustva i pol,
i to samo ona polja koja su popunjena: red sa praznom vrednošću izgleda kao greška, a
visina i pol su na serveru opcioni.

**`/profile/edit`** — zaglavlje sa strelicom nazad, naslovom i dugmetom „Gotovo", pa krug
sa slikom i „Promeni sliku" / „Ukloni sliku", pa ime, pa ostali podaci.

## Odluke koje su oblikovale izvedbu

**Slika stoji u bazi, kao `bytea`.** Aplikacija radi u kontejneru bez montiranog volumena,
pa bi fajl na disku nestao pri prvom restartu. Uz to kolona nasleđuje filtriranje po
vlasniku koje svaki upit u ovom servisu već ima, a fajl na disku bi tražio sopstveno
imenovanje i sopstvenu proveru putanje.

**Tip slike utvrđuje server iz bajtova, nikada iz `Content-Type` zaglavlja ni iz
ekstenzije.** Oba dolaze od klijenta i oba se slobodno lažu, a taj tip se posle vraća
svakom pregledaču koji otvori profil — pa mora da bude tvrdnja servera. `ImageFormat.Detect`
gleda potpis na početku fajla i prihvata JPEG, PNG i WebP.

**SVG nije na spisku, iako je slika.** SVG je i dokument koji nosi skript. Isključen je
namerno, i test to zaključava.

**RIFF potpis se proverava u celini.** WebP je RIFF kontejner, ali isti kontejner nose i WAV
i AVI; provera samo prva četiri bajta bi ih propustila.

**Slika se čuva odmah po izboru, odvojenim zahtevom.** Ostala polja čekaju „Sačuvaj". Ne
zato što je lakše: slika ide kao multipart a ostalo kao JSON, i vezati ih u jedan zahtev
značilo bi da neuspeh na jednom polju vrati i sliku.

**Otprema je multipart, ne base64 u JSON-u.** Base64 uveća sadržaj za trećinu i tera ceo
zahtev u memoriju kao string pre nego što se veličina uopšte proveri.

**Granica je 2 MB, i proverava se na tri mesta.** U pregledaču (udobnost), u kontroleru pre
čitanja u memoriju, i u servisu (jer je servis ugovor za sebe). Slika stoji u koloni baze i
vraća se pri svakom čitanju profila, pa granica štiti i sopstveni odgovor, ne samo upis.

**`CurrentUserDto` nosi samo zastavicu `hasAvatar`, ne i bajtove.** Profil se čita na svakom
ekranu; slika je do dva megabajta i traži se odvojeno.

**Slika ne ide kroz `PUT /api/auth/profile`.** `ProfileReplacementTests` iz prethodne runde
traži da svako polje profila koje korisnik menja bude i u `UpdateProfileDto` — i ovde je
tražio i ova dva. Nisu dodata u DTO nego u imenovani spisak izuzetaka, sa razlogom: da su u
JSON zamenu profila, svako čuvanje osnovnih podataka slalo bi i brisalo sliku. Test je tu i
uradio ono za šta je napisan.

**`blob:` URL je peti keš vezan za korisnika.** `GET /api/auth/avatar` traži Authorization
zaglavlje, a `<img>` ga ne šalje, pa se slika dohvata kao blob i pravi se lokalni URL. Taj
URL ostaje upotrebljiv dok se ne poništi — dakle lice prethodnog korisnika bi stajalo na
profilu narednog do osvežavanja stranice. Zato ulazi u `resetUserCaches` i u zajedničke
helpere u `auth.service.spec.ts`, pa ga sada pokrivaju sva tri postojeća puta (odjava,
prijava, registracija). Provereno da to radi: uklanjanje `clearAvatar()` iz
`resetUserCaches` obara četiri testa, ne jedan.

**`revokeObjectURL` se poziva i na svakoj zameni**, ne samo pri odjavi: bez toga pregledač
drži svaku ranije dohvaćenu sliku u memoriji do osvežavanja stranice.

## Šta je rivju našao i kako je popravljeno

**Otpremanje slike moglo je da upiše profil sa nulama.** Pomoćnu metodu koja kreira profil
ako ga nema izvukao sam iz `UpdateProfileAsync`, gde je bezopasna jer se sva polja odmah
prepišu iz zahteva. Putevi za sliku su je nasledili, a oni ne diraju uzrast ni masu — pa je
`PUT /api/auth/avatar` na nalogu bez profila upisivao `Age = 0` i `BodyweightKg = 0` (ta
polja su u entitetu ne-nullable, dakle nula je jedina vrednost koju prazan red može imati),
i profil bi zatim prikazao „Uzrast 0" i „Telesna masa 0 kg" kao da je to korisnik rekao.
Metoda je razdvojena na dve, sa imenima koja kažu šta rade: `CreateProfileIfMissingAsync`
(samo izmena profila) i `RequireExistingProfileAsync` (slika, greška ako profila nema).
Provereno tako što je red iz `Profiles` obrisan direktno u bazi: i `PUT` i `DELETE` na
slici vraćaju 400 sa „Profil ne postoji", a `/me` i dalje vraća `null` umesto nula.

**404 na slici ostavljao je staru sliku na ekranu.** `setAvatarBlob` — koji je jedini
zvao čišćenje — stoji unutar `map`, a 404 obori tok pre njega. Oba ekrana su grešku gutala
sa `catchError`, pa je signal zadržavao prethodnu vrednost: slika obrisana u drugom tabu
ostajala je vidljiva. Sada servis hvata 404 kao odgovor, čisti signal i vraća `null`.

**Slika se preuzimala pri svakom otvaranju ekrana.** Oba ekrana zovu `loadAvatar()`, a API
na sve odgovore šalje `Cache-Control: no-store`, pa keš pregledača ne pomaže: profil →
izmena → profil bilo je tri preuzimanja do dva megabajta. Servis sada pamti da je slika
tražena — kao i to da je nema — i otpremanje tu zastavicu poništava. Izmereno: četiri
otvaranja ekrana, **jedan** zahtev (ranije četiri). Posle brisanja se takođe ne pita
ponovo, jer se zna da slike nema.

Cena je svesna: promena slike u drugom tabu ne vidi se u ovom do odjave ili osvežavanja.
To je bio i izbor — bajtovi na telefonu vrede više od trenutne svežine slike koju je
korisnik sam postavio.

**Fokus na biraču fajla nije bio vidljiv.** „Promeni sliku" je `<label>`, a oznake nisu u
redu tabulatora; jedini element koji se fokusira je sakriveni `<input type="file">`, a
`.sr-only` ga svodi na jedan piksel bez ikakvog prstena. Korisnik tastature je na tom
ekranu gubio fokus iz vida. Unos je premešten **pre** oznake, pa
`input:focus-visible + label` može da oboji oznaku — CSS nema selektor za prethodni element,
zato red u dokumentu nije slučajan.

**Naslov i slovo u krugu bili su napisani dvaput.** Pravilo „prazno ili samo razmak pada na
email" postojalo je u oba ekrana, a test ga je pokrivao na jednom. Sada su `profileTitle` i
`profileInitial` u `auth.models.ts`.

**Ruta `profile/edit` premeštena je pre `profile`.** Sa obrnutim redom radi, i to je
izmereno — ali samo zato što je `profile` terminalna ruta. Onog dana kada dobije `children`,
`/profile/edit` bi počeo da završava na catch-all ruti bez ijednog traga u diff-u.

Uz to: `saved` signal koji se postavljao a nigde nije čitan je uklonjen; `AvatarDto.Content`
sada piše da se niz **ne** kopira i zašto, umesto da geterima bez setera nagoveštava
nepromenljivost koju ne pruža; i dodati su testovi za granu sa slikom i za uklanjanje slike,
koje nijedan test na tom ekranu nije pokrivao.

Dva nalaza su namerno **ostavljena**. Spisak prihvaćenih formata stoji i na serveru i u
`accept` atributu — to je isti obrazac kao ostale konstante koje dve strane dele
(`PASSWORD_MIN_LENGTH`, granice visine), i zatvaranje bi tražilo nov endpoint samo za tri
imena tipova. I `BuildCurrentUserAsync` čini jedan dodatan upit ka Identity-ju po upisu
slike, radi email adrese; skraćivanje odgovora bi nateralo klijenta da ionako pozove `/me`.

## Zajednički stilovi i duplirano čitanje tokena

Kartice, polja, kontrole, dugmad i poruke su izvučeni u `_profile-shell.scss`, koji oba
ekrana uvoze — isto kao `_auth-shell.scss` kod prijave i registracije. Bez toga bi svaki
ekran nosio svoju kopiju istih pravila, a izmena izgleda polja hvatala jedan i promašivala
drugi. To je greška koju je rivju prethodne runde već našao u čarobnjaku.

`AuthController` sada nasleđuje `AuthorizedControllerBase` i čita id korisnika kroz
`GetUserId()`, kao svaki drugi kontroler i kako CLAUDE.md traži. Čitanje `sub` claim-a bilo
je prepisano u svakoj akciji tog fajla; sa tri nova endpointa to bi bilo devet kopija istog
parsiranja tokena. `[AllowAnonymous]` na registraciji i prijavi nadjačava `[Authorize]` sa
bazne klase, pa se ništa nije otvorilo.

## Pogrešna dijagnoza, zapisana jer se skoro ušla u kod

`/profile/edit` je otvarao ekran treninga. Zaključio sam da ruta `profile` prefiks-poklapanjem
proglašava `/profile/edit` svojim, prebacio specifičniju rutu ispred nje i to komentarisao
kao izmereno. Nije bilo izmereno. Pravi uzrok je bio zaglavljen dev server: gradnja je
padala na `profile-edit.scss` koji u tom trenutku još nije bio upisan, pa je lazy import
komponente odbijen i router je pao na catch-all. Posle restarta servera `/profile/edit`
radi sa **prvobitnim** redom ruta, što je i provereno. Red ruta i komentar su vraćeni.

## Provereno u živoj aplikaciji

- otpremljena prava PNG slika (180 bajtova): `GET` 404 → `PUT` 200 → `GET` 200,
  `content-type: image/png`, isti broj bajtova nazad, krug prikazuje sliku umesto slova
- **odbijeno sve što se pravi da je slika**, svako sa lažnim imenom i lažnim
  `Content-Type: image/png`: HTML sa skriptom, SVG sa skriptom, Windows izvršni fajl, i WAV
  (isti RIFF kontejner kao WebP) — sva četiri 400, sa istom porukom
- postojeća slika je posle tih odbijenih pokušaja nepromenjena (200, 180 bajtova)
- izolacija: moj token 200, token drugog naloga 404 (svoj rezultat, ne moj), bez tokena 401.
  Endpoint nema ni parametar koji bi se menjao — id dolazi samo iz tokena.
- profil prikazuje ime „Mateja", email, i pregled: 27, 82.5 kg, 183 cm, Srednji nivo, Muški
- nijedan od dva ekrana se ne preliva na 375px
- posle popravki: brisanje slike uživo — prikaz pada na slovo „M", dugme „Ukloni sliku"
  nestaje, a profil ostaje netaknut (`Mateja`, 27, 82.5 kg, 183 cm)
- četiri otvaranja ekrana koji prikazuju sliku → jedan zahtev za slikom
- nalog kome je red iz `Profiles` obrisan direktno u bazi: `PUT` i `DELETE` na slici
  vraćaju 400 sa „Profil ne postoji", `/me` vraća `null` a ne nule; `PUT /auth/profile`
  isti profil ispravno kreira i popunjava
- `dotnet test` 313, Angular 75 (bilo 58)
