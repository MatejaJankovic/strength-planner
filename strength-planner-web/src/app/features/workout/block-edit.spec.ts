import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { WorkoutSessionDto } from '../../core/models/training.models';
import { AddExercise } from './add-exercise';
import { blockEditError } from './block-edit-error';
import { RemoveExercise } from './remove-exercise';
import { WorkoutSession } from './workout-session';

function plan(id: string, exerciseName = 'Vežba', targetWeightKg: number | null = 60) {
  return {
    id,
    exerciseId: `e-${id}`,
    exerciseName,
    order: 1,
    targetSets: 3,
    prescribedSets: 3,
    repRangeMin: 8,
    repRangeMax: 12,
    targetRir: 2,
    targetWeightKg,
    weightStepKg: 2.5,
    isBodyweight: false,
    bodyweightLoadKg: 0,
    setLogs: [],
  };
}

function session(status: string, plans: ReturnType<typeof plan>[]): WorkoutSessionDto {
  return {
    id: 's1',
    weekNumber: 2,
    isDeload: false,
    isAutoDeload: false,
    dayLabel: 'Day A',
    status,
    exercisePlans: plans,
  } as unknown as WorkoutSessionDto;
}

const EXERCISES = [
  { exerciseId: 'fly', name: 'Cable Fly', equipment: 'Cable', isCustom: false, primaryMuscles: ['Chest'] },
  { exerciseId: 'plain', name: 'Moja vežba', equipment: 'Machine', isCustom: true, primaryMuscles: [] },
];
const OPTIONS = { exercises: EXERCISES };

/** Odgovor koji server šalje u produkciji: opšti engleski naslov, a razlog nikako. */
const GENERIC_CONFLICT = { title: 'Request conflicts with current state.', status: 409, detail: 'Request conflicts with current state.' };

