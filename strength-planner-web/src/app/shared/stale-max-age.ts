import { OneRepMaxDto } from '../core/models/analytics.models';

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Koliko je nedelja star maksimum koji je server označio kao stariji od prozora, ili null
 * kad je svež.
 *
 * Plan i posle pauze polazi od poslednjeg poznatog maksimuma, da blok ne krene od praznih
 * opterećenja. Do sada to nije pisalo nigde, a broj opisuje vežbača od pre pauze. Procenat
 * pada se ne izmišlja: literatura ga ne daje kao broj koji bi se mogao oduzeti, pa ekran
 * kaže samo koliko je zapis star.
 *
 * Da li je zapis star odlučuje server (`isOutsideLookback`); ovde se samo broje nedelje
 * za tekst, zaokruženo naniže, jer „8 nedelja" za zapis od 62 dana nije preterivanje.
 */
export function staleMaxAgeWeeks(
  record: Pick<OneRepMaxDto, 'isOutsideLookback' | 'recordedAt'>,
  now: Date,
): number | null {
  if (!record.isOutsideLookback) {
    return null;
  }

  const days = (now.getTime() - new Date(record.recordedAt).getTime()) / DAY_MS;
  return Math.max(1, Math.floor(days / 7));
}

/** „1 nedelju", „3 nedelje", „8 nedelja" - oblik koji ide posle „star". */
export function weeksLabel(weeks: number): string {
  const lastTwo = weeks % 100;
  const last = weeks % 10;
  if (last === 1 && lastTwo !== 11) {
    return `${weeks} nedelju`;
  }
  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) {
    return `${weeks} nedelje`;
  }
  return `${weeks} nedelja`;
}
