# CI i zavisnosti

## Problem

- Poslednja zakazana pokretanja CI-ja padala su na dva posla: `npm audit` isporučenih
  paketa i gitleaks.
- gitleaks je prijavljivao 15 nalaza u tri `*.spec.ts` fajla: četiri izmišljene lozinke koje
  testovi upisuju u obrasce.
- Akcije u `ci.yml` bile su na Node 20 izdanjima. Autor gitleaks akcije navodi da GitHub taj
  runtime uklanja 16.9.2026, pa se stara izdanja više ne mogu pokrenuti.

## Rešenje

- Angular paketi na 22.3.0, i sve zavisnosti razrešene iznova (`package-lock.json` je
  napravljen od nule).
- Uklonjen `overrides` blok: `@angular/build` 22.3 traži `@babel/core` 8, a blok ga je držao
  na 7.
- Najmanja verzija `vitest` podignuta na 4.1.12.
- Četiri lozinke iz testova dodate u `.gitleaks.toml`, usidrene po vrednosti.
- `actions/checkout@v7`, `setup-node@v7`, `setup-dotnet@v6` i gitleaks akcija v3.0.0,
  zakačena za commit. Dodato `workflow_dispatch` za ručno pokretanje.

## Provera

- `npm audit` i `npm audit --omit=dev`: 0. `dotnet list package --vulnerable`: 0.
- `npm ci` prolazi nad novim lockfile-om na Windows-u i u Docker build-u (Linux).
- Produkcijski build, 477 klijentskih testova i obe skripte nad izgrađenom aplikacijom
  prolaze.
- gitleaks 8.24.3 (isto izdanje koje koristi akcija) lokalno nad celom istorijom: 15 nalaza
  sa starom konfiguracijom, 0 sa novom. Probni fajl sa nasumičnom lozinkom i sa nasumičnim
  nizom koji sadrži izuzetu vrednost i dalje se prijavljuje.

## Ako zatreba ponovo praviti lockfile

npm 10.9.8 se ruši (`Cannot read properties of null (reading 'edgesOut')`) kada pravi stablo
od nule uz `vitest`. Prošlo je ovim redom, iz `strength-planner-web`:

```bash
npm install --package-lock-only --legacy-peer-deps
npm install --package-lock-only
npm ci
```

Sam prvi korak nije dovoljan: `npm ci` tada javlja da u lockfile-u nedostaju paketi.
