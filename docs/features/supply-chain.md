# Zavisnosti, tajne i provere koje se same pokreću

## Razvojne ranjivosti

`npm audit` je prijavljivao tri niske ranjivosti u lancu Angular alata (`@babel/core` preko
`@angular/build`, `esbuild` preko `vite`). Nijedna se nije isporučivala — `npm audit
--omit=dev` je i pre ovoga bio nula — ali su držale skener crvenim, a crven skener na koji
se navikneš prestaje da bude skener.

`npm audit fix` ih nije rešavao: ispravka traži verzije koje Angular 22 ne razrešava sam.
Rešene su `overrides` blokom koji podiže razrešene verzije unutar postojećeg lanca, bez
diranja same verzije Angulara.

Provereno da to ništa ne lomi: produkcijski build prolazi, svih 7 frontend testova prolazi,
`npm audit` je nula i sa razvojnim zavisnostima.

> **Kada ovo ukloniti.** `overrides` je tup alat: nameće verziju svakom paketu koji zavisi od
> nje, bez obzira na opseg koji je taj paket tražio. Danas je to tačno, ali važi zauvek — kad
> Angular objavi verziju čiji alat traži drugi `esbuild`, npm će i dalje forsirati ovaj, a
> greška će izaći daleko od ovog fajla i neće ličiti na svoj uzrok. `package.json` ne trpi
> komentare, pa je uslov zapisan ovde: **obrisati oba unosa čim `npm audit` bude nula i bez
> njih** (najlakše posle Dependabot PR-a koji diže `@angular/build`).

## Tajne u istoriji

Kroz istoriju je jednom prošao JWT ključ — `dev-only-super-secret-key-change-me-…`, vrednost
koja i po imenu kaže da je razvojna i nikada nije korišćena van lokalne mašine. Uklonjen je
ranije, prepisivanjem istorije.

Provereno da ga zaista nema: prošlo je kroz **sve grane na daljinskom**, ne samo kroz `main`
— nijedna ga ne sadrži. Preživeo je još samo u tri lokalne pomoćne grane napravljene pre
prepisivanja (`backup-before-jwt-purge`, `backup-pre-history-rewrite`, `backup-pre-rewrite-2`),
koje nikada nisu bile poslate.

One su obrisane. Jedan `git push --all` bio je dovoljan da ih vrati u opticaj — a to je
tačno ona vrsta poteza koji se uradi u žurbi. Posle brisanja pretraga po svim dostupnim
ref-ovima vraća nula pogodaka.

## Provere koje ne zavise od toga da se neko seti

Ovo je glavni deo. Sve gore je stanje na jedan dan; nalaz iz skeniranja zavisnosti stari sam
od sebe, jer paket koji je danas čist objavi ranjivost sledeće nedelje bez ijedne izmene u
ovom repozitorijumu.

**CI** (`.github/workflows/ci.yml`) na svaku izmenu **i ponedeljkom ujutru po rasporedu**:

- `dotnet build` i `dotnet test`
- `npm run build` i `npm test`
- `dotnet list package --vulnerable` — ispis se čita i prevodi u pad, jer ta naredba **uvek**
  izlazi sa nulom; bez toga bi korak bio zelen i kada nešto nađe
- `npm audit --omit=dev` obara build, jer se to isporučuje korisniku
- `npm audit` sa razvojnim zavisnostima samo prijavljuje — ranjivost u alatu ne treba da
  zaustavi rad na aplikaciji
- **gitleaks nad celom istorijom** (`fetch-depth: 0`), da tajna koja uđe pa se ukloni iz
  radnog stabla ne prođe nezapaženo — jer u istoriji ostaje

Raspored postoji zbog te druge vrste nalaza: bez njega bi ranjivost objavljena u nedelji kad
niko ništa ne menja čekala sledeći commit.

**Dependabot** (`.github/dependabot.yml`) donosi same ispravke: NuGet i npm nedeljno, GitHub
akcije i Docker osnovne imidže mesečno. Bez toga nalaz stoji dok se neko ne seti da ga reši,
a to je tačno stanje u kom je repozitorijum i bio — `deployment-security.md` je do sada
tražio da se vlasnik sam seti da povuče nove osnovne imidže.

Microsoft/System paketi i Angular paketi su grupisani, jer se izdaju zajedno: pojedinačni
PR-ovi bi pravili kombinacije verzija koje niko nije testirao.

Compose fajlovi imaju svoj unos (`docker-compose`), odvojen od `docker`. To nije uredno
razvrstavanje nego nužnost: ekosistem `docker` čita samo Dockerfile-ove, pa bi `postgres:16`
i `caddy:2-alpine` — baza i komponenta koja prekida TLS — ostali bez ijednog predloga za
nadogradnju, dok bi dokumentacija tvrdila da su pokriveni. To je gore od nepokrivenosti, jer
vlasnik prestane da gleda baš ono što niko ne gleda.

## Sitnice koje su se pokazale bitnim

Skener koji pri prvom pokretanju pocrveni na vrednostima koje su namerno lažne nauči ljude da
ga preskaču — i tada prestaje da bude skener. Zato `.gitleaks.toml` poimence izuzima
`.env.example`, uputstvo za user-secrets i dokumentaciju, u kojoj se placeholder vrednosti
navode kao deo objašnjenja.

Provera ranjivih NuGet paketa čita **JSON**, a ne traži englesku rečenicu u ispisu. Prva
verzija je radila `grep` po tekstu; to radi dok se rečenica ne promeni u nekoj verziji SDK-a
ili dok runner ne bude na drugom jeziku, a onda korak tiho postane zelen nad projektom punim
ranjivih paketa. Greška u bezopasnom smeru je kod ovakve provere najopasnija.

gitleaks je zakačen za commit, a ne za oznaku `v2`. Oznakom upravlja tuđi repozitorijum i
može da se pomeri, a taj korak radi sa tokenom ovog repozitorijuma. U izmeni koja se bavi
lancem snabdevanja, jedina uvedena tuđa zavisnost ne sme da bude ona koja visi u vazduhu.
