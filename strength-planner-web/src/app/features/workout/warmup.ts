import { BAR_KG } from './plates';

export interface WarmupSet {
  weightKg: number;
  reps: number;
}

/**
 * Udeo radne težine i broj ponavljanja: težina raste, a ponavljanja padaju, da zagrevanje ne
 * zamara pre prve radne serije. Uobičajena praksa, ne izmerena vrednost: zato je to predlog koji se
 * prikazuje, a ne propis koji se upisuje.
 */
const RAMP: readonly (readonly [share: number, reps: number])[] = [
  [0.4, 5],
  [0.6, 3],
  [0.8, 1],
];

/** Ispod ovoga zagrevanje u koracima nema smisla: sama šipka je već blizu radne težine. */
const MIN_WORKING_OVER_BAR = 1.5;

function roundToStep(weightKg: number, stepKg: number): number {
  return Math.round(weightKg / stepKg) * stepKg;
}

/**
 * Serije zagrevanja za vežbu sa šipkom.
 *
 * Ne ulaze u volumen, ne upisuju se i ne utiču na progresiju: prikazuju se pre prve radne serije.
 * Težine se zaokružuju na korak vežbe i ne idu ispod šipke. Serija koja bi izašla jednaka prethodnoj
 * ili radnoj težini se izbacuje. Za radnu težinu blizu same šipke zagrevanje je prazna šipka.
 */
export function warmupSets(
  workingKg: number,
  stepKg: number,
  barKg: number = BAR_KG,
): WarmupSet[] {
  if (!(workingKg > barKg) || !(stepKg > 0)) {
    return [];
  }

  if (workingKg < barKg * MIN_WORKING_OVER_BAR) {
    return [{ weightKg: barKg, reps: 8 }];
  }

  const sets: WarmupSet[] = [];
  let previous = 0;

  for (const [share, reps] of RAMP) {
    const weightKg = Math.max(barKg, roundToStep(workingKg * share, stepKg));

    if (weightKg <= previous || weightKg >= workingKg) {
      continue;
    }

    sets.push({ weightKg, reps });
    previous = weightKg;
  }

  return sets;
}

/** „40 kg × 5 · 60 kg × 3 · 80 kg × 1". */
export function warmupLabel(sets: readonly WarmupSet[]): string {
  return sets
    .map((set) => `${String(set.weightKg).replace('.', ',')} kg × ${set.reps}`)
    .join(' · ');
}
