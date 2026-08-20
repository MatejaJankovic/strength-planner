import { Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Zaglavlje podekrana: strelica nazad, naslov u sredini, i mesto za jednu radnju desno.
 *
 * Tri ekrana ga koriste — izmena profila, podešavanja i vežbe. Prvo je bilo prepisano u sve
 * tri komponente, sa tri kopije istog dugmeta od 44px i iste mreže od tri kolone, i kopije
 * su se već razišle: jedna je grupisala dugme nazad sa dugmetom za dodavanje, druga je
 * nosila prazan `<span>` samo da naslov ostane u sredini. Isti razlog zbog kog su kartice i
 * polja izvučeni u `_form-shell.scss`, samo jedan nivo iznad.
 *
 * Radnja desno dolazi projekcijom u `[slot=action]`. Kada je nema, kolona ostaje prazna, pa
 * naslov stoji u sredini bez pomoćnog elementa.
 */
@Component({
  selector: 'app-subscreen-header',
  imports: [MatIconModule],
  templateUrl: './subscreen-header.html',
  styleUrl: './subscreen-header.scss',
})
export class SubscreenHeader {
  readonly title = input.required<string>();

  /** Šta čitač ekrana kaže za strelicu nazad — odredište, ne samo „Nazad". */
  readonly backLabel = input('Nazad');

  readonly back = output<void>();
}
