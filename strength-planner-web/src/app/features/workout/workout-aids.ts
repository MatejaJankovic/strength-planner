import { platesFor, platesLabel } from './plates';
import { warmupLabel, warmupSets } from './warmup';

/** Vrednost `equipment` za vežbu sa olimpijskom šipkom: jedina na kojoj ploče i zagrevanje imaju smisla. */
export const BARBELL = 'Barbell';

/**
 * Šta se stavlja na šipku za težinu koja se unosi, ili null kad napomena nema čemu da posluži:
 * vežba nije sa šipkom, ili težina još nije uneta (bez 1RM-a polje počinje od nule, pa bi se
 * „manje od šipke" prikazivalo pre nego što je lifter ikad dodirnuo polje).
 */
export function platesHint(equipment: string | undefined, weightKg: number): string | null {
  if (equipment !== BARBELL || !(weightKg > 0)) {
    return null;
  }

  return platesLabel(platesFor(weightKg));
}

/**
 * Predlog zagrevanja pre prve radne serije, ili null.
 *
 * Samo za vežbu sa šipkom, samo dok nema nijedne odrađene serije (posle prve radne serije
 * mišići su zagrejani), i samo kad ima šta da se predloži. Ne upisuje se i ne ulazi u volumen:
 * ovo je podsetnik, a ne serija.
 */
export function warmupHint(
  equipment: string | undefined,
  workingKg: number,
  stepKg: number,
  loggedSets: number,
): string | null {
  if (equipment !== BARBELL || loggedSets > 0) {
    return null;
  }

  const sets = warmupSets(workingKg, stepKg);
  return sets.length > 0 ? warmupLabel(sets) : null;
}
