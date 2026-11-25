import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { HistoryEntryDto, TrainingHistoryDto } from '../../core/models/training.models';
import { TrainingHistory } from './training-history';

function entry(id: string, date: Date, overrides: Partial<HistoryEntryDto> = {}): HistoryEntryDto {
  return {
    sessionId: id,
    status: 'Completed',
    date: date.toISOString(),
    dateKnown: true,
    dayLabel: 'Day A',
    weekNumber: 1,
    isDeload: false,
    planName: 'Plan jesen',
    blockName: 'Blok 1',
    goal: 'Strength',
    setCount: 12,
    tonnageKg: 4380,
    exercises: ['Back Squat', 'Pull-up'],
    ...overrides,
  };
}

function pageOf(items: HistoryEntryDto[], total: number, page = 1): TrainingHistoryDto {
  return { items, total, page, pageSize: 20 };
}

/**
 * Istorija treninga čita stranicu po stranicu i drži spisak u ekranu, ne u servisu: ništa ne sme
 * da ostane iza odjave. Ovde se proverava šta ekran radi sa onim što server vrati.
 */
describe('TrainingHistory', () => {
  let fixture: ComponentFixture<TrainingHistory>;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'session/:id', children: [] },
          { path: 'profile', children: [] },
        ]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function open(): void {
    fixture = TestBed.createComponent(TrainingHistory);
  }

  function request(page: number) {
    return http.expectOne(
      (candidate) =>
        candidate.url.endsWith('/history') && candidate.params.get('page') === String(page),
    );
  }

  function text(): string {
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('traži prvu stranicu i grupiše treninge po mesecima', () => {
    open();
    request(1).flush(
      pageOf(
        [
          entry('a', new Date(2026, 10, 12, 12)),
          entry('b', new Date(2026, 10, 5, 12)),
          entry('c', new Date(2026, 9, 28, 12)),
        ],
        3,
      ),
    );

    const rendered = text();

    expect(rendered).toContain('Novembar 2026');
    expect(rendered).toContain('Oktobar 2026');
    expect(rendered).toContain('3 treninga kroz sve planove');
    expect(fixture.nativeElement.querySelectorAll('.entry').length).toBe(3);
    // Sve stane na jednu stranicu, pa nema dugmeta za još.
    expect(rendered).not.toContain('Učitaj još');
  });

  it('prikazuje šta je urađeno: serije, tonažu i vežbe', () => {
    open();
    request(1).flush(pageOf([entry('a', new Date(2026, 10, 12, 12))], 1));

    const rendered = text();

    expect(rendered).toContain('12 serija');
    expect(rendered).toContain('4 380 kg');
    expect(rendered).toContain('Back Squat, Pull-up');
    expect(rendered).toContain('čet, 12. nov');
  });

  it('preskočen trening je označen, nema serija, a nepoznat dan se ne piše kao datum', () => {
    open();
    request(1).flush(
      pageOf(
        [
          entry('s', new Date(2026, 10, 12, 12), {
            status: 'Skipped',
            dateKnown: false,
            weekNumber: 3,
            setCount: 0,
            tonnageKg: 0,
            exercises: [],
          }),
        ],
        1,
      ),
    );

    const rendered = text();

    expect(rendered).toContain('Preskočen');
    // Nedelja stoji u podnaslovu, a datum se ne izmišlja.
    expect(rendered).toContain('nedelja 3');
    expect((fixture.nativeElement as HTMLElement).querySelector('.entry__when')).toBeNull();
    expect(rendered).not.toContain('čet, 12. nov');
    expect(rendered).not.toContain('0 serija');
  });

  it('"Učitaj još" dodaje sledeću stranicu na kraj, a dugme nestaje kad je sve učitano', () => {
    open();
    request(1).flush(pageOf([entry('a', new Date(2026, 10, 12, 12))], 2));

    expect(text()).toContain('Učitaj još');

    const more = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.btn--more');
    more?.click();
    request(2).flush(pageOf([entry('b', new Date(2026, 9, 1, 12))], 2, 2));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.entry').length).toBe(2);
    expect(text()).not.toContain('Učitaj još');
  });

  it('neuspelo "Učitaj još" ostavlja učitano i ponavlja istu stranicu', () => {
    open();
    request(1).flush(pageOf([entry('a', new Date(2026, 10, 12, 12))], 3));
    text();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.btn--more')?.click();
    request(2).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    // Prva stranica je i dalje tu, greška stoji uz dugme, a dugme nudi ponovni pokušaj.
    expect(fixture.nativeElement.querySelectorAll('.entry').length).toBe(1);
    expect(text()).toContain('Ne mogu da učitam istoriju');
    expect(text()).toContain('Pokušaj ponovo');

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.btn--more')?.click();
    request(2).flush(
      pageOf([entry('b', new Date(2026, 10, 5, 12)), entry('c', new Date(2026, 10, 1, 12))], 3, 2),
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.entry').length).toBe(3);
    expect(text()).not.toContain('Ne mogu da učitam istoriju');
  });

  it('trening koji se pojavi na dve stranice se prikazuje jednom', () => {
    open();
    request(1).flush(pageOf([entry('a', new Date(2026, 10, 12, 12))], 3));
    text();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.btn--more')?.click();
    request(2).flush(
      pageOf([entry('a', new Date(2026, 10, 12, 12)), entry('b', new Date(2026, 10, 5, 12))], 3, 2),
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.entry').length).toBe(2);
  });

  it('prazna istorija kaže da još nema treninga', () => {
    open();
    request(1).flush(pageOf([], 0));

    expect(text()).toContain('Još nema odrađenih treninga');
  });

  it('greška se prikazuje sa ponovnim pokušajem koji traži prvu stranicu iznova', () => {
    open();
    request(1).flush(null, { status: 500, statusText: 'Server Error' });

    expect(text()).toContain('Pokušaj ponovo');

    const retry = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.btn');
    retry?.click();
    request(1).flush(pageOf([entry('a', new Date(2026, 10, 12, 12))], 1));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.entry').length).toBe(1);
  });

  it('dodir na trening otvara ga', () => {
    open();
    request(1).flush(pageOf([entry('abc', new Date(2026, 10, 12, 12))], 1));
    text();

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.entry')?.click();

    expect(navigate).toHaveBeenCalledWith(['/session', 'abc']);
  });
});
