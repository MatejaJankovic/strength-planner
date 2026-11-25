import { Component, computed, model } from '@angular/core';

/** Odgovori na pitanje o umoru, 1 do 5. Granice prate `SubjectiveFatigue` na serveru (1 i 5). */
export const FATIGUE_RATINGS: readonly { value: number; label: string }[] = [
  { value: 1, label: 'Sveže' },
  { value: 2, label: 'Dobro' },
  { value: 3, label: 'Umereno' },
  { value: 4, label: 'Jako umoran' },
  { value: 5, label: 'Iscrpljen' },
];

let nextId = 0;

/**
 * „Kako se osećaš posle treninga?" - jedno neobavezno pitanje uz „Završi trening".
 *
 * Šalje se sa završetkom, a ne posle njega: ocena umora nedelje se računa kad se završi njen poslednji trening,
 * pa odgovor iz tog treninga mora da stigne pre toga. Ne čita se nivo odgovora nego porast nad vežbačevim
 * ranijim nedeljama, pa nema „tačnog" odgovora, i zato nema ni opisa koliko je koji „dobar". Ponovni dodir
 * izabranog odgovora ga poništava: pitanje je neobavezno, a bez poništavanja bi izbor bio nepovratan.
 *
 * Grupa dugmadi sa `aria-pressed`, a ne radio-grupa: radio dugme se aktiviranjem ne može poništiti (pregled #127).
 */
@Component({
  selector: 'app-fatigue-rating',
  templateUrl: './fatigue-rating.html',
  styleUrl: './fatigue-rating.scss',
})
export class FatigueRating {
  /** Izabrani odgovor, ili null kad vežbač ne odgovara. */
  readonly value = model<number | null>(null);

  protected readonly ratings = FATIGUE_RATINGS;
  protected readonly chosen = computed(() => FATIGUE_RATINGS.find((option) => option.value === this.value()) ?? null);
  protected readonly legendId = `fatigue-rating-${nextId++}`;

  protected choose(rating: number): void {
    this.value.set(this.value() === rating ? null : rating);
  }
}
