import { loadFloorNote } from './load-floor-note';

/**
 * Napomena o podu opterećenja ima dva uzroka i lako se pomešaju: server u oba slučaja
 * vraća istu zastavicu. Tekst je do sada govorio o sopstvenoj masi i za vežbu koja je ne
 * nosi — plank upisan sa 0 kg — pa su ta dva slučaja ovde razdvojena i pokrivena.
 */
describe('loadFloorNote', () => {
  it('bez poda nema napomene', () => {
    expect(loadFloorNote({ loadFloorReached: false, isBodyweight: true })).toBeNull();
    expect(loadFloorNote({ loadFloorReached: false, isBodyweight: false })).toBeNull();
  });

  it('vežba sa telesnom masom je stala na sopstvenoj masi', () => {
    expect(loadFloorNote({ loadFloorReached: true, isBodyweight: true })).toBe('at-bodyweight');
  });

  it('vežba bez telesne mase nije bila opterećena', () => {
    // Plank ili bilo koja vežba upisana sa 0 kg: telo se za nju ne računa, pa rečenica o
    // sopstvenoj masi ne bi bila tačna.
    expect(loadFloorNote({ loadFloorReached: true, isBodyweight: false })).toBe('unloaded');
  });
});
