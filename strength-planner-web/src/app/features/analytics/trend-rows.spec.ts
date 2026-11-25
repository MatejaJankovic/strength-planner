import { TrendDto, TrendWeekDto } from '../../core/models/analytics.models';
import { metricFromKey, metricKey, metricValue, trendRows, weekLabel } from './trend-rows';

function week(start: string, overrides: Partial<TrendWeekDto> = {}): TrendWeekDto {
  return {
    weekStart: start,
    tonnageKg: 0,
    sets: 0,
    stimulativeSets: 0,
    stimulativeSetsByMuscle: {},
    blockName: null,
    planName: null,
    isDeload: false,
    blockStarts: false,
    ...overrides,
  };
}

describe('trend rows', () => {
  const NOV_23 = '2026-11-23T00:00:00Z';

  it('ponedeljak nedelje se piše bez godine, po UTC-u kao i na serveru', () => {
    expect(weekLabel(NOV_23)).toBe('23. nov');
    expect(weekLabel('2026-12-07T00:00:00Z')).toBe('7. dec');
  });

  it('mera bira šta se čita iz nedelje', () => {
    const w = week(NOV_23, {
      tonnageKg: 4380,
      stimulativeSets: 12.5,
      stimulativeSetsByMuscle: { Chest: 6, Back: 4.5 },
    });

    expect(metricValue(w, { kind: 'tonnage' })).toBe(4380);
    expect(metricValue(w, { kind: 'sets' })).toBe(12.5);
    expect(metricValue(w, { kind: 'muscle', muscle: 'Chest' })).toBe(6);
    // Mišić koji se te nedelje nije trenirao nije izostavljen, nego nula.
    expect(metricValue(w, { kind: 'muscle', muscle: 'Quads' })).toBe(0);
  });

  it('izbor u listi prolazi kroz ključ i nazad, i za mišić sa razmakom ili dvotačkom', () => {
    for (const metric of [
      { kind: 'tonnage' } as const,
      { kind: 'sets' } as const,
      { kind: 'muscle', muscle: 'Chest' } as const,
      { kind: 'muscle', muscle: 'Front Delt: head' } as const,
    ]) {
      expect(metricFromKey(metricKey(metric))).toEqual(metric);
    }

    expect(metricFromKey('nešto-nepoznato')).toEqual({ kind: 'tonnage' });
  });

  it('traka je srazmerna najvećoj nedelji u prikazanom opsegu', () => {
    const trend: TrendDto = {
      muscles: [],
      weeks: [
        week(NOV_23, { tonnageKg: 1000, sets: 4 }),
        week('2026-11-30T00:00:00Z', { tonnageKg: 4000, sets: 10 }),
        week('2026-12-07T00:00:00Z', { tonnageKg: 2000, sets: 6 }),
      ],
    };

    expect(trendRows(trend, { kind: 'tonnage' }).map((row) => row.barPct)).toEqual([25, 100, 50]);
  });

  it('nedelja bez treninga je prazan red, a ne traka od nule koja izgleda kao mala nedelja', () => {
    const trend: TrendDto = {
      muscles: [],
      weeks: [week(NOV_23, { tonnageKg: 1000, sets: 4 }), week('2026-11-30T00:00:00Z')],
    };

    const rows = trendRows(trend, { kind: 'tonnage' });

    expect(rows.map((row) => row.isEmpty)).toEqual([false, true]);
    expect(rows[1].barPct).toBe(0);
  });

  it('opseg bez ijedne serije nema traku (nema deljenja nulom)', () => {
    const trend: TrendDto = { muscles: [], weeks: [week(NOV_23), week('2026-11-30T00:00:00Z')] };

    expect(trendRows(trend, { kind: 'tonnage' }).map((row) => row.barPct)).toEqual([0, 0]);
  });

  it('naslov bloka stoji samo u nedelji u kojoj blok počinje, sa planom kad se razlikuje', () => {
    const trend: TrendDto = {
      muscles: [],
      weeks: [
        week(NOV_23, { sets: 3, blockName: 'Blok 1', planName: 'Plan jesen', blockStarts: true }),
        week('2026-11-30T00:00:00Z', { sets: 3, blockName: 'Blok 1', planName: 'Plan jesen' }),
        week('2026-12-07T00:00:00Z', { sets: 3, blockName: 'Blok 2', planName: 'Blok 2', blockStarts: true }),
      ],
    };

    expect(trendRows(trend, { kind: 'sets' }).map((row) => row.blockBanner)).toEqual([
      'Blok 1 · Plan jesen',
      null,
      'Blok 2',
    ]);
  });

  it('deload nedelja nosi oznaku', () => {
    const trend: TrendDto = {
      muscles: [],
      weeks: [week(NOV_23, { sets: 1, isDeload: true }), week('2026-11-30T00:00:00Z', { sets: 3 })],
    };

    expect(trendRows(trend, { kind: 'sets' }).map((row) => row.isDeload)).toEqual([true, false]);
  });
});
