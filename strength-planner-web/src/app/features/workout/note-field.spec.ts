import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NOTE_MAX_LENGTH, NoteField } from './note-field';

describe('NoteField', () => {
  let fixture: ComponentFixture<NoteField>;
  let http: HttpTestingController;

  function create(kind: 'session' | 'exercise', value: string | null = null, targetId = 't-1'): void {
    fixture = TestBed.createComponent(NoteField);
    fixture.componentRef.setInput('label', kind === 'session' ? 'Beleška o treningu' : 'Beleška uz vežbu');
    fixture.componentRef.setInput('kind', kind);
    fixture.componentRef.setInput('targetId', targetId);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
  }

  function root(): HTMLElement {
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function text(): string {
    return (root().textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  function button(label: string): HTMLButtonElement {
    const found = Array.from(root().querySelectorAll('button')).find((candidate) =>
      (candidate.textContent ?? '').includes(label),
    );
    if (!found) {
      throw new Error(`Nema dugmeta „${label}"`);
    }
    return found;
  }

  function type(value: string): void {
    const field = root().querySelector('textarea')!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('bez beleške nudi da se doda, i ništa više', () => {
    create('exercise');

    expect(text()).toContain('Dodaj belešku');
    expect(root().querySelector('textarea')).toBeNull();
  });

  it('sa beleškom je prikazuje i nudi izmenu', () => {
    create('exercise', 'Sedište 4, pin 7');

    expect(text()).toContain('Sedište 4, pin 7');
    expect(text()).toContain('Izmeni');
    expect(text()).not.toContain('Dodaj belešku');
  });

  it('dodavanje otvara polje sa ograničenjem i brojačem', () => {
    create('exercise');
    button('Dodaj belešku').click();

    const field = root().querySelector('textarea')!;
    expect(field.maxLength).toBe(NOTE_MAX_LENGTH);
    expect(text()).toContain(`0 / ${NOTE_MAX_LENGTH}`);

    type('Levo rame: spusti hvat');
    // „Levo rame: spusti hvat" ima 22 znaka.
    expect(text()).toContain(`22 / ${NOTE_MAX_LENGTH}`);
  });

  it('izmena kreće od postojeće beleške', () => {
    create('session', 'Stara');
    button('Izmeni').click();

    expect(root().querySelector('textarea')!.value).toBe('Stara');
  });

  it('beleška uz trening se šalje treningu, a čuvanjem se prikazuje kakvu je server vratio', () => {
    create('session', null, 's-77');
    button('Dodaj belešku').click();
    type('  Težak dan  ');

    button('Sačuvaj').click();
    const request = http.expectOne((candidate) => candidate.url.endsWith('/sessions/s-77/note'));
    expect(request.request.body).toEqual({ note: '  Težak dan  ' });
    // Server je skinuo razmake: prikazuje se ono što je sačuvano, ne ono što je otkucano.
    request.flush({ note: 'Težak dan' });

    expect(text()).toContain('Težak dan');
    expect(root().querySelector('textarea')).toBeNull();
    expect(text()).toContain('Izmeni');
  });

  it('beleška uz vežbu se šalje vežbi', () => {
    create('exercise', null, 'e-5');
    button('Dodaj belešku').click();
    type('Sedište 4');

    button('Sačuvaj').click();

    http.expectOne((candidate) => candidate.url.endsWith('/exercises/e-5/note')).flush({ note: 'Sedište 4' });
  });

  it('prazna beleška briše staru: ponovo nudi dodavanje', () => {
    create('exercise', 'Stara', 'e-5');
    button('Izmeni').click();
    type('');

    button('Sačuvaj').click();
    http.expectOne((candidate) => candidate.url.endsWith('/exercises/e-5/note')).flush({ note: null });

    expect(text()).toContain('Dodaj belešku');
    expect(text()).not.toContain('Stara');
  });

  it('dok se čuva, dugmad su zaključana i drugi dodir ne šalje drugi zahtev', () => {
    create('exercise', null, 'e-5');
    button('Dodaj belešku').click();
    type('x');

    button('Sačuvaj').click();
    const saving = button('Čuvam');
    expect(saving.disabled).toBe(true);
    expect(button('Otkaži').disabled).toBe(true);
    saving.click();

    http.expectOne((candidate) => candidate.url.endsWith('/exercises/e-5/note')).flush({ note: 'x' });
  });

  it('greška ostavlja polje otvoreno sa onim što je otkucano, i kaže zašto', () => {
    create('exercise', null, 'e-5');
    button('Dodaj belešku').click();
    type('Moja beleška');

    button('Sačuvaj').click();
    http
      .expectOne((candidate) => candidate.url.endsWith('/exercises/e-5/note'))
      .flush({ title: 'Validation failed.' }, { status: 400, statusText: 'Bad Request' });

    expect(root().querySelector('[role="alert"]')).not.toBeNull();
    expect(root().querySelector('textarea')!.value).toBe('Moja beleška');
    expect(button('Sačuvaj').disabled).toBe(false);
  });

  it('otkazivanje odbacuje otkucano i ostavlja staru belešku', () => {
    create('exercise', 'Stara');
    button('Izmeni').click();
    type('Nova koja se ne čuva');

    button('Otkaži').click();

    expect(text()).toContain('Stara');
    expect(text()).not.toContain('Nova koja se ne čuva');
  });

  it('kad ekran dobije novu belešku sa servera (ponovno učitavanje), prikazuje nju', () => {
    create('exercise', 'Prva');
    expect(text()).toContain('Prva');

    fixture.componentRef.setInput('value', 'Druga');

    expect(text()).toContain('Druga');
  });

  it('sačuvana beleška ne vraća staru vrednost dok server ne pošalje drugu', () => {
    create('exercise', 'Stara', 'e-5');
    button('Izmeni').click();
    type('Nova');
    button('Sačuvaj').click();
    http.expectOne((candidate) => candidate.url.endsWith('/exercises/e-5/note')).flush({ note: 'Nova' });

    // Ulaz je i dalje stara vrednost (ekran nije učitan ponovo), a prikaz ostaje na novoj.
    fixture.detectChanges();

    expect(text()).toContain('Nova');
    expect(text()).not.toContain('Stara');
  });

  it('beleška se prikazuje onako kako je napisana: novi redovi ostaju', () => {
    create('exercise', 'prvi red\ndrugi red');

    const paragraph = root().querySelector<HTMLElement>('.note__text')!;
    expect(paragraph.textContent).toBe('prvi red\ndrugi red');
  });
});
