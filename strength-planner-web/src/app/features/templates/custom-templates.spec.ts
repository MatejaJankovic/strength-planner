import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CustomTemplates } from './custom-templates';

/**
 * Editor ličnog šablona radi nad ugnježdenim spiskom (dani, pa vežbe u danu), a takav
 * spisak je lako pogrešno adresirati: u ugnježdenom `@for` je `$index` indeks unutrašnje
 * petlje, ne spoljašnje. Prva verzija je zbog toga iz drugog dana brisala vežbu prvog.
 *
 * Ovi testovi ne proveravaju izgled nego adresiranje i pravila koja server posle odbija,
 * jer se to dvoje ne vidi na snimku ekrana.
 */
describe('CustomTemplates - editor ličnog šablona', () => {
  let fixture: ComponentFixture<CustomTemplates>;
  let http: HttpTestingController;

  /** Katalog vežbi koji ekran nudi u padajućem meniju. */
  const exercises = [
    { id: 'ex-squat', name: 'Back Squat' },
    { id: 'ex-bench', name: 'Bench Press' },
    { id: 'ex-row', name: 'Barbell Row' },
  ];

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [CustomTemplates],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CustomTemplates);

    // Konstruktor odmah učitava šablone i vežbe.
    http.expectOne((request) => request.url.endsWith('/templates/custom')).flush([]);
    http.expectOne((request) => request.url.endsWith('/exercises')).flush(exercises);

    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  /** Pristup zaštićenim članovima komponente; test je jedini koji to radi. */
  function component(): any {
    return fixture.componentInstance as any;
  }

  function buildTwoDays(): void {
    const instance = component();
    instance.startNew();
    instance.addDay();

    instance.addExercise(0, 'ex-squat');
    instance.addExercise(1, 'ex-bench');
  }

  it('menja vežbu u danu koji je zaista izabran', () => {
    const instance = component();
    buildTwoDays();

    instance.setExerciseNumber(1, 0, 'sets', '7');

    // Drugi dan je dobio izmenu, prvi je ostao netaknut. Sa pogrešnim indeksom bi bilo
    // obrnuto, a oba dana imaju po jednu vežbu pa se greška ne bi videla po dužini spiska.
    expect(instance.days()[1].exercises[0].sets).toBe(7);
    expect(instance.days()[0].exercises[0].sets).toBe(3);
  });

  it('briše vežbu iz dana koji je zaista izabran', () => {
    const instance = component();
    buildTwoDays();

    instance.removeExercise(1, 0);

    expect(instance.days()[0].exercises.length).toBe(1);
    expect(instance.days()[1].exercises.length).toBe(0);
  });

  it('drži serije i ponavljanja unutar granica koje propis nedelje ume da izrazi', () => {
    const instance = component();
    instance.startNew();
    instance.addExercise(0, 'ex-squat');

    instance.setExerciseNumber(0, 0, 'sets', '99');
    instance.setExerciseNumber(0, 0, 'repRangeMax', '40');

    expect(instance.days()[0].exercises[0].sets).toBe(10);
    expect(instance.days()[0].exercises[0].repRangeMax).toBe(12);

    instance.setExerciseNumber(0, 0, 'sets', '0');
    expect(instance.days()[0].exercises[0].sets).toBe(2);
  });

  it('opseg ostaje opseg kada se donja granica podigne preko gornje', () => {
    const instance = component();
    instance.startNew();
    instance.addExercise(0, 'ex-squat');

    // Vežba kreće od 8-12; donja granica na 11 mora da povuče gornju sa sobom umesto da
    // server odbije ceo šablon zbog jednog polja.
    instance.setExerciseNumber(0, 0, 'repRangeMin', '11');
    expect(instance.days()[0].exercises[0].repRangeMax).toBeGreaterThanOrEqual(11);

    instance.setExerciseNumber(0, 0, 'repRangeMax', '4');
    expect(instance.days()[0].exercises[0].repRangeMin).toBeLessThanOrEqual(4);
  });

  it('ne nudi vežbu koju dan već ima', () => {
    const instance = component();
    instance.startNew();
    instance.addExercise(0, 'ex-squat');

    const offered = instance.availableFor(0).map((exercise: { id: string }) => exercise.id);

    expect(offered).not.toContain('ex-squat');
    expect(offered).toContain('ex-bench');
  });

  it('ne dozvoljava čuvanje dva dana istog naziva', () => {
    const instance = component();
    buildTwoDays();

    instance.setDayName(1, instance.days()[0].name);
    instance.name.set('Moj plan');

    // Naziv dana postaje oznaka treninga po kojoj deload prepoznaje dan, pa dva ista
    // znače da se polazni broj serija čita iz pogrešnog treninga.
    expect(instance.canSave()).toBe(false);
    expect(instance.saveBlockedReason()).toContain('razlikuju');
  });

  it('novi dan dobija slobodan naziv', () => {
    const instance = component();
    instance.startNew();
    instance.setDayName(0, 'Dan 2');
    instance.addDay();

    const names = instance.days().map((day: { name: string }) => day.name);

    expect(new Set(names).size).toBe(names.length);
  });

  it('ne dozvoljava čuvanje dana bez ijedne vežbe', () => {
    const instance = component();
    instance.startNew();
    instance.name.set('Moj plan');

    expect(instance.canSave()).toBe(false);
    expect(instance.saveBlockedReason()).toContain('vežbu');
  });

  it('šalje dane i vežbe onim redom kojim su unete', () => {
    const instance = component();
    buildTwoDays();
    instance.addExercise(0, 'ex-row');
    instance.name.set('Moj plan');

    instance.save();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/templates/custom') && candidate.method === 'POST',
    );

    expect(request.request.body.name).toBe('Moj plan');
    expect(request.request.body.days.length).toBe(2);
    expect(request.request.body.days[0].exercises.map((item: any) => item.exerciseId)).toEqual([
      'ex-squat',
      'ex-row',
    ]);
    expect(request.request.body.days[1].exercises[0].exerciseId).toBe('ex-bench');

    request.flush({ id: 't1', key: 'custom:t1', name: 'Moj plan', days: [] });

    // Posle čuvanja ekran se osvežava. Spisak vežbi tada NE ide ponovo na mrežu:
    // ExerciseService ga kešira posle prvog učitavanja i vraća iz signala.
    http.expectOne((candidate) => candidate.url.endsWith('/templates/custom')).flush([]);
  });
});
