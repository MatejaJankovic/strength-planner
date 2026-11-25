import { HistoryEntryDto } from '../../core/models/training.models';
import {
  dayLabel,
  exercisesLabel,
  groupByMonth,
  monthHeading,
  tonnageLabel,
  whenLabel,
} from './history-format';

function entry(date: string, overrides: Partial<HistoryEntryDto> = {}): HistoryEntryDto {
  return {
    sessionId: date,
    status: 'Completed',
    date,
    dateKnown: true,
    dayLabel: 'Day A',
    weekNumber: 1,
    isDeload: false,
    planName: 'Plan',
    blockName: 'Blok',
    goal: 'Strength',
    setCount: 10,
    tonnageKg: 1000,
    exercises: [],
    ...overrides,
  };
}

describe('history formatting', () => {
  // Podne po lokalnom vremenu: mesec i dan ne zavise od vremenske zone na kojoj se test pokreće.
  const NOV_12 = new Date(2026, 10, 12, 12).toISOString();
  const DEC_1 = new Date(2026, 11, 1, 12).toISOString();

  it('mesec i dan se pišu na srpskom', () => {
    expect(monthHeading(NOV_12)).toBe('Novembar 2026');
    expect(dayLabel(NOV_12)).toBe('čet, 12. nov');
  });

  it('nepoznat dan se ne piše kao datum', () => {
    expect(whenLabel({ date: NOV_12, dateKnown: true, weekNumber: 2 })).toBe('čet, 12. nov');
    expect(whenLabel({ date: NOV_12, dateKnown: false, weekNumber: 3 })).toBe('nedelja 3');
  });

  it('tonaža ima razmak u hiljadama koji ne puca na dva reda', () => {
    expect(tonnageLabel(950)).toBe('950 kg');
    expect(tonnageLabel(4380.4)).toBe('4 380 kg');
    expect(tonnageLabel(1234567)).toBe('1 234 567 kg');
  });

  it('vežbe se skraćuju na prve tri, a ostale se broje', () => {
    expect(exercisesLabel(['A', 'B'])).toBe('A, B');
    expect(exercisesLabel(['A', 'B', 'C'])).toBe('A, B, C');
    expect(exercisesLabel(['A', 'B', 'C', 'D', 'E'])).toBe('A, B, C +2');
    expect(exercisesLabel([])).toBe('');
  });

  it('grupisanje čuva redosled i pravi novu grupu tek kad se mesec promeni', () => {
    const NOV_10 = new Date(2026, 10, 10, 12).toISOString();
    const months = groupByMonth([entry(DEC_1), entry(NOV_12), entry(NOV_10)]);

    expect(months.map((month) => month.heading)).toEqual(['Decembar 2026', 'Novembar 2026']);
    expect(months.map((month) => month.entries.length)).toEqual([1, 2]);
  });

  it('prazan spisak nema nijedan mesec', () => {
    expect(groupByMonth([])).toEqual([]);
  });
});
