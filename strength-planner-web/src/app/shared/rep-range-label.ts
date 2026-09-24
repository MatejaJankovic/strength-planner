/**
 * Opseg ponavljanja kako se prikazuje: `8–12`, ili samo `5` kada je propisan tačan broj.
 *
 * Lični šablon sme da propiše fiksan broj ponavljanja (5×5 je program, a ne nemarno unet
 * opseg), pa bi „5–5" bilo tačno ali besmisleno. Stoji u `shared` jer isti broj prikazuju
 * tri ekrana — trening, pregled bloka i spisak ličnih šablona — a kopija u jednom od njih
 * bi bila četvrto mesto koje mora da zna pravilo.
 *
 * Crtica je en dash (–), kao i na ostalim ekranima.
 */
export function repRangeLabel(repRangeMin: number, repRangeMax: number): string {
  return repRangeMin === repRangeMax ? `${repRangeMin}` : `${repRangeMin}–${repRangeMax}`;
}
