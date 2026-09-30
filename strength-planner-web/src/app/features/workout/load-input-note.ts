/**
 * Napomene uz polje za težinu koje iz samog broja ne slede.
 *
 * Dve su, i obe su nalazi iz pregleda modela vežbi:
 *
 * - Kod bučica se unosi težina **jedne**. Nigde nije bilo rečeno, a odluka postoji i vidi
 *   se u koraku: 2 kg je pomak jedne bučice na stalku (10, 12, 14…), dok par ide po 4 kg.
 *   Vežbač koji unese zbir dobija predlog koji na stalku ne postoji.
 * - Nula kilograma kod vežbe koja se opterećuje spolja nije stanje nego greška. Serija
 *   upisana na nuli ne daje ni procenu maksimuma ni tonažu, a progresija iz nje ne može da
 *   izvede sledeće opterećenje. Kod šipke upozorenje dodaje i to da i prazna šipka ima težinu -
 *   ali samo kod šipke: sajla, mašina i bučica šipku nemaju, a runda 13 je tu rečenicu
 *   zapisala kao ispravljenu, iako je ekran nastavio da je piše uz svaku spravu. Broj se ne
 *   tvrdi: olimpijska šipka ima 20 kg, a EZ šipka za Skull Crusher oko pola toga.
 *
 * Vežba koju nosi sopstvena masa je izuzeta iz obe: nula tamo znači „sopstvenom masom", što
 * je tačan unos — a plank se i loguje na nuli.
 */
export type LoadInputNote = 'single-dumbbell' | 'zero-on-barbell' | 'zero-on-loaded' | null;

// Server spravu poredi bez obzira na velika i mala slova, a vežbu koju korisnik napravi
// preko API-ja čuva onako kako je otkucana - pa i ovde.
const BODYWEIGHT = 'bodyweight';
const DUMBBELL = 'dumbbell';
const BARBELL = 'barbell';

export function loadInputNote(equipment: string | undefined, weightKg: number): LoadInputNote {
  const kind = equipment?.trim().toLowerCase();

  if (!kind || kind === BODYWEIGHT) {
    return null;
  }

  // Nula je hitnija poruka od podsetnika na jednu bučicu, pa ide prva.
  if (weightKg <= 0) {
    return kind === BARBELL ? 'zero-on-barbell' : 'zero-on-loaded';
  }

  return kind === DUMBBELL ? 'single-dumbbell' : null;
}

/**
 * Tekst upozorenja na nulu. Stoji ovde, a ne u šablonu ekrana, da bi ga test video: greška iz
 * runde 13 nije bila u izboru napomene nego u rečenici, a test koji proverava samo izbor nju ne
 * bi uhvatio.
 */
export function zeroLoadWarning(note: 'zero-on-barbell' | 'zero-on-loaded'): string {
  const consequence =
    'Serija upisana na nuli ne daje ni procenu maksimuma ni tonažu, a ni predlog za sledeći put.';

  return note === 'zero-on-barbell'
    ? `0 kg, a i prazna šipka ima težinu (olimpijska oko 20 kg). ${consequence}`
    : `0 kg, a ova vežba se opterećuje spolja. ${consequence}`;
}
