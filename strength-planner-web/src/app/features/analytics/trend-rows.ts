import { TrendDto, TrendWeekDto } from '../../core/models/analytics.models';

/** Šta se meri po nedelji: tonaža, sve serije, ili serije jednog mišića. */
export type TrendMetric = { kind: 'tonnage' } | { kind: 'sets' } | { kind: 'muscle'; muscle: string };

export interface TrendRange {
  label: string;
  /** Koliko nedelja unazad od tekuće; 0 je cela istorija. */
  weeks: number;
}

export const TREND_RANGES: readonly TrendRange[] = [
  { label: '8 nedelja', weeks: 8 },
  { label: '6 meseci', weeks: 26 },
  { label: 'Godinu dana', weeks: 52 },
  { label: 'Sve', weeks: 0 },
];

const MONTHS_SHORT = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'avg', 'sep', 'okt', 'nov', 'dec'];

export interface TrendRow {
  weekStart: string;
  /** „23. nov": ponedeljak nedelje. */
  label: string;
  value: number;
  /** Širina trake, 0 do 100, prema najvećoj vrednosti u prikazanom opsegu. */
  barPct: number;
  isDeload: boolean;
  /** Nedelja bez ijedne serije: praznina je podatak, pa se vidi kao praznina. */
  isEmpty: boolean;
  /** Naslov bloka iznad reda, kad u toj nedelji počinje novi blok. */
  blockBanner: string | null;
}

/** Ponedeljak nedelje, bez godine: „23. nov". Čita se u UTC-u, kao i server. */
export function weekLabel(iso: string): string {
  const date = new Date(iso);
  return `${date.getUTCDate()}. ${MONTHS_SHORT[date.getUTCMonth()]}`;
}

/** Vrednost jedne nedelje za izabranu meru. */
export function metricValue(week: TrendWeekDto, metric: TrendMetric): number {
  switch (metric.kind) {
    case 'tonnage':
      return week.tonnageKg;
    case 'sets':
      return week.stimulativeSets;
    case 'muscle':
      return week.stimulativeSetsByMuscle[metric.muscle] ?? 0;
  }
}

/** Ključ za izbor u listi, i nazad: lista može da nosi samo tekst. */
export function metricKey(metric: TrendMetric): string {
  return metric.kind === 'muscle' ? `muscle:${metric.muscle}` : metric.kind;
}

export function metricFromKey(key: string): TrendMetric {
  if (key.startsWith('muscle:')) {
    return { kind: 'muscle', muscle: key.slice('muscle:'.length) };
  }

  return key === 'sets' ? { kind: 'sets' } : { kind: 'tonnage' };
}

/**
 * Redovi za prikaz. Traka je srazmerna najvećoj nedelji u opsegu, pa se opseg bira zajedno sa
 * merom: ista nedelja izgleda manja u širem opsegu sa jačim nedeljama.
 */
export function trendRows(trend: TrendDto, metric: TrendMetric): TrendRow[] {
  const values = trend.weeks.map((week) => metricValue(week, metric));
  const max = Math.max(...values, 0);

  return trend.weeks.map((week, index) => {
    const value = values[index];

    return {
      weekStart: week.weekStart,
      label: weekLabel(week.weekStart),
      value,
      barPct: max > 0 ? Math.round((value / max) * 100) : 0,
      isDeload: week.isDeload,
      isEmpty: week.sets === 0,
      blockBanner: week.blockStarts && week.blockName
        ? week.planName && week.planName !== week.blockName
          ? `${week.blockName} · ${week.planName}`
          : week.blockName
        : null,
    };
  });
}
