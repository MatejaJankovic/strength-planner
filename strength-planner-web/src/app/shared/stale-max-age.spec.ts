import { staleMaxAgeWeeks, weeksLabel } from './stale-max-age';

/**
 * Starost maksimuma se prikazuje samo kad je server rekao da je zapis izvan prozora. Ovde
 * se ne odlučuje da li je zapis star - samo koliko nedelja piše na ekranu.
 */
describe('staleMaxAgeWeeks', () => {
  const now = new Date('2026-11-05T12:00:00Z');

  it('svež maksimum nema starost na ekranu', () => {
    expect(staleMaxAgeWeeks({ isOutsideLookback: false, recordedAt: '2026-01-01T00:00:00Z' }, now)).toBeNull();
    expect(staleMaxAgeWeeks({ recordedAt: '2026-01-01T00:00:00Z' }, now)).toBeNull();
  });

  it('broji pune nedelje, naniže', () => {
    // 62 dana: osam punih nedelja i šest dana.
    expect(staleMaxAgeWeeks({ isOutsideLookback: true, recordedAt: '2026-09-04T12:00:00Z' }, now)).toBe(8);
    expect(staleMaxAgeWeeks({ isOutsideLookback: true, recordedAt: '2026-08-27T12:00:00Z' }, now)).toBe(10);
  });
});

describe('weeksLabel', () => {
  it('slaže broj i padež', () => {
    expect(weeksLabel(1)).toBe('1 nedelju');
    expect(weeksLabel(3)).toBe('3 nedelje');
    expect(weeksLabel(8)).toBe('8 nedelja');
    expect(weeksLabel(11)).toBe('11 nedelja');
    expect(weeksLabel(12)).toBe('12 nedelja');
    expect(weeksLabel(21)).toBe('21 nedelju');
    expect(weeksLabel(22)).toBe('22 nedelje');
  });
});
