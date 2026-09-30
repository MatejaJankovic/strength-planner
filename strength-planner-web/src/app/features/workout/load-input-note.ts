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

const BODYWEIGHT = 'Bodyweight';
const DUMBBELL = 'Dumbbell';
const BARBELL = 'Barbell';

export function loadInputNote(equipment: string | undefined, weightKg: number): LoadInputNote {
  if (!equipment || equipment === BODYWEIGHT) {
    return null;
  }

  // Nula je hitnija poruka od podsetnika na jednu bučicu, pa ide prva.
  if (weightKg <= 0) {
    return equipment === BARBELL ? 'zero-on-barbell' : 'zero-on-loaded';
  }

  return equipment === DUMBBELL ? 'single-dumbbell' : null;
}
