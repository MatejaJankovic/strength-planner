import { setFeedback } from './set-feedback';

/**
 * Napomena ispod unosa serije je obećanje o sledećem treningu, pa mora da prati pravilo
 * sa servera. Stara napomena je za otkaz na vrhu opsega tvrdila „zadržava se", a server
 * je iznad ~125 kg spuštao težinu - to se na snimku ekrana ne vidi.
 */
describe('setFeedback', () => {
  const hypertrophy = { repRangeMin: 8, repRangeMax: 12, targetRir: 1 };
  const narrowVolumeWeek = { repRangeMin: 11, repRangeMax: 12, targetRir: 2 };

  it('najavljuje korak za otkaz na vrhu običnog opsega', () => {
    expect(setFeedback(hypertrophy, { reps: 12, rir: 0, isFailure: true })).toBe('failure-at-top');
  });

  it('najavljuje zadržavanje za otkaz na vrhu uskog opsega sa većim ciljnim RIR-om', () => {
    expect(setFeedback(narrowVolumeWeek, { reps: 12, rir: 0, isFailure: true })).toBe(
      'failure-at-top-narrow',
    );
  });

  it('prepoznaje otkaz ispod i unutar opsega', () => {
    expect(setFeedback(hypertrophy, { reps: 6, rir: 0, isFailure: true })).toBe('failure-below-range');
    expect(setFeedback(hypertrophy, { reps: 10, rir: 0, isFailure: true })).toBe('failure-in-range');
  });

  it('seriju ispod dna sa rezervom čita kao težu od plana kad kapacitet ne dostiže cilj', () => {
    // 5 + 2 = 7 < 8 + 1
    expect(setFeedback(hypertrophy, { reps: 5, rir: 2, isFailure: false })).toBe(
      'below-range-with-reserve',
    );
  });

  it('ne upozorava kad je kapacitet ispod dna dovoljan', () => {
    // 6 + 3 = 9 = 8 + 1: tačno propisano opterećenje.
    expect(setFeedback(hypertrophy, { reps: 6, rir: 3, isFailure: false })).toBeNull();
  });

  it('RIR 0 ispod dna bez kvačice tretira kao otkaz', () => {
    expect(setFeedback(hypertrophy, { reps: 6, rir: 0, isFailure: false })).toBe(
      'below-range-no-reserve',
    );
  });

  it('ćuti za obične serije unutar opsega', () => {
    expect(setFeedback(hypertrophy, { reps: 10, rir: 1, isFailure: false })).toBeNull();
    expect(setFeedback(hypertrophy, { reps: 12, rir: 0, isFailure: false })).toBeNull();
  });
});
