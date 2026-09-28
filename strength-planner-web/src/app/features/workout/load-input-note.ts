/**
 * Napomene uz polje za težinu koje iz samog broja ne slede.
 *
 * Dve su, i obe su nalazi iz pregleda modela vežbi:
 *
 * - Kod bučica se unosi težina **jedne**. Nigde nije bilo rečeno, a odluka postoji i vidi
 *   se u koraku: 2 kg je pomak jedne bučice na stalku (10, 12, 14…), dok par ide po 4 kg.
 *   Vežbač koji unese zbir dobija predlog koji na stalku ne postoji.
 * - Nula kilograma kod vežbe koja se opterećuje spolja nije stanje nego greška: prazna
 *   šipka je oko 20 kg. Serija upisana na nuli ne daje ni procenu maksimuma ni tonažu, a
 *   progresija iz nje ne može da izvede sledeće opterećenje.
 *
 * Vežba koju nosi sopstvena masa je izuzeta iz obe: nula tamo znači „sopstvenom masom", što
 * je tačan unos — a plank se i loguje na nuli.
 */
export type LoadInputNote = 'single-dumbbell' | 'zero-on-loaded' | null;

const BODYWEIGHT = 'Bodyweight';
const DUMBBELL = 'Dumbbell';

export function loadInputNote(equipment: string | undefined, weightKg: number): LoadInputNote {
  if (!equipment || equipment === BODYWEIGHT) {
    return null;
  }

  // Nula je hitnija poruka od podsetnika na jednu bučicu, pa ide prva.
  if (weightKg <= 0) {
    return 'zero-on-loaded';
  }

  return equipment === DUMBBELL ? 'single-dumbbell' : null;
}
