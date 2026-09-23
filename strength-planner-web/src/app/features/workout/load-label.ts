/**
 * Kako se piše opterećenje vežbe koju opterećuje sopstveno telo.
 *
 * Zgib se ne unosi kao „0 kg": u polje ide ono što se DODAJE (pojas, traka), a nula znači
 * sopstvenom masom. Zato se i prikazuje tako — „TM" za sopstvenu masu, „TM + 5 kg" za
 * pojas. Skraćenica je objašnjena na kartici vežbe, gde se i vidi koliko je to kilograma.
 *
 * Funkcija je zajednička za rezime treninga i za listu odrađenih serija, jer su to dva
 * mesta na kojima se isti broj pisao na dva načina.
 */
export function loadLabel(addedKg: number, isBodyweight: boolean): string {
  if (!isBodyweight) {
    return `${formatKg(addedKg)} kg`;
  }

  return addedKg > 0 ? `TM + ${formatKg(addedKg)} kg` : 'TM';
}

/** Kilogrami se pišu kao i svuda u aplikaciji: bez decimale kada je cela vrednost. */
export function formatKg(value: number): string {
  return Number.isInteger(value) ? `${value}` : value.toFixed(1);
}
