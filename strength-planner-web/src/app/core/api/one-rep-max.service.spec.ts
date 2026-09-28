import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { OneRepMaxService } from './one-rep-max.service';
import { API_BASE_URL } from './api-base';
import { OneRepMaxDto } from '../models/analytics.models';

/**
 * Ekran „Poznati maksimumi" prikazuje vrednost **od koje plan zaista polazi**, a nju bira
 * pravilo nad svim zapisima (najbolja u prozoru od 56 dana). Zato izmena koja može da
 * promeni izbor ne sme da upiše svoj rezultat u keš kao tekuću vrednost.
 *
 * Ovo je već jednom promaklo, u ovoj istoj grani: procena iz test-serije se upisivala
 * lokalno, pa je ekran pokazivao 66 kg dok je plan polazio od 121 — tačno onaj nesklad
 * između ekrana i generatora zbog kog je nalaz D19 i postojao.
 */
describe('OneRepMaxService — keš ne sme da pogađa koja je vrednost tekuća', () => {
  const base = 'http://localhost/api';
  let service: OneRepMaxService;
  let http: HttpTestingController;

  const existing: OneRepMaxDto = {
    id: 'record-high',
    exerciseId: 'bench',
    exercise: 'Bench Press',
    valueKg: 121,
    source: 'Estimated',
    recordedAt: '2026-09-01T00:00:00Z',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: base },
      ],
    });

    service = TestBed.inject(OneRepMaxService);
    http = TestBed.inject(HttpTestingController);

    service.load().subscribe();
    http.expectOne(`${base}/onerepmax`).flush([existing]);
  });

  afterEach(() => http.verify());

  it('ne menja prikazanu vrednost kada test-serija da nižu procenu', () => {
    service
      .saveFromSet({ exerciseId: 'bench', weightKg: 50, reps: 2, rir: 0 })
      .subscribe();

    http.expectOne(`${base}/onerepmax/from-set`).flush({
      id: 'record-low',
      exerciseId: 'bench',
      exercise: 'Bench Press',
      valueKg: 53.3,
      source: 'Estimated',
      recordedAt: '2026-09-28T00:00:00Z',
    });

    // Novi zapis postoji, ali tekuća vrednost je i dalje ono što je server rekao da jeste.
    expect(service.oneRepMaxes().map((item) => item.valueKg)).toEqual([121]);
  });

  it('ne uklanja red sam od sebe pri brisanju - iza njega može da stoji stariji zapis', () => {
    service.remove('record-high').subscribe();
    http.expectOne(`${base}/onerepmax/record-high`).flush(null);

    expect(service.oneRepMaxes()).toHaveLength(1);
  });

  it('ručni unos ostaje jedini koji sme da prepiše keš, jer poništava starije procene', () => {
    service.save({ exerciseId: 'bench', valueKg: 130 }).subscribe();
    http.expectOne(`${base}/onerepmax`).flush({
      id: 'record-manual',
      exerciseId: 'bench',
      exercise: 'Bench Press',
      valueKg: 130,
      source: 'Manual',
      recordedAt: '2026-09-28T00:00:00Z',
    });

    expect(service.oneRepMaxes().map((item) => item.valueKg)).toEqual([130]);
  });
});
