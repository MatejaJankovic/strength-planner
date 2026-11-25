import { platesFor, platesLabel } from './plates';
import { warmupLabel, warmupSets } from './warmup';

/** Vrednosti `equipment` i `exerciseType` koje čine složenu vežbu sa olimpijskom šipkom. */
export const BARBELL = 'Barbell';
export const COMPOUND = 'Compound';

/**
 * Šipka od 20 kg i rampa zagrevanja imaju smisla samo uz složenu vežbu sa šipkom (čučanj, potisak,
 * mrtvo dizanje). Pregib i skull crusher su u katalogu takođe „Barbell", ali se rade lakom ili EZ
 * šipkom: tamo bi predlog ploča i zagrevanja zavarao, a ne pomogao. Bez poznatog tipa (putanja koja
 * ga ne šalje) ne predlaže se ništa.
 */
export function isBarbellCompound(equipment: string | undefined, exerciseType: string | undefined): boolean {
  // Server poredi opremu bez obzira na velika slova i čuva vežbu napravljenu preko API-ja onako kako je
  // uneta, pa „barbell" mora da važi isto što i „Barbell" (kao u load-input-note.ts).
  return equipment?.trim().toLowerCase() === BARBELL.toLowerCase() && exerciseType === COMPOUND;
}

/**
 * Šta se stavlja na šipku za težinu koja se unosi, ili null kad napomena nema čemu da posluži:
 * vežba nije složena sa šipkom, ili je težina manja od same šipke (to uključuje nulu, od koje polje
 * počinje dok nema 1RM-a, pa se ništa ne prikazuje pre nego što lifter dodirne polje).
 */
export function platesHint(
  equipment: string | undefined,
  exerciseType: string | undefined,
  weightKg: number,
): string | null {
  if (!isBarbellCompound(equipment, exerciseType)) {
    return null;
  }

  return platesLabel(platesFor(weightKg));
}

/**
 * Predlog zagrevanja pre prve radne serije, ili null.
 *
 * Samo za složenu vežbu sa šipkom, samo dok nema nijedne odrađene serije (posle prve radne serije
 * mišići su zagrejani), i samo kad ima šta da se predloži. Ne upisuje se i ne ulazi u volumen:
 * ovo je podsetnik, a ne serija.
 */
export function warmupHint(
  equipment: string | undefined,
  exerciseType: string | undefined,
  workingKg: number,
  stepKg: number,
  loggedSets: number,
): string | null {
  if (!isBarbellCompound(equipment, exerciseType) || loggedSets > 0) {
    return null;
  }

  const sets = warmupSets(workingKg, stepKg);
  return sets.length > 0 ? warmupLabel(sets) : null;
}
