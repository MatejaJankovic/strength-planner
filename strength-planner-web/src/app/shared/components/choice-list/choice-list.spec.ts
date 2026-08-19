import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChoiceList, ChoiceOption } from './choice-list';

const OPTIONS: ReadonlyArray<ChoiceOption> = [
  { value: '0', label: 'Početnik', hint: '0-1 godina' },
  { value: '1', label: 'Srednji nivo', hint: '1-3 godine' },
  { value: '2', label: 'Napredni', hint: '3+ godine' },
];

/**
 * `ChoiceList` odstupa od ponašanja prave radio grupe u pregledaču: strelice se zaustavljaju
 * na prvoj i poslednjoj ponudi umesto da kruže, i fokus prati izbor. Oba su svesne odluke,
 * pa ih drži test — a ne komentar.
 */
describe('ChoiceList', () => {
  let fixture: ComponentFixture<ChoiceList>;
  let emitted: string[];

  function create(value: string | null): void {
    fixture = TestBed.createComponent(ChoiceList);
    fixture.componentRef.setInput('options', OPTIONS);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('groupLabel', 'Nivo iskustva');

    emitted = [];
    fixture.componentInstance.valueChange.subscribe((next) => emitted.push(next));

    fixture.detectChanges();
  }

  function cards(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.choice'));
  }

  function press(index: number, key: string): void {
    cards()[index].dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ChoiceList] });
  });

  it('dodir na karticu šalje njenu vrednost', () => {
    create(null);

    cards()[1].click();

    expect(emitted).toEqual(['1']);
  });

  it('označava ponudu koju je roditelj dao', () => {
    create('2');

    expect(cards()[2].classList).toContain('is-selected');
    expect(cards()[2].getAttribute('aria-checked')).toBe('true');
    expect(cards()[0].getAttribute('aria-checked')).toBe('false');
  });

  it('strelica nadole pomera izbor na sledeću ponudu', () => {
    create('0');

    press(0, 'ArrowDown');

    expect(emitted).toEqual(['1']);
  });

  it('strelica nagore pomera izbor na prethodnu ponudu', () => {
    create('1');

    press(1, 'ArrowUp');

    expect(emitted).toEqual(['0']);
  });

  it('leva i desna strelica rade kao gornja i donja', () => {
    create('1');

    press(1, 'ArrowRight');
    press(1, 'ArrowLeft');

    expect(emitted).toEqual(['2', '0']);
  });

  /**
   * Prava radio grupa kruži s kraja na početak. Ovde ne sme: izbor je ujedno i odgovor na
   * pitanje koje je jedino na ekranu, pa bi strelica nadole sa poslednje ponude izgledala
   * kao da je izbor poništen.
   */
  it('strelica sa poslednje ponude ne kruži na prvu', () => {
    create('2');

    press(2, 'ArrowDown');

    expect(emitted).toEqual([]);
  });

  it('strelica sa prve ponude ne kruži na poslednju', () => {
    create('0');

    press(0, 'ArrowUp');

    expect(emitted).toEqual([]);
  });

  it('ostali tasteri ne menjaju izbor', () => {
    create('1');

    press(1, 'Enter');
    press(1, 'a');
    press(1, 'Tab');

    expect(emitted).toEqual([]);
  });

  /**
   * Fokus mora da prati izbor, inače naredna strelica kreće od reda koji više nije izabran
   * i izbor „preskače" preko jedne ponude. Traži se po klasi a ne po indeksu među decom,
   * pa dodavanje omotača u listu ne razilazi fokus i vrednost.
   */
  it('fokus prati izbor', () => {
    create('0');

    press(0, 'ArrowDown');

    expect(document.activeElement).toBe(cards()[1]);
  });

  it('samo izabrana ponuda je u redu tabulatora', () => {
    create('1');

    expect(cards().map((card) => card.getAttribute('tabindex'))).toEqual(['-1', '0', '-1']);
  });

  it('bez izbora u red tabulatora ulazi prva ponuda', () => {
    // Inače tabulator preskoči celu grupu i do nje se ne može doći sa tastature.
    create(null);

    expect(cards().map((card) => card.getAttribute('tabindex'))).toEqual(['0', '-1', '-1']);
  });
});
