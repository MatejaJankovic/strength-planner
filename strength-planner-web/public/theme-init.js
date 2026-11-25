// Postavlja temu pre prvog iscrtavanja, da se pri otvaranju ne vidi bljesak svetle teme na tamnom telefonu.
//
// Mora da bude spoljašnja skripta (CSP dozvoljava samo `script-src 'self'`) i mora da se izvrši pre
// stilova, pa stoji u <head> bez `defer`. Ista pravila kao `ThemeService` (src/app/core/theme): ključ u
// pregledaču, vrednosti `light` / `dark`, i boja trake pregledača. Skript ne sme da baci grešku: bez njega
// aplikacija radi, samo sa bljeskom.
(function () {
  try {
    var stored = localStorage.getItem('strength-planner.theme');
    var root = document.documentElement;

    if (stored === 'light' || stored === 'dark') {
      root.setAttribute('data-theme', stored);
    }

    var dark =
      stored === 'dark' ||
      (stored !== 'light' && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
    var meta = document.querySelector('meta[name="theme-color"]');

    if (meta) {
      meta.setAttribute('content', dark ? '#0f1215' : '#0e5c63');
    }
  } catch (error) {
    // Prostor blokiran ili nedostupan: ostaje tema sistema.
  }
})();
