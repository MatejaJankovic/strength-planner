import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ExerciseCatalog } from './exercise-catalog';

/**
 * Katalog vežbi je nastao spajanjem dve kartice sa ekrana profila — spiska sopstvenih
 * vežbi i koraka opterećenja. Ni jedna ni druga nisu imale testove; test za korak je
 * najvažniji, jer taj kod ručno prepisuje vrednost u `<select>`:
 *
 * Angular ne prepisuje `[selected]` kada se model nije promenio, pa odbijena izmena ostaje
 * u prikazu i select laže o stanju servera. Ista greška je u ovoj sesiji potvrđena i
 * popravljena u `MeasureInput`, gde se videla samo na ekranu.
 */
describe('ExerciseCatalog', () => {
  let fixture: ComponentFixture<ExerciseCatalog>;
  let http: HttpTestingController;

  const BACK_SQUAT = {
    id: 'e1',
    name: 'Back Squat',
    type: 'Compound',
    equipment: 'Barbell',
    isCustom: false,
    weightStepKg: 2.5,
    defaultWeightStepKg: 2.5,
    isWeightStepOverridden: false,
    muscles: [{ muscleGroup: 'Quads', contribution: 1 }],
  };

  const BENCH = { ...BACK_SQUAT, id: 'e2', name: 'Bench Press' };
  const OWN = { ...BACK_SQUAT, id: 'e3', name: 'Hack Squat', isCustom: true };

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'profile', children: [] }]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function load(exercises: unknown[] = [BACK_SQUAT, BENCH, OWN]): void {
    fixture = TestBed.createComponent(ExerciseCatalog);

    http.expectOne((request) => request.url.endsWith('/exercises/muscle-groups')).flush([
      'Quads',
      'Glutes',
    ]);
    http.expectOne((request) => request.url.endsWith('/exercises')).flush(exercises);

    fixture.detectChanges();
  }

  function component(): any {
    return fixture.componentInstance as any;
  }

  function stepSelect(index = 0): HTMLSelectElement {
    return fixture.nativeElement.querySelectorAll('.step-controls select')[index];
  }

  it('prikazuje jedan red po vežbi, sa selektom za korak', () => {
    load();

    expect(fixture.nativeElement.querySelectorAll('.exlist__item').length).toBe(3);
    expect(fixture.nativeElement.querySelectorAll('.step-controls select').length).toBe(3);
  });

  it('sopstvena vežba nosi bedž, sistemska ne', () => {
    load();

    const rows: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.exlist__item'));
    const badges = rows.map((row) =>
      Array.from(row.querySelectorAll('.badge')).map((badge) => badge.textContent!.trim()),
    );

    expect(badges).toEqual([[], [], ['tvoja']]);
  });

  it('broji ukupno i sopstvene vežbe', () => {
    load();

    expect(component().totalCount()).toBe(3);
    expect(component().customCount()).toBe(1);
  });

  it('pretraga filtrira spisak, i prazan unos ga vraća', () => {
    load();

    component().setFilter('squat');
    fixture.detectChanges();
    expect(component().filteredExercises().map((e: any) => e.name)).toEqual([
      'Back Squat',
      'Hack Squat',
    ]);

    component().setFilter('');
    fixture.detectChanges();
    expect(component().filteredExercises().length).toBe(3);
  });

  it('pretraga ne razlikuje velika i mala slova', () => {
    load();

    component().setFilter('BENCH');

    expect(component().filteredExercises().map((e: any) => e.name)).toEqual(['Bench Press']);
  });

  it('izmena koraka se šalje serveru', () => {
    load();

    const select = stepSelect();
    select.value = '5';
    select.dispatchEvent(new Event('change'));

    const request = http.expectOne(
      (candidate) => candidate.url.includes('/weight-step') && candidate.method === 'PUT',
    );
    expect(request.request.body).toEqual({ weightStepKg: 5 });

    request.flush({ ...BACK_SQUAT, weightStepKg: 5, isWeightStepOverridden: true });
    fixture.detectChanges();

    expect(component().weightStepSaved()).toContain('Back Squat');
  });

  it('izbor iste vrednosti ne šalje zahtev', () => {
    // Bez ove provere bi svaki `change` bez izmene trošio poziv ka serveru.
    load();

    const select = stepSelect();
    select.value = '2.5';
    select.dispatchEvent(new Event('change'));

    // Nema zahteva - `afterEach` sa `http.verify()` bi pao da je nešto poslato.
    expect(component().savingStepId()).toBeNull();
  });

  /**
   * Prijavljena greška iz iste sesije, u drugoj komponenti: Angular ne prepisuje
   * `[selected]` kada se model nije promenio, pa odbijena izmena ostaje u prikazu.
   */
  it('odbijena izmena koraka se ručno vraća u select', () => {
    load();

    const select = stepSelect();
    select.value = '10';
    select.dispatchEvent(new Event('change'));

    http
      .expectOne((candidate) => candidate.url.includes('/weight-step'))
      .flush({ errors: ['Nije dozvoljeno.'] }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    // Model je ostao na 2.5, pa i select mora.
    expect(select.value).toBe('2.5');
    expect(component().weightStepError()).not.toBeNull();
    expect(component().savingStepId()).toBeNull();
  });

  it('server koji zaokruži korak diktira šta select prikazuje', () => {
    load();

    const select = stepSelect();
    select.value = '5';
    select.dispatchEvent(new Event('change'));

    // Server vraća 2.5, ne 5 - prikaz mora da prati njega, a ne izbor korisnika.
    http
      .expectOne((candidate) => candidate.url.includes('/weight-step'))
      .flush({ ...BACK_SQUAT, weightStepKg: 2.5 });
    fixture.detectChanges();

    expect(select.value).toBe('2.5');
  });

  it('vraćanje na podrazumevani korak šalje null', () => {
    load([{ ...BACK_SQUAT, weightStepKg: 5, isWeightStepOverridden: true }, BENCH, OWN]);

    component().resetWeightStep({ ...BACK_SQUAT, weightStepKg: 5 });

    const request = http.expectOne((candidate) => candidate.url.includes('/weight-step'));
    expect(request.request.body).toEqual({ weightStepKg: null });

    request.flush(BACK_SQUAT);
  });

  it('korak koji nije na listi se dodaje u ponude, da select ne padne na prvu', () => {
    // Vrednost postavljena direktno preko API-ja mora da se vidi, a ne da tiho postane 0.5.
    load();

    const options = component().stepOptionsFor({ ...BACK_SQUAT, weightStepKg: 3.75 });

    expect(options).toContain(3.75);
    expect(options).toEqual([...options].sort((a: number, b: number) => a - b));
  });

  it('dodavanje vežbe šalje primarnu i sekundarne grupe, i zatvara formu', () => {
    load();

    component().toggleAdd();
    fixture.detectChanges();
    expect(component().addOpen()).toBe(true);

    component().exerciseForm.setValue({
      name: '  Hack Squat  ',
      type: 'Compound',
      equipment: 'Machine',
      primaryMuscle: 'Quads',
    });
    component().toggleSecondary('Glutes');
    component().saveExercise();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/exercises') && candidate.method === 'POST',
    );

    expect(request.request.body.name).toBe('Hack Squat');
    expect(request.request.body.muscles).toEqual([
      { muscleGroup: 'Quads', contribution: 1.0 },
      { muscleGroup: 'Glutes', contribution: 0.5 },
    ]);

    request.flush({ ...OWN, name: 'Hack Squat' });
    fixture.detectChanges();

    // Nova vežba je na dnu spiska, pa se forma zatvara da se spisak vidi.
    expect(component().addOpen()).toBe(false);
    expect(component().exerciseSaved()).toContain('Hack Squat');
  });

  it('primarna grupa se ne šalje i kao sekundarna', () => {
    load();

    component().toggleAdd();
    component().exerciseForm.setValue({
      name: 'Front Squat',
      type: 'Compound',
      equipment: 'Barbell',
      primaryMuscle: 'Quads',
    });
    // Ista grupa dodirnuta i kao sekundarna - u zahtevu sme da stoji samo jednom.
    component().toggleSecondary('Quads');
    component().saveExercise();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/exercises') && candidate.method === 'POST',
    );

    expect(request.request.body.muscles).toEqual([{ muscleGroup: 'Quads', contribution: 1.0 }]);

    request.flush({ ...OWN, name: 'Front Squat' });
  });

  it('nevažeća forma ne šalje zahtev', () => {
    load();

    component().toggleAdd();
    component().exerciseForm.setValue({
      name: '',
      type: 'Compound',
      equipment: 'Barbell',
      primaryMuscle: '',
    });
    component().saveExercise();

    // Nema zahteva; `http.verify()` u `afterEach` to potvrđuje.
    expect(component().savingExercise()).toBe(false);
  });
});
