import { absorbsStep, repTargetFor, repsToEarnStep, stepAbove } from './step-absorption';

/**
 * Ekran prikazuje cilj ponavljanja, a o koraku odlučuje server. Tabela ispod je ista kao u
 * StepAbsorptionTests na serveru (RepsToEarnStep_IsTheTopOfTheRange_...): kad se pravilo
 * menja na jednom mestu, ova tabela je ono što pada na drugom.
 */
describe('step-absorption', () => {
  const serverTable: [number, number, number, number, number, number][] = [
    // opterećenje, korak, dno, vrh, ciljni RIR, očekivan cilj
    [100, 2.5, 8, 12, 1, 12],
    [60, 2.5, 3, 6, 2, 6],
    [20, 2, 8, 12, 1, 12],
    [8, 2, 8, 12, 1, 17],
    [20, 5, 8, 12, 1, 17],
    [12.5, 2.5, 8, 12, 1, 15],
    [30, 2.5, 11, 12, 1, 14],
    [100, 2.5, 11, 12, 2, 12],
    [30, 2.5, 11, 12, 2, 12],
    [100, 2.5, 5, 5, 2, 5],
    [40, 2.5, 5, 5, 2, 5],
    [60, 2.5, 5, 5, 1, 5],
  ];

  it.each(serverTable)(
    'daje isti cilj kao server: %s kg, korak %s, %s-%s @RIR%s -> %s',
    (load, step, min, max, rir, expected) => {
      expect(repsToEarnStep(load, step, min, max, rir)).toBe(expected);
    },
  );

  it('meri kapacitet kao server: ponavljanja plus rezerva naspram dna posle koraka', () => {
    // 8 kg, korak 2: potrebno 17.5 efektivnih.
    expect(absorbsStep(8, 2, 8, 17, 1)).toBe(true);
    expect(absorbsStep(8, 2, 8, 17, 0)).toBe(false);
    expect(absorbsStep(8, 2, 8, 15, 3)).toBe(true);
  });

  it('sledeća težina na mreži je ista kao WeightMath.StepAbove', () => {
    expect(stepAbove(10, 2)).toBe(12);
    expect(stepAbove(9, 2)).toBe(10);
    expect(stepAbove(15, 2)).toBe(16);
    expect(stepAbove(22.5, 5)).toBe(25);
    expect(stepAbove(100, 2.5)).toBe(102.5);
  });

  describe('repTargetFor', () => {
    const lateralRaise = { repRangeMin: 8, repRangeMax: 12, targetRir: 1, weightStepKg: 2, bodyweightLoadKg: 0 };

    it('računa iz težine koja se unosi', () => {
      expect(repTargetFor(lateralRaise, 8, false)).toBe(17);
      expect(repTargetFor(lateralRaise, 10, false)).toBe(15);
      expect(repTargetFor(lateralRaise, 16, false)).toBe(12);
    });

    it('u deload nedelji je cilj vrh opsega', () => {
      // Posle deload-a se nastavlja od težine zarađene pre njega: produžen cilj bi samo
      // terao ponavljanja naviše u nedelji odmora.
      expect(repTargetFor(lateralRaise, 8, true)).toBe(12);
    });

    it('telo je deo tereta', () => {
      // Zgib +5 kg uz 80 kg tela, korak 1 kg: 85 kg ukupno, korak staje (cilj 12). Samo
      // dodatih 5 kg bi dalo cilj 15 - tako bi izgledala greška koja telo ne uračuna.
      const pullUp = { repRangeMin: 8, repRangeMax: 12, targetRir: 1, weightStepKg: 1, bodyweightLoadKg: 80 };
      expect(repTargetFor(pullUp, 5, false)).toBe(12);
      expect(repsToEarnStep(5, 1, 8, 12, 1)).toBe(15);
    });
  });
});
