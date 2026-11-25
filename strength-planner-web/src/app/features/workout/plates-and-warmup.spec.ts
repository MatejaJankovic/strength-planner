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

  it('težina koja nije broj nije prazna šipka', () => {
    // NaN nije manji od šipke, pa bi `totalKg < barKg` propustilo ovo kao „prazna šipka".
    expect(platesFor(Number.NaN).belowBar).toBe(true);
  });

  it('beskonačna težina se odbija, umesto da petlja ploča traje zauvek', () => {
    expect(platesFor(Number.POSITIVE_INFINITY)).toEqual({ plates: [], remainderPerSideKg: 0, belowBar: true });
    expect(platesFor(Number.NEGATIVE_INFINITY).belowBar).toBe(true);
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

  it('male ploče se sabiraju tačno (47.5: po strani 13.75 = 10 + 2.5 + 1.25)', () => {
    expect(platesFor(47.5)).toEqual({ plates: [10, 2.5, 1.25], remainderPerSideKg: 0, belowBar: false });
  });

  it('korak koji nije stepen dvojke ne ostavlja šum pokretnog zareza u ostatku', () => {
    // 54.3 kg (korak 2.33 daje ovakve težine): po strani 17.15 = 15 + 1.25, ostaje 0.9.
    // Bez zaokruživanja ostatak je 0.8999999999999986 i tako se ispisuje.
    const load = platesFor(54.3);

    expect(load.plates).toEqual([15, 1.25]);
    expect(load.remainderPerSideKg).toBe(0.9);
    expect(platesLabel(load)).toContain('0,9 kg po strani');
  });

  it('drugi skup ploča (bez 1.25) menja rezultat', () => {
    const load = platesFor(82.5, BAR_KG, [25, 20, 15, 10, 5, 2.5]);

    expect(load.plates).toEqual([25, 5]);
    expect(load.remainderPerSideKg).toBe(1.25);
  });
});

describe('platesLabel', () => {
  it('piše ploče sa zarezom kao decimalnim znakom, uz šipku na kojoj se računa', () => {
    expect(platesLabel(platesFor(82.5))).toBe('Po strani (šipka 20 kg): 25 + 5 + 1,25.');
  });

  it('prazna šipka i ostatak imaju svaki svoju poruku', () => {
    expect(platesLabel(platesFor(20))).toBe('Prazna šipka (20 kg).');
    expect(platesLabel(platesFor(61))).toContain('0,5 kg po strani se ne može sastaviti');
  });

  it('težina manja od šipke se ne komentariše: aplikacija ne zna koju šipku lifter ima', () => {
    expect(platesLabel(platesFor(10))).toBeNull();
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

  it('ni zaokruživanje na veliki korak ne sme da da težinu koja nije ispod radne', () => {
    // 80% od 90 kg je 72, a korak od 100 kg to zaokružuje na 100: preko radne težine.
    const sets = warmupSets(90, 100);

    expect(sets.every((set) => set.weightKg < 90)).toBe(true);
  });

  it('korak koji nije stepen dvojke ne ostavlja šum pokretnog zareza u težinama', () => {
    // Korak je bilo koja vrednost sa dve decimale; 0.1 i 2.33 daju 24.400000000000002 i slično.
    for (const step of [0.1, 0.2, 0.3, 0.6, 0.7, 1.1, 1.2, 1.4, 2.3, 2.33, 3.3]) {
      for (let working = 25; working <= 300; working += 0.5) {
        for (const set of warmupSets(working, step)) {
          expect(set.weightKg, `${working} kg, korak ${step}`).toBe(Number(set.weightKg.toFixed(2)));
        }
      }
    }
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
  it('piše ploče samo za složenu vežbu sa šipkom', () => {
    expect(platesHint('Barbell', 'Compound', 100)).toBe('Po strani (šipka 20 kg): 25 + 15.');
    expect(platesHint('Dumbbell', 'Compound', 100)).toBeNull();
    expect(platesHint('Machine', 'Compound', 100)).toBeNull();
    expect(platesHint(undefined, 'Compound', 100)).toBeNull();
  });

  it('oprema se poredi bez obzira na velika slova i razmake, kao na serveru', () => {
    // Vežba napravljena preko API-ja čuva se onako kako je uneta.
    expect(platesHint('barbell', 'Compound', 100)).toBe('Po strani (šipka 20 kg): 25 + 15.');
    expect(platesHint(' BARBELL ', 'Compound', 100)).toBe('Po strani (šipka 20 kg): 25 + 15.');
    expect(warmupHint('barbell', 'Compound', 100, 2.5, 0)).not.toBeNull();
  });

  it('ćuti za pregib i skull crusher: šipka tu nije od 20 kg', () => {
    expect(platesHint('Barbell', 'Isolation', 25)).toBeNull();
  });

  it('ćuti kad se tip ne zna (putanja koja ga ne šalje)', () => {
    expect(platesHint('Barbell', undefined, 100)).toBeNull();
    expect(platesHint('Barbell', '', 100)).toBeNull();
  });

  it('ćuti dok težina nije uneta ili je manja od šipke', () => {
    expect(platesHint('Barbell', 'Compound', 0)).toBeNull();
    expect(platesHint('Barbell', 'Compound', 17.5)).toBeNull();
    expect(platesHint('Barbell', 'Compound', Number.NaN)).toBeNull();
  });
});

describe('warmupHint', () => {
  it('predlaže zagrevanje za složenu vežbu sa šipkom pre prve radne serije', () => {
    expect(warmupHint('Barbell', 'Compound', 100, 2.5, 0)).toBe('40 kg × 5 · 60 kg × 3 · 80 kg × 1');
  });

  it('ćuti kad je serija već odrađena', () => {
    expect(warmupHint('Barbell', 'Compound', 100, 2.5, 1)).toBeNull();
  });

  it('ćuti za vežbu bez šipke i za izolaciju sa šipkom', () => {
    expect(warmupHint('Dumbbell', 'Compound', 100, 2.5, 0)).toBeNull();
    expect(warmupHint('Barbell', 'Isolation', 100, 2.5, 0)).toBeNull();
    expect(warmupHint('Barbell', undefined, 100, 2.5, 0)).toBeNull();
  });

  it('ćuti kad nema šta da se predloži', () => {
    expect(warmupHint('Barbell', 'Compound', 0, 2.5, 0)).toBeNull();
    expect(warmupHint('Barbell', 'Compound', 20, 2.5, 0)).toBeNull();
  });
});
