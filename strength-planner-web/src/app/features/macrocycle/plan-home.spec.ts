import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { PlanHome } from './plan-home';
import { MesocycleService } from '../../core/api/mesocycle.service';

/**
 * Plan je otkako je pravljenje mezociklusa uklonjeno **jedini** ulaz u trening, pa je i
 * jedino mesto sa kog se briše. Brisanje je nepovratno i odnosi sve odrađene serije, pa
 * ovi testovi drže dve stvari koje se na snimku ekrana ne vide: da zahtev ide na plan a ne
 * na mezociklus, i da posle njega na ekranu ne ostane ni plan ni keširani trening.
 */
describe('PlanHome - brisanje plana', () => {
  let fixture: ComponentFixture<PlanHome>;
  let http: HttpTestingController;

  const plan = {
    id: 'plan-1',
    name: 'Zima 2026',
    startDate: '2026-01-05',
    isActive: true,
    blocks: [
      {
        id: 'block-1',
        order: 1,
        goal: 1,
        templateKey: 'upper-lower',
        templateName: 'Upper/Lower',
        periodizationModel: 0,
        durationWeeks: 4,
        status: 'active',
        mesocycleId: 'meso-1',
        completedSessions: 0,
        totalSessions: 16,
      },
    ],
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [PlanHome],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlanHome);

    // Konstruktor odmah učitava aktivan plan.
    http.expectOne((request) => request.url.endsWith('/macrocycles/active')).flush(plan);

    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  /** Pristup zaštićenim članovima komponente; test je jedini koji to radi. */
  function component(): any {
    return fixture.componentInstance as any;
  }

  it('šalje brisanje na plan, a ne na mezociklus', async () => {
    component().requestDelete();
    component().confirmDelete();

    // Ranije je ekran „Trening" brisao mezociklus, što je blok ostavljalo bez treninga -
    // pa ga je plan pri sledećem čitanju ponovo generisao i obrisano se vraćalo.
    const request = http.expectOne((item) => item.url.endsWith('/macrocycles/plan-1'));
    expect(request.request.method).toBe('DELETE');

    request.flush(null);
    await fixture.whenStable();
  });

  it('posle brisanja ne ostavlja ni plan ni keširan trening', async () => {
    const mesocycles = TestBed.inject(MesocycleService);

    component().requestDelete();
    component().confirmDelete();
    http.expectOne((item) => item.url.endsWith('/macrocycles/plan-1')).flush(null);
    await fixture.whenStable();

    expect(component().plan()).toBeNull();
    // Prazno stanje mora da se prikaže; bez ovoga bi ekran ostao na starom planu.
    expect(component().notFound()).toBe(true);
    // Trening je nestao zajedno sa planom.
    expect(mesocycles.active()).toBeNull();
  });

  it('traži potvrdu pre brisanja', () => {
    expect(component().confirmingDelete()).toBe(false);

    component().requestDelete();
    expect(component().confirmingDelete()).toBe(true);

    component().cancelDelete();
    expect(component().confirmingDelete()).toBe(false);

    // Odustajanje ne sme da pošalje nijedan zahtev.
    http.expectNone((item) => item.url.includes('/macrocycles/plan-1'));
  });

  it('zadržava plan na ekranu kada brisanje ne uspe', async () => {
    component().requestDelete();
    component().confirmDelete();

    http
      .expectOne((item) => item.url.endsWith('/macrocycles/plan-1'))
      .flush({ message: 'Neuspelo' }, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    // Plan i dalje postoji, pa mora da ostane vidljiv uz poruku o grešci.
    expect(component().plan()).not.toBeNull();
    expect(component().deleteError()).toBeTruthy();
  });
});

/**
 * Padajući meni bloka pokazuje samo naziv šablona. Ekran za mezociklus, koji je jedini
 * prikazivao koje vežbe šablon nosi, više ne postoji - pa čarobnjak mora sam da pokaže
 * sadržaj izabranog šablona.
 */
describe('PlanHome - sadržaj šablona u čarobnjaku', () => {
  let fixture: ComponentFixture<PlanHome>;
  let http: HttpTestingController;

  const templates = [
    {
      key: 'upper-lower',
      name: 'Upper/Lower',
      isCustom: false,
      note: null,
      days: [
        { name: 'Upper', exercises: ['Bench Press', 'Barbell Row'] },
        { name: 'Lower', exercises: ['Back Squat'] },
      ],
    },
    {
      key: 'custom:abc',
      name: 'Moj Upper/Lower',
      isCustom: true,
      note: null,
      days: [{ name: 'Dan 1', exercises: ['Deadlift'] }],
    },
  ];

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [PlanHome],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlanHome);

    http
      .expectOne((request) => request.url.endsWith('/macrocycles/active'))
      .flush(null, { status: 404, statusText: 'Not Found' });

    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function component(): any {
    return fixture.componentInstance as any;
  }

  function openWizard(): void {
    component().openWizard();
    http.expectOne((request) => request.url.endsWith('/templates')).flush(templates);
    http
      .expectOne((request) => request.url.includes('/macrocycles/suggested-blocks'))
      .flush([{ goal: 1, templateKey: 'upper-lower', periodizationModel: 2 }]);
  }

  it('deli šablone na lične i ugrađene', async () => {
    openWizard();
    await fixture.whenStable();

    expect(component().customTemplates().map((item: any) => item.key)).toEqual(['custom:abc']);
    expect(component().builtInTemplates().map((item: any) => item.key)).toEqual(['upper-lower']);
  });

  it('vraća dane izabranog šablona', async () => {
    openWizard();
    await fixture.whenStable();

    const days = component().templateDays('upper-lower');
    expect(days.map((day: any) => day.name)).toEqual(['Upper', 'Lower']);
    expect(days[0].exercises).toEqual(['Bench Press', 'Barbell Row']);
  });

  /**
   * Rezervni šablon, koji ekran koristi kada spisak ne stigne, nema dane. Prazan spisak se
   * vraća kao null da se ne bi prikazao prazan okvir ispod menija.
   */
  it('ne prikazuje prazan spisak dana', async () => {
    openWizard();
    await fixture.whenStable();

    expect(component().templateDays('nepostojeci')).toBeNull();
  });
});
