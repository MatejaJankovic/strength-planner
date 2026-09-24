import { loadLabel } from './load-label';

/**
 * Kako se opterećenje piše kada vežbu opterećuje telo. Postoji zato što je isti broj
 * na dva ekrana bio ispisan na dva načina: „0 kg" u listi serija, a „Sledeće 0 kg" u
 * rezimeu — oba su značila „sopstvenom masom", i ni jedno to nije reklo.
 */
describe('loadLabel', () => {
  it('vežbu sa spoljnim opterećenjem piše kao i pre', () => {
    expect(loadLabel(100, false)).toBe('100 kg');
    expect(loadLabel(0, false)).toBe('0 kg');
    expect(loadLabel(2.5, false)).toBe('2.5 kg');
  });

  it('nula dodatnih kilograma je sopstvena masa', () => {
    expect(loadLabel(0, true)).toBe('TM');
  });

  it('dodato opterećenje stoji uz telesnu masu', () => {
    expect(loadLabel(5, true)).toBe('TM + 5 kg');
    expect(loadLabel(17.5, true)).toBe('TM + 17.5 kg');
  });
});
