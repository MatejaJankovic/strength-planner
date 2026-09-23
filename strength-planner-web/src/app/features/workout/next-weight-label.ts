import { CompletedExerciseSummaryDto } from '../../core/models/training.models';
import { StatChipTone } from '../../shared/components/stat-chip/stat-chip';

/**
 * Tekst i ton oznake „Sledeće" u rezimeu treninga.
 *
 * Do sada je stajala samo strelica naviše, i to iz zastavice koja je značila „sve serije su
 * stigle do vrha opsega" — pa je pred planirani deload pisalo „90 kg ↑". Sada se prikazuje
 * razlika prema težini koju je vežbač zaista podigao, pa oznaka govori i kada predlog pada.
 *
 * Pravila su u čistoj funkciji sa sopstvenim testom: izraz u šablonu ne bi pokrio nulu i
 * nedostajuću vrednost, a to su dva slučaja koja se lako pomešaju.
 */
export function nextWeightLabel(summary: Pick<CompletedExerciseSummaryDto, 'nextWeightKg' | 'weightChangeKg'>): string {
  const next = summary.nextWeightKg;

  if (next == null) {
    return '';
  }

  const change = summary.weightChangeKg;
  const base = `${formatKg(next)} kg`;

  if (change == null || change === 0) {
    return base;
  }

  return change > 0
    ? `${base} ↑ +${formatKg(change)}`
    : `${base} ↓ −${formatKg(-change)}`;
}

/** Ton oznake: naglašen samo kada predlog raste. */
export function nextWeightTone(
  summary: Pick<CompletedExerciseSummaryDto, 'weightChangeKg'>,
): StatChipTone {
  return summary.weightChangeKg != null && summary.weightChangeKg > 0 ? 'accent' : 'neutral';
}

/** Kilogrami se pišu kao i svuda u aplikaciji: bez decimale kada je cela vrednost. */
function formatKg(value: number): string {
  return Number.isInteger(value) ? `${value}` : value.toFixed(1);
}
