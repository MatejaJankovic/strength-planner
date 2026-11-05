import { MesocycleDto } from '../../core/models/training.models';
import { staleMaxExercises } from './stale-max-exercises';

/**
 * Napomena o starom maksimumu tvrdi da plan polazi od njega. To je tačno samo dok vežba na
 * tom danu bloka nije odrađena: posle toga opterećenje istog dana dolazi iz dignutih serija.
 */
describe('staleMaxExercises', () => {
  const now = new Date('2026-11-05T12:00:00Z');
  const seventyDaysAgo = '2026-08-27T12:00:00Z';

  const session = (status: string, ...exercises: [string, string][]) => day('Upper A', status, ...exercises);
  const day = (dayLabel: string, status: string, ...exercises: [string, string][]) => ({
    dayLabel,
    status,
    exercisePlans: exercises.map(([exerciseId, exerciseName]) => ({ exerciseId, exerciseName })),
  });
  const plan = (...sessions: ReturnType<typeof day>[]) =>
    ({ weeks: [{ sessions }] }) as unknown as Pick<MesocycleDto, 'weeks'>;

  const stale = (exerciseId: string) => ({
    exerciseId,
    isOutsideLookback: true,
    recordedAt: seventyDaysAgo,
  });

  it('navodi vežbu koja čeka i nije odrađena, sa starošću u nedeljama', () => {
    const result = staleMaxExercises(plan(session('Planned', ['row', 'Barbell Row'])), [stale('row')], now);

    expect(result).toEqual([{ name: 'Barbell Row', weeks: 10 }]);
  });

  it('ne navodi vežbu koja je na tom danu već odrađena', () => {
    // Opterećenje istog dana sledeće nedelje se tada računa iz dignutih serija, ne iz maksimuma.
    const result = staleMaxExercises(
      plan(session('Completed', ['row', 'Barbell Row']), session('Planned', ['row', 'Barbell Row'])),
      [stale('row')],
      now,
    );

    expect(result).toEqual([]);
  });

  it('navodi vežbu koja je odrađena na drugom danu, a na ovom još čeka', () => {
    // Završen Upper A prepisuje samo Upper A sledeće nedelje; Upper B prve nedelje i dalje
    // nosi opterećenje izvedeno iz starog maksimuma.
    const result = staleMaxExercises(
      plan(day('Upper A', 'Completed', ['bench', 'Bench Press']), day('Upper B', 'Planned', ['bench', 'Bench Press'])),
      [stale('bench')],
      now,
    );

    expect(result).toEqual([{ name: 'Bench Press', weeks: 10 }]);
  });

  it('ne navodi vežbu koja stoji samo u preskočenom treningu', () => {
    const result = staleMaxExercises(plan(session('Skipped', ['row', 'Barbell Row'])), [stale('row')], now);

    expect(result).toEqual([]);
  });

  it('svež maksimum ne daje napomenu', () => {
    const result = staleMaxExercises(
      plan(session('Planned', ['row', 'Barbell Row'])),
      [{ exerciseId: 'row', isOutsideLookback: false, recordedAt: seventyDaysAgo }],
      now,
    );

    expect(result).toEqual([]);
  });

  it('sortira po imenu', () => {
    const result = staleMaxExercises(
      plan(session('InProgress', ['row', 'Barbell Row'], ['bench', 'Bench Press'], ['curl', 'Barbell Curl'])),
      [stale('row'), stale('bench'), stale('curl')],
      now,
    );

    expect(result.map((entry) => entry.name)).toEqual(['Barbell Curl', 'Barbell Row', 'Bench Press']);
  });
});
