import { repRangeLabel } from './rep-range-label';

/**
 * Propis od tačno pet ponavljanja je pisalo „5–5" na tri ekrana. Broj je bio tačan, a
 * tekst je govorio o opsegu koga nema.
 */
describe('repRangeLabel', () => {
  it('opseg piše sa crticom', () => {
    expect(repRangeLabel(8, 12)).toBe('8–12');
    expect(repRangeLabel(3, 6)).toBe('3–6');
    expect(repRangeLabel(11, 12)).toBe('11–12');
  });

  it('fiksan broj ponavljanja piše kao jedan broj', () => {
    expect(repRangeLabel(5, 5)).toBe('5');
    expect(repRangeLabel(12, 12)).toBe('12');
  });
});
