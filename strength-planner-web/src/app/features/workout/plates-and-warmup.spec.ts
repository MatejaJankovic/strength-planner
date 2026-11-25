import { BAR_KG, platesFor, platesLabel } from './plates';
import { warmupLabel, warmupSets } from './warmup';
import { platesHint, warmupHint } from './workout-aids';

describe('platesFor', () => {
  it('ploče sa jedne strane za težinu iz celih ploča', () => {
    // 100 kg: šipka 20, ostaje 80, po 40 sa strane = 25 + 15.
    expect(platesFor(100).plates).toEqual([25, 15]);
    expect(platesFor(60).plates).toEqual([20]);
  });

  it('gramzivo od najteže, uz male ploče na kraju', () => {
    // (82.5 − 20) / 2 = 31.25 = 25 + 5 + 1.25.
    const load = platesFor(82.5);

    expect(load.plates).toEqual([25, 5, 1.25]);
    expect(load.remainderPerSideKg).toBe(0);
  });

  it('prazna šipka i težina manja od šipke se razlikuju', () => {
    expect(platesFor(BAR_KG)).toEqual({ plates: [], remainderPerSideKg: 0, belowBar: false });
    expect(platesFor(15).belowBar).toBe(true);
  });

  it('težina koja se ne može sastaviti vraća ostatak, umesto da se tiho zaokruži', () => {
    // 61 kg: po strani 20.5; 20 + ostaje 0.5 koje ne postoji kao ploča.
    const load = platesFor(61);

    expect(load.plates).toEqual([20]);
    expect(load.remainderPerSideKg).toBe(0.5);
  });

  it('zbir ploča i šipke daje traženu težinu za svaku težinu koja se može sastaviti', () => {
    for (let total = BAR_KG; total <= 300; total += 2.5) {
      const load = platesFor(total);
      const built = BAR_KG + 2 * load.plates.reduce((sum, plate) => sum + plate, 0);

      expect(load.remainderPerSideKg, `${total} kg`).toBe(0);
      expect(built, `${total} kg`).toBeCloseTo(total, 5);
    }
  });

  it('pokretni zarez ne kvari zbir malih ploča (2.5 + 1.25)', () => {
    // 47.5: po strani 13.75 = 10 + 2.5 + 1.25.
    expect(platesFor(47.5)).toEqual({ plates: [10, 2.5, 1.25], remainderPerSideKg: 0, belowBar: false });
  });

  it('drugi skup ploča (bez 1.25) menja rezultat', () => {
    const load = platesFor(82.5, BAR_KG, [25, 20, 15, 10, 5, 2.5]);

    expect(load.plates).toEqual([25, 5]);
    expect(load.remainderPerSideKg).toBe(1.25);
  });
});

describe('platesLabel', () => {
  it('piše ploče sa zarezom kao decimalnim znakom', () => {
    expect(platesLabel(platesFor(82.5))).toBe('Po strani: 25 + 5 + 1,25.');
  });

  it('prazna šipka, manje od šipke i ostatak imaju svaki svoju poruku', () => {
    expect(platesLabel(platesFor(20))).toBe('Prazna šipka (20 kg).');
    expect(platesLabel(platesFor(10))).toBe('Manje od same šipke (20 kg).');
    expect(platesLabel(platesFor(61))).toContain('0,5 kg po strani se ne može sastaviti');
  });
});

describe('warmupSets', () => {
  it('rampa od 40%, 60% i 80% radne težine, zaokružena na korak', () => {
    // 100 kg, korak 2.5: 40, 60, 80.
    expect(warmupSets(100, 2.5)).toEqual([
      { weightKg: 40, reps: 5 },
      { weightKg: 60, reps: 3 },
      { weightKg: 80, reps: 1 },
    ]);
  });

  it('težina se zaokružuje na korak vežbe, ne na ceo broj', () => {
    // 90 × 0.4 = 36 -> 35 (36 / 2.5 = 14.4 koraka, zaokruženo na 14); 54 -> 55; 72 -> 72.5.
    expect(warmupSets(90, 2.5).map((set) => set.weightKg)).toEqual([35, 55, 72.5]);
  });

  it('težine rastu, ponavljanja padaju, i nijedna nije radna težina', () => {
    for (const working of [45, 60, 80, 100, 140, 200]) {
      const sets = warmupSets(working, 2.5);

      for (let index = 1; index < sets.length; index++) {
        expect(sets[index].weightKg).toBeGreaterThan(sets[index - 1].weightKg);
        expect(sets[index].reps).toBeLessThan(sets[index - 1].reps);
      }
      expect(sets.every((set) => set.weightKg < working)).toBe(true);
      expect(sets.every((set) => set.weightKg >= BAR_KG)).toBe(true);
    }
  });

  it('radna težina blizu šipke daje samo praznu šipku, a na šipki ili ispod nje ništa', () => {
    expect(warmupSets(25, 2.5)).toEqual([{ weightKg: BAR_KG, reps: 8 }]);
    expect(warmupSets(BAR_KG, 2.5)).toEqual([]);
    expect(warmupSets(15, 2.5)).toEqual([]);
  });

  it('serija koja bi bila jednaka prethodnoj se izbacuje', () => {
    // 35 kg: 40% = 14 -> klamp na šipku 20; 60% = 21 -> 20 (isto, izbačeno); 80% = 28 -> 27.5.
    expect(warmupSets(35, 2.5)).toEqual([
      { weightKg: 20, reps: 5 },
      { weightKg: 27.5, reps: 1 },
    ]);
  });

  it('nevažan korak ili težina ne daju zagrevanje', () => {
    expect(warmupSets(100, 0)).toEqual([]);
    expect(warmupSets(Number.NaN, 2.5)).toEqual([]);
  });
});

describe('warmupLabel', () => {
  it('serije razdvaja tačkom i piše zarezom', () => {
    expect(warmupLabel([{ weightKg: 37.5, reps: 5 }, { weightKg: 55, reps: 3 }])).toBe('37,5 kg × 5 · 55 kg × 3');
  });
});

describe('platesHint', () => {
  it('piše ploče samo za vežbu sa šipkom', () => {
    expect(platesHint('Barbell', 100)).toBe('Po strani: 25 + 15.');
    expect(platesHint('Dumbbell', 100)).toBeNull();
    expect(platesHint('Machine', 100)).toBeNull();
    expect(platesHint(undefined, 100)).toBeNull();
  });

  it('ćuti dok težina nije uneta', () => {
    expect(platesHint('Barbell', 0)).toBeNull();
    expect(platesHint('Barbell', Number.NaN)).toBeNull();
  });
});

describe('warmupHint', () => {
  it('predlaže zagrevanje za vežbu sa šipkom pre prve radne serije', () => {
    expect(warmupHint('Barbell', 100, 2.5, 0)).toBe('40 kg × 5 · 60 kg × 3 · 80 kg × 1');
  });

  it('ćuti kad je serija već odrađena, kad vežba nije sa šipkom i kad nema šta da se predloži', () => {
    expect(warmupHint('Barbell', 100, 2.5, 1)).toBeNull();
    expect(warmupHint('Dumbbell', 100, 2.5, 0)).toBeNull();
    expect(warmupHint('Barbell', 0, 2.5, 0)).toBeNull();
    expect(warmupHint('Barbell', 20, 2.5, 0)).toBeNull();
  });
});