describe('AddExercise', () => {
  let fixture: ComponentFixture<AddExercise>;
  let http: HttpTestingController;
  let added: WorkoutSessionDto[];

  function create(): void {
    fixture = TestBed.createComponent(AddExercise);
    fixture.componentRef.setInput('sessionId', 's1');
    added = [];
    fixture.componentInstance.added.subscribe((result) => added.push(result));
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

  function openPicker(options: { exercises: unknown[]; unavailable?: string | null } = OPTIONS): void {
    button('Dodaj vežbu').click();
    http
      .expectOne((request) => request.method === 'GET' && request.url.endsWith('/sessions/s1/addable-exercises'))
      .flush(options);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('zatvoreno nudi samo dugme, bez ijednog zahteva', () => {
    create();

    expect(text()).toContain('Dodaj vežbu');
    expect(root().querySelector('select')).toBeNull();
  });

  it('otvaranje nudi vežbe sa mišićima, prva je izabrana', () => {
    create();
    openPicker();

    const select = root().querySelector('select')!;
    expect(Array.from(select.options).map((option) => option.textContent?.trim())).toEqual([
      'Cable Fly (Chest)',
      'Moja vežba',
    ]);
    expect(select.value).toBe('fly');
  });

  it('potvrda šalje izabranu vežbu, izlazi iz izbora i vraća trening', () => {
    create();
    openPicker();

    const select = root().querySelector('select')!;
    select.value = 'plain';
    select.dispatchEvent(new Event('change'));
    button('Dodaj do kraja bloka').click();

    const request = http.expectOne(
      (candidate) => candidate.method === 'POST' && candidate.url.endsWith('/sessions/s1/exercises'),
    );
    expect(request.request.body).toEqual({ exerciseId: 'plain' });
    const result = session('Planned', [plan('p1'), plan('p2')]);
    request.flush(result);

    expect(added).toEqual([result]);
    expect(root().querySelector('select')).toBeNull();
  });

  it('dok čeka odgovor, drugi pritisak ne šalje vežbu dvaput', () => {
    create();
    openPicker();

    const confirm = button('Dodaj do kraja bloka');
    confirm.click();
    fixture.detectChanges();
    confirm.click();

    http.expectOne((candidate) => candidate.method === 'POST').flush(session('Planned', [plan('p1')]));
    expect(added.length).toBe(1);
  });

  it('razlog koji server šalje kao podatak ostaje na ekranu, bez izbora i bez dugmeta za potvrdu', () => {
    create();
    openPicker({
      exercises: [],
      unavailable: 'Ovaj blok je već imao rasterećenje zbog umora, pa se u njemu više ne dodaju vežbe.',
    });

    expect(text()).toContain('Ovaj blok je već imao rasterećenje zbog umora');
    expect(root().querySelector('select')).toBeNull();
    expect(text()).not.toContain('Dodaj do kraja bloka');
    expect(text()).toContain('Odustani');
  });

  /**
   * Tekst iz servera u produkciji ne stiže do ekrana (opšti naslov na engleskom): vežbač dobija srpsku poruku,
   * a ne „Request conflicts with current state."
   */
  it('odbijen zahtev pokazuje srpsku poruku, a ne opšti engleski naslov servera', () => {
    create();
    button('Dodaj vežbu').click();
    http.expectOne((request) => request.method === 'GET').flush(GENERIC_CONFLICT, { status: 409, statusText: 'Conflict' });

    expect(text()).toContain('Izmena nije moguća');
    expect(text()).not.toContain('Request conflicts');
    expect(text()).not.toContain('Dodaj do kraja bloka');
    expect(text()).toContain('Odustani');
  });

  it('prazna ponuda kaže zašto', () => {
    create();
    openPicker({ exercises: [] });

    expect(text()).toContain('Nema vežbe koja bi mogla da se doda');
  });

  it('greška pri dodavanju ostavlja izbor otvoren, da se može ponoviti', () => {
    create();
    openPicker();
    button('Dodaj do kraja bloka').click();

    http.expectOne((request) => request.method === 'POST').flush(GENERIC_CONFLICT, { status: 409, statusText: 'Conflict' });

    expect(text()).toContain('Izmena nije moguća');
    expect(root().querySelector('select')).not.toBeNull();
    expect(added.length).toBe(0);
  });

  it('odgovor koji stigne posle odustajanja ne otvara izbor ponovo', () => {
    create();
    button('Dodaj vežbu').click();
    button('Odustani').click();

    http.expectOne((request) => request.method === 'GET').flush(OPTIONS);

    expect(root().querySelector('select')).toBeNull();
    expect(text()).toContain('Dodaj vežbu');
  });
});

describe('blockEditError', () => {
  it.each([400, 404, 409])('status %i daje srpsku poruku, bez naslova servera', (status) => {
    const error = new HttpErrorResponse({ status, error: GENERIC_CONFLICT });

    expect(blockEditError(error, 'rezerva')).toContain('Izmena nije moguća');
  });

  it('bez odgovora servera (mreža, 500) ostaje poruka pozivaoca', () => {
    expect(blockEditError(new HttpErrorResponse({ status: 0 }), 'Vežba nije dodata.')).toBe('Vežba nije dodata.');
    expect(blockEditError(new HttpErrorResponse({ status: 500 }), 'Vežba nije dodata.')).toBe('Vežba nije dodata.');
    expect(blockEditError(new Error('x'), 'Vežba nije dodata.')).toBe('Vežba nije dodata.');
  });
});

describe('RemoveExercise', () => {
  let fixture: ComponentFixture<RemoveExercise>;
  let http: HttpTestingController;
  let removed: WorkoutSessionDto[];

  function create(): void {
    fixture = TestBed.createComponent(RemoveExercise);
    fixture.componentRef.setInput('planId', 'p1');
    fixture.componentRef.setInput('exerciseName', 'Bench Press');
    removed = [];
    fixture.componentInstance.removed.subscribe((result) => removed.push(result));
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

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uklanjanje traži potvrdu i ne šalje ništa dok je ne dobije', () => {
    create();

    button('Ukloni vežbu').click();

    expect(text()).toContain('„Bench Press" se uklanja');
    expect(text()).toContain('Ukloni do kraja bloka');
    http.expectNone(() => true);
  });

  it('potvrda briše plan i vraća trening', () => {
    create();
    button('Ukloni vežbu').click();
    button('Ukloni do kraja bloka').click();

    const request = http.expectOne(
      (candidate) => candidate.method === 'DELETE' && candidate.url.endsWith('/sessions/plans/p1'),
    );
    const result = session('Planned', [plan('p2')]);
    request.flush(result);

    expect(removed).toEqual([result]);
    expect(text()).toContain('Ukloni vežbu');
    expect(text()).not.toContain('se uklanja');
  });

  it('odustajanje vraća dugme, a greška ostaje uz potvrdu', () => {
    create();
    button('Ukloni vežbu').click();
    button('Odustani').click();
    expect(text()).not.toContain('se uklanja');

    button('Ukloni vežbu').click();
    button('Ukloni do kraja bloka').click();
    http.expectOne((request) => request.method === 'DELETE').flush(GENERIC_CONFLICT, { status: 409, statusText: 'Conflict' });

    expect(text()).toContain('Izmena nije moguća');
    expect(text()).not.toContain('Request conflicts');
    expect(text()).toContain('Ukloni do kraja bloka');
    expect(removed.length).toBe(0);
  });
});

/**
 * Šta ekran treninga nudi, i šta radi sa unosom kada se dan promeni: nesačuvan unos na drugoj kartici ne
 * sme da nestane zbog toga što je u danu dodata ili uklonjena neka druga vežba.
 */
describe('WorkoutSession: izmena bloka', () => {
  let fixture: ComponentFixture<WorkoutSession>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function open(status: string, count = 2): void {
    fixture = TestBed.createComponent(WorkoutSession);
    fixture.detectChanges();
    http
      .expectOne((request) => request.method === 'GET' && request.url.includes('/sessions/'))
      .flush(session(status, Array.from({ length: count }, (_, index) => plan(`p${index + 1}`, `Vežba ${index + 1}`))));
    fixture.detectChanges();
    TestBed.tick();
  }

  function labels(): string[] {
    fixture.detectChanges();
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).map((candidate) =>
      (candidate.textContent ?? '').replace(/\s+/g, ' ').trim(),
    );
  }

  it('trening koji čeka ili traje nudi dodavanje i uklanjanje', () => {
    for (const status of ['Planned', 'InProgress']) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
      });
      http = TestBed.inject(HttpTestingController);

      open(status);

      expect(labels().filter((label) => label.includes('Ukloni vežbu')).length, status).toBe(2);
      expect(labels().some((label) => label.includes('Dodaj vežbu')), status).toBe(true);
      http.verify();
    }
  });

  it('završen i preskočen trening su istorija: ništa se ne nudi', () => {
    for (const status of ['Completed', 'Skipped']) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
      });
      http = TestBed.inject(HttpTestingController);

      open(status);

      expect(labels().some((label) => label.includes('Ukloni vežbu')), status).toBe(false);
      expect(labels().some((label) => label.includes('Dodaj vežbu')), status).toBe(false);
      http.verify();
    }
  });

  it('jedina vežba u danu se ne uklanja, ali se druga može dodati', () => {
    open('Planned', 1);

    expect(labels().some((label) => label.includes('Ukloni vežbu'))).toBe(false);
    expect(labels().some((label) => label.includes('Dodaj vežbu'))).toBe(true);
  });

  it('dan koji je pun ne nudi dodavanje, a ostaje uklanjanje', () => {
    open('Planned', 12);

    expect(labels().some((label) => label.includes('Dodaj vežbu'))).toBe(false);
    expect(labels().filter((label) => label.includes('Ukloni vežbu')).length).toBe(12);
  });

  it('posle dodavanja nesačuvan unos na drugoj kartici ostaje, a nova kartica dobija svoj', () => {
    open('InProgress');
    const component = fixture.componentInstance as unknown as {
      setWeight(planId: string, value: number): void;
      draftOf(planId: string): { weightKg: number };
      onBlockEdited(result: WorkoutSessionDto, message: string): void;
    };

    component.setWeight('p1', 77.5);
    component.onBlockEdited(
      session('InProgress', [plan('p1'), plan('p2'), plan('p3', 'Nova vežba', 25)]),
      'Vežba je dodata do kraja bloka.',
    );

    expect(component.draftOf('p1').weightKg).toBe(77.5);
    expect(component.draftOf('p3').weightKg).toBe(25);
    expect(labels().filter((label) => label.includes('Ukloni vežbu')).length).toBe(3);
  });

  /**
   * Poruka stoji iznad donje trake i pokriva ono što je ispod nje: posle dodavanja to je dugme „Dodaj
   * vežbu", pa se druga vežba nije mogla dodati dok poruka ne nestane. Poruke posle zamene i preskakanja
   * nisu nestajale uopšte.
   */
  it('poruka o izmeni nestaje sama', () => {
    vi.useFakeTimers();
    try {
      open('Planned');
      const component = fixture.componentInstance as unknown as {
        onBlockEdited(result: WorkoutSessionDto, message: string): void;
      };

      component.onBlockEdited(session('Planned', [plan('p1'), plan('p2'), plan('p3')]), 'Vežba je dodata do kraja bloka.');
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Vežba je dodata do kraja bloka.');

      vi.advanceTimersByTime(3300);
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Vežba je dodata do kraja bloka.');
    } finally {
      vi.useRealTimers();
    }
  });

  it('poruka posle zamene vežbe nestaje sama', () => {
    vi.useFakeTimers();
    try {
      open('Planned');
      const component = fixture.componentInstance as unknown as {
        openSwap(target: ReturnType<typeof plan>): void;
        swapChoice: { set(value: string): void };
        confirmSwap(target: ReturnType<typeof plan>): void;
      };

      component.openSwap(plan('p1', 'Vežba 1'));
      http.expectOne((request) => request.url.endsWith('/sessions/plans/p1/alternatives')).flush([
        { exerciseId: 'alt', name: 'Druga', equipment: 'Barbell', isCustom: false },
      ]);
      component.swapChoice.set('alt');
      component.confirmSwap(plan('p1', 'Vežba 1'));
      http
        .expectOne((request) => request.method === 'POST' && request.url.endsWith('/sessions/plans/p1/substitute'))
        .flush(session('Planned', [plan('p1', 'Druga'), plan('p2')]));
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Vežba je zamenjena do kraja bloka.');

      vi.advanceTimersByTime(3300);
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Vežba je zamenjena do kraja bloka.');
    } finally {
      vi.useRealTimers();
    }
  });

  it('poruka posle preskakanja treninga nestaje sama', () => {
    vi.useFakeTimers();
    try {
      open('Planned');
      const component = fixture.componentInstance as unknown as { skip(): void };

      component.skip();
      http
        .expectOne((request) => request.method === 'POST' && request.url.endsWith('/skip'))
        .flush(session('Skipped', [plan('p1'), plan('p2')]));
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Trening je preskočen.');

      vi.advanceTimersByTime(3300);
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Trening je preskočen.');
    } finally {
      vi.useRealTimers();
    }
  });

  it('dodavanje kroz ekran menja trening koji se prikazuje', () => {
    open('Planned');

    labels();
    (Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find((candidate) =>
      (candidate.textContent ?? '').includes('Dodaj vežbu'),
    ) as HTMLButtonElement).click();
    http
      .expectOne((request) => request.url.endsWith('/sessions/s1/addable-exercises'))
      .flush(OPTIONS);
    fixture.detectChanges();
    (Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find((candidate) =>
      (candidate.textContent ?? '').includes('Dodaj do kraja bloka'),
    ) as HTMLButtonElement).click();
    http
      .expectOne((request) => request.method === 'POST' && request.url.endsWith('/sessions/s1/exercises'))
      .flush(session('Planned', [plan('p1'), plan('p2'), plan('p3', 'Cable Fly')]));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Cable Fly');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Vežba je dodata do kraja bloka.');
  });
});
