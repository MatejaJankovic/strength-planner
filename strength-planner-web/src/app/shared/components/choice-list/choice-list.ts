import { Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Jedna ponuda u listi. `hint` je sitniji red ispod naslova (npr. „0-1 godina"). */
export interface ChoiceOption {
  value: string;
  label: string;
  hint?: string;
  icon?: string;
}

/**
 * Lista ponuda u obliku kartica, po jedna vrednost — ono što bi inače bio `<select>`
 * ili grupa radio dugmadi, samo na celu širinu ekrana.
 *
 * Postoji zato što na telefonu `<select>` otvara sistemski meni koji sakrije pitanje na
 * koje se odgovara. Kartica je uvek vidljiva, a cilj za prst joj je cela širina reda.
 *
 * Pristupačnost se ne oslanja na izgled: reda ima onoliko koliko ima ponuda, a nose
 * `role="radio"` unutar `role="radiogroup"`, pa čitač ekrana čita „2 od 3 izabrano"
 * isto kao kod prave radio grupe. Strelice pomeraju izbor, kao što se od radio grupe i
 * očekuje.
 */
@Component({
  selector: 'app-choice-list',
  imports: [MatIconModule],
  templateUrl: './choice-list.html',
  styleUrl: './choice-list.scss',
})
export class ChoiceList {
  readonly options = input.required<ReadonlyArray<ChoiceOption>>();
  readonly value = input<string | null>(null);
  readonly groupLabel = input.required<string>();
  readonly valueChange = output<string>();

  protected select(option: ChoiceOption): void {
    this.valueChange.emit(option.value);
  }

  /**
   * Strelice biraju susednu ponudu i tu se zaustavljaju.
   *
   * Prava radio grupa u pregledaču kruži s kraja na početak, ali ovde je izbor ujedno i
   * odgovor na pitanje koje je jedino na ekranu: da kruži, strelica nadole sa poslednje
   * ponude bi izgledala kao da je izbor poništen.
   */
  protected onKeydown(event: KeyboardEvent, index: number): void {
    const step = event.key === 'ArrowDown' || event.key === 'ArrowRight' ? 1 : event.key === 'ArrowUp' || event.key === 'ArrowLeft' ? -1 : 0;

    if (step === 0) {
      return;
    }

    const options = this.options();
    const next = Math.min(Math.max(index + step, 0), options.length - 1);

    if (next === index) {
      return;
    }

    event.preventDefault();
    this.valueChange.emit(options[next].value);

    // Fokus mora da prati izbor, inače naredna strelica kreće od reda koji više nije
    // izabran i izbor „preskače" preko jedne ponude.
    const list = (event.currentTarget as HTMLElement).parentElement;
    const target = list?.children.item(next);
    if (target instanceof HTMLElement) {
      target.focus();
    }
  }
}
