/** Težina olimpijske šipke i ploče koje se podrazumevano nalaze u teretani, od najteže ka najlakšoj. */
export const BAR_KG = 20;
export const PLATES_KG: readonly number[] = [25, 20, 15, 10, 5, 2.5, 1.25];

export interface PlateLoad {
  /** Ploče sa jedne strane, od najteže, kako se redom stavljaju. */
  plates: number[];
  /** Šta ostaje sa jedne strane kad se ploče iskoriste: nula kad se težina sastavlja tačno. */
  remainderPerSideKg: number;
  /** Težina je manja od same šipke. */
  belowBar: boolean;
}

/** Zaokruživanje na dve decimale: 2.5 + 1.25 u pokretnom zarezu nije uvek tačno 3.75. */
function round2(value: number): number {
  return Math.round(value * 100) / 100;
}

/**
 * Koje ploče idu sa svake strane šipke za ukupnu težinu.
 *
 * Ukupna težina je šipka plus dve strane, pa se sa jedne strane stavlja (ukupno − šipka) / 2.
 * Ploče se biraju gramzivo, od najteže: to je i način na koji se ploče stavljaju, jer najteže idu
 * najbliže zglobu šipke. Kad se težina ne može sastaviti iz raspoloživih ploča (npr. 61 kg sa
 * najmanjom pločom od 1.25), ostatak se vraća da ekran to kaže, umesto da prećuti zaokruživanje.
 */
export function platesFor(
  totalKg: number,
  barKg: number = BAR_KG,
  available: readonly number[] = PLATES_KG,
): PlateLoad {
  if (totalKg < barKg) {
    return { plates: [], remainderPerSideKg: 0, belowBar: true };
  }

  let remaining = round2((totalKg - barKg) / 2);
  const plates: number[] = [];

  for (const plate of [...available].sort((a, b) => b - a)) {
    while (remaining >= plate - 1e-9) {
      plates.push(plate);
      remaining = round2(remaining - plate);
    }
  }

  return { plates, remainderPerSideKg: remaining, belowBar: false };
}

/** „25 + 15 + 2,5", sa zarezom kao decimalnim znakom. */
function listPlates(plates: readonly number[]): string {
  return plates.map((plate) => String(plate).replace('.', ',')).join(' + ');
}

/** Tekst ispod težine: šta se stavlja na šipku. */
export function platesLabel(load: PlateLoad, barKg: number = BAR_KG): string {
  if (load.belowBar) {
    return `Manje od same šipke (${barKg} kg).`;
  }

  if (load.plates.length === 0) {
    return load.remainderPerSideKg > 0
      ? `Prazna šipka (${barKg} kg); ${String(load.remainderPerSideKg).replace('.', ',')} kg po strani se ne može sastaviti.`
      : `Prazna šipka (${barKg} kg).`;
  }

  const base = `Po strani: ${listPlates(load.plates)}.`;
  return load.remainderPerSideKg > 0
    ? `${base} Još ${String(load.remainderPerSideKg).replace('.', ',')} kg po strani se ne može sastaviti iz ploča koje imaš.`
    : base;
}
