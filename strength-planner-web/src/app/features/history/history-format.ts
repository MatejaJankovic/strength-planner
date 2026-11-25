import { HistoryEntryDto } from '../../core/models/training.models';

const MONTHS = [
  'Januar',
  'Februar',
  'Mart',
  'April',
  'Maj',
  'Jun',
  'Jul',
  'Avgust',
  'Septembar',
  'Oktobar',
  'Novembar',
  'Decembar',
];

const MONTHS_SHORT = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'avg', 'sep', 'okt', 'nov', 'dec'];
const WEEKDAYS_SHORT = ['ned', 'pon', 'uto', 'sre', 'čet', 'pet', 'sub'];

/**
 * Oblikovanje istorije treninga. Nazivi meseci su ovde, a ne u `Intl`, jer ekran mora da piše isto
 * na svakom telefonu: podrška za srpski sa latiničnim pismom nije svuda ista.
 */

/** „Novembar 2026": zaglavlje meseca nad spiskom. */
export function monthHeading(iso: string): string {
  const date = new Date(iso);
  return `${MONTHS[date.getMonth()]} ${date.getFullYear()}`;
}

/** „sre, 12. nov": dan treninga. */
export function dayLabel(iso: string): string {
  const date = new Date(iso);
  return `${WEEKDAYS_SHORT[date.getDay()]}, ${date.getDate()}. ${MONTHS_SHORT[date.getMonth()]}`;
}

/**
 * Kad tačan dan nije poznat (preskočen trening, ili stari trening bez ijedne serije), ne piše se
 * ništa: datum koji bi bio izveden iz plana izgledao bi kao činjenica, a nedelja već stoji u
 * podnaslovu.
 */
export function whenLabel(entry: Pick<HistoryEntryDto, 'date' | 'dateKnown'>): string {
  return entry.dateKnown ? dayLabel(entry.date) : '';
}

/** „21 trening", „22 treninga": broj određuje oblik po srpskim pravilima, ne samo jedinica. */
export function countLabel(count: number): string {
  const singular = count % 10 === 1 && count % 100 !== 11;
  return `${count} ${singular ? 'trening' : 'treninga'}`;
}

/** „4 380 kg", sa nerazdvojivim razmakom u hiljadama da broj ne pukne na dva reda. */
export function tonnageLabel(kg: number): string {
  return `${Math.round(kg)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, ' ')} kg`;
}

/** „Bench Press, Overhead Press +2": prvih nekoliko vežbi, ostalo se broji. */
export function exercisesLabel(names: readonly string[], shown = 3): string {
  if (names.length <= shown) {
    return names.join(', ');
  }

  return `${names.slice(0, shown).join(', ')} +${names.length - shown}`;
}

export interface HistoryMonth {
  heading: string;
  entries: HistoryEntryDto[];
}

/**
 * Spisak je već poređan od najnovijeg; mesec se menja kad se menja datum. Poređanje se ne
 * ponavlja ovde, jer bi dve nezavisne odluke o redosledu mogle da se razilaze.
 */
export function groupByMonth(entries: readonly HistoryEntryDto[]): HistoryMonth[] {
  const months: HistoryMonth[] = [];

  for (const entry of entries) {
    const heading = monthHeading(entry.date);
    const last = months[months.length - 1];

    if (last && last.heading === heading) {
      last.entries.push(entry);
    } else {
      months.push({ heading, entries: [entry] });
    }
  }

  return months;
}
