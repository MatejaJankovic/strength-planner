import { Component, computed, ElementRef, input, output, viewChild } from '@angular/core';

/**
 * Unos jedne brojne mere: veliki prikaz vrednosti, klizač ispod njega i polje za
 * precizan unos.
 *
 * Klizač sam nije dovoljan — na opsegu od 30 do 300 kg jedan piksel vredi skoro pola
 * kilograma, pa se tačna vrednost prstom ne pogađa. Polje samo nije dovoljno jer traži
 * tastaturu za nešto što je u suštini izbor sa skale. Zato oba, nad istom vrednošću.
 */
@Component({
  selector: 'app-measure-input',
  templateUrl: './measure-input.html',
  styleUrl: './measure-input.scss',
})
export class MeasureInput {
  readonly value = input.required<number>();
  readonly min = input.required<number>();
  readonly max = input.required<number>();
  readonly step = input(1);
  readonly unit = input.required<string>();
  readonly label = input.required<string>();

  /**
   * Identifikator polja za precizan unos, za vezu sa oznakom.
   *
   * Traži se izvana namerno. Ranije se gradio spajanjem sa <see cref="label"/>, pa je
   * ispadao id sa razmacima („measure-Telesna masa u kilogramima") — što HTML ne
   * dopušta — i dve mere sa istom oznakom na jednom ekranu dobile bi isti id.
   */
  readonly inputId = input.required<string>();

  readonly valueChange = output<number>();

  private readonly field = viewChild<ElementRef<HTMLInputElement>>('field');

  /**
   * Broj decimala koji se prikazuje izvodi se iz koraka: korak 0.5 traži jednu decimalu,
   * korak 1 nijednu. Bez toga masa od 72.5 kg piše „73" dok klizač stoji između dve crte.
   */
  protected readonly display = computed(() => {
    const decimals = Number.isInteger(this.step()) ? 0 : 1;
    return this.value().toFixed(decimals);
  });

  protected onSlide(raw: string): void {
    this.emit(Number(raw));
  }

  /**
   * Polje za precizan unos šalje vrednost tek na `change`, ne na svaki otkucaj: dok se
   * kuca „180", međustanje „1" je ispod donje granice i skakalo bi na nju.
   */
  protected onType(raw: string): void {
    // Prazno polje je „još ne znam", a ne nula. Number('') je nula, pa bi bez ovoga
    // brisanje sadržaja da bi se ukucalo nešto drugo skočilo na donju granicu — na
    // koraku telesne mase na 30 kg.
    if (raw.trim() === '') {
      this.rewriteField();
      return;
    }

    const parsed = Number(raw);
    if (Number.isNaN(parsed)) {
      this.rewriteField();
      return;
    }

    this.emit(parsed);
  }

  private emit(value: number): void {
    const clamped = Math.min(Math.max(value, this.min()), this.max());

    // toFixed skida float drift kod koraka koji nisu stepen dvojke.
    const next = Number(clamped.toFixed(1));

    this.valueChange.emit(next);

    // Odbijena vrednost se mora ručno vratiti u polje.
    //
    // Angular ne prepisuje [value] kada se model nije promenio, a odsecanje na granicu
    // upravo to i daje: ako je vrednost već bila na granici, `next` je isti kao pre, pa
    // se prikaz ne osvežava i u polju ostaje ono što je korisnik ukucao. Izmereno: model
    // na 300, ukucano 999 — prikaz i klizač su pokazivali 300, a polje 999. Ista greška
    // koju `profile-home.ts` već opisuje za korak opterećenja.
    if (next === this.value()) {
      this.rewriteField();
    }
  }

  /** Vraća u polje vrednost koju komponenta stvarno drži. */
  private rewriteField(): void {
    const field = this.field()?.nativeElement;
    if (field) {
      field.value = String(this.value());
    }
  }
}
