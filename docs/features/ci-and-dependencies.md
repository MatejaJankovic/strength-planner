# CI i zavisnosti

## Problem

- Poslednja zakazana pokretanja CI-ja padala su na dva posla: `npm audit` isporučenih
  paketa i gitleaks.
- gitleaks je prijavljivao 15 nalaza u tri `*.spec.ts` fajla: četiri izmišljene lozinke koje
  testovi upisuju u obrasce.
- Akcije u `ci.yml` ciljale su Node 20, koji GitHub povlači: 21.9.2026. su se još
  izvršavale, prinudno na Node 24. Autor gitleaks akcije najavio je da stara izdanja posle
  16.9.2026. prestaju da rade; to nije izmereno.

## Rešenje

- Angular paketi na 22.3.0, i sve zavisnosti razrešene iznova (`package-lock.json` je
  napravljen od nule).
- Uklonjen `overrides` blok: `@angular/build` 22.3 traži `@babel/core` 8, a blok ga je držao
  na 7.
- Najmanja verzija `vitest` podignuta na 4.1.12.
- Četiri lozinke iz testova izuzete u `.gitleaks.toml`, usidrene po vrednosti i samo u
  klijentskim `*.spec.ts` fajlovima.
- `actions/checkout@v7`, `setup-node@v7`, `setup-dotnet@v6` i gitleaks akcija v3.0.0,
  zakačena za commit. Dodato `workflow_dispatch` za ručno pokretanje.

## Provera

- `npm audit` i `npm audit --omit=dev`: 0. `dotnet list package --vulnerable`: 0.
- `npm ci` prolazi nad novim lockfile-om na Windows-u i u Docker build-u (Linux).
- Produkcijski build i 477 klijentskih testova prolaze, kao i `check-production-index.py`
  nad izgrađenim `index.html` i `check-theme-contrast.py` nad izvornim fajlovima.
- gitleaks 8.24.3 (isto izdanje koje koristi akcija) lokalno nad celom istorijom: 15 nalaza
  sa starom konfiguracijom, 0 sa novom. U probnim fajlovima se i dalje prijavljuju nasumična
  lozinka, nasumičan niz koji sadrži izuzetu vrednost i izuzeta vrednost van klijentskog
  testa (`.ts`, `appsettings.json`, `.env`).
- Na produkcijskom buildu iza nginx-a prođeni su registracija, unos maksimuma, pravljenje
  plana, upis serija i završetak treninga, analitika, istorija, podešavanja i promena teme.

## Šta je pregled našao

- Izuzetak za četiri lozinke važio je u svim fajlovima, pa bi ista vrednost u konfiguraciji
  prošla. Sužen je na klijentske testove.
- Beleška i `security.md` tvrdili su više nego što je izmereno (da stare akcije ne mogu da se
  pokrenu, da se svaka druga tajna u testu prijavljuje, da su obe skripte proverile build) i
  nosili stari broj testova.

## Ako zatreba ponovo praviti lockfile

npm 10.9.8 se ruši (`Cannot read properties of null (reading 'edgesOut')`) kada pravi stablo
od nule uz `vitest`. Prošlo je ovim redom, iz `strength-planner-web`:

```bash
npm install --package-lock-only --legacy-peer-deps
npm install --package-lock-only
npm ci
```

Sam prvi korak nije dovoljan: `npm ci` tada javlja da u lockfile-u nedostaju paketi.
