import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MeasureInput } from './measure-input';

/**
 * `MeasureInput` odlučuje koju vrednost čarobnjak pošalje za masu, visinu i uzrast — tri
 * od osam koraka registracije. Odsecanje na granice i broj decimala su njegov posao, i
 * upravo tu je već bila greška koja se videla samo na ekranu.
 */
describe('MeasureInput', () => {
  let fixture: ComponentFixture<MeasureInput>;
  let emitted: number[];

  function create(value: number, min: number, max: number, step: number): void {
    fixture = TestBed.createComponent(MeasureInput);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('min', min);
    fixture.componentRef.setInput('max', max);
    fixture.componentRef.setInput('step', step);
    fixture.componentRef.setInput('unit', 'kg');
    fixture.componentRef.setInput('label', 'Telesna masa u kilogramima');
    fixture.componentRef.setInput('inputId', 'test-measure');

    emitted = [];
    fixture.componentInstance.valueChange.subscribe((next) => emitted.push(next));

    fixture.detectChanges();
  }

  function field(): HTMLInputElement {
    return fixture.nativeElement.querySelector('.measure__field');
  }

  function slider(): HTMLInputElement {
    return fixture.nativeElement.querySelector('.measure__slider');
  }

  function readout(): string {
    return fixture.nativeElement.querySelector('.measure__number').textContent.trim();
  }

  /** Kucanje u polje za precizan unos: vrednost se šalje na `change`, ne na otkucaj. */
  function type(raw: string): void {
    const input = field();
    input.value = raw;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [MeasureInput] });
  });

  it('odseca vrednost iznad gornje granice', () => {
    create(75, 30, 300, 0.5);

    type('999');

    expect(emitted).toEqual([300]);
  });

  it('odseca vrednost ispod donje granice', () => {
    create(75, 30, 300, 0.5);

    type('5');

    expect(emitted).toEqual([30]);
  });

  /**
   * Prijavljena greška: polje je pokazivalo odbijenu vrednost.
   *
   * Angular ne prepisuje `[value]` kada se model nije promenio, a odsecanje na granicu
   * upravo to daje kada je vrednost već bila na granici. Izmereno na živoj aplikaciji:
   * model 300, ukucano 999 — prikaz i klizač su pokazivali 300, a polje 999.
   */
  it('vraća odbijenu vrednost u polje kada je vrednost već bila na granici', () => {
    create(300, 30, 300, 0.5);

    type('999');

    // Model se nije promenio, pa emitovanje ne menja ništa - ali polje ne sme da laže.
    expect(field().value).toBe('300');
    expect(readout()).toBe('300.0');
    expect(slider().value).toBe('300');
  });

  it('prazno polje ne postaje nula i ne skače na donju granicu', () => {
    // Number('') je nula, pa je brisanje sadržaja radi ponovnog kucanja skakalo na 30 kg.
    create(82.5, 30, 300, 0.5);

    type('');

    expect(emitted).toEqual([]);
    expect(field().value).toBe('82.5');
  });

  it('slova u polju ne menjaju vrednost', () => {
    create(82.5, 30, 300, 0.5);

    type('abc');

    expect(emitted).toEqual([]);
    expect(field().value).toBe('82.5');
  });

  it('korak sa decimalom prikazuje jednu decimalu', () => {
    create(82.5, 30, 300, 0.5);

    expect(readout()).toBe('82.5');
  });

  it('celobrojni korak ne prikazuje decimale', () => {
    create(27, 14, 90, 1);

    expect(readout()).toBe('27');
  });

  it('klizač šalje vrednost na koju je pomeren', () => {
    create(75, 30, 300, 0.5);

    const range = slider();
    range.value = '92.5';
    range.dispatchEvent(new Event('input'));

    expect(emitted).toEqual([92.5]);
  });

  it('identifikator polja dolazi izvana i vezuje oznaku', () => {
    // Ranije se gradio spajanjem sa oznakom, pa je ispadao id sa razmacima - što HTML ne
    // dopušta - i dve mere sa istom oznakom dobile bi isti id.
    create(75, 30, 300, 0.5);

    const label: HTMLLabelElement = fixture.nativeElement.querySelector('.measure__precise-label');

    expect(field().id).toBe('test-measure');
    expect(label.getAttribute('for')).toBe('test-measure');
    expect(field().id).not.toContain(' ');
  });

  it('čitač ekrana dobija i jedinicu, ne samo broj', () => {
    create(82.5, 30, 300, 0.5);

    expect(slider().getAttribute('aria-valuetext')).toBe('82.5 kg');
  });
});
