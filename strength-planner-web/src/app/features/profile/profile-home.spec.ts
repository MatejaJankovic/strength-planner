import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ProfileHome } from './profile-home';
import { Sex } from '../../core/models/auth.models';

/**
 * Prijavljena greška: izabrani pol se pri povratku na profil nije prikazivao.
 *
 * Uzrok je bio da registracija upisuje "male"/"female", a profil nudi "M"/"F"; Angular za
 * vrednost koja ne odgovara nijednoj opciji ostavlja meni prazan, bez ijedne poruke. Test
 * ide kroz isti put: odgovor servera -> vrednost polja -> telo zahteva pri čuvanju.
 */
describe('ProfileHome - pol se prikazuje i vraća serveru', () => {
  let fixture: ComponentFixture<ProfileHome>;
  let http: HttpTestingController;

  function load(sex: unknown, extra: Record<string, unknown> = {}): void {
    fixture = TestBed.createComponent(ProfileHome);

    http.expectOne((request) => request.url.endsWith('/auth/me')).flush({
      id: 'u1',
      email: 'ja@primer.com',
      sex,
      age: 23,
      bodyweightKg: 120,
      experienceLevel: 2,
      ...extra,
    });
    http.expectOne((request) => request.url.endsWith('/exercises/muscle-groups')).flush([]);
    http.expectOne((request) => request.url.endsWith('/exercises')).flush([]);
  }

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      imports: [ProfileHome],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function form(): any {
    return (fixture.componentInstance as any).profileForm;
  }

  it('označava pol koji je server vratio', () => {
    load(Sex.Male);

    expect(form().controls.sex.value).toBe('0');
  });

  it('označava i drugu vrednost, ne samo prvu', () => {
    load(Sex.Female);

    expect(form().controls.sex.value).toBe('1');
  });

  it('prazan pol ostaje neoznačen', () => {
    load(null);

    expect(form().controls.sex.value).toBe('');
  });

  it('vrednost koju enum ne definiše ne obara ekran', () => {
    // Zatečeni nalozi su imali "M", "Male" i "male" u istoj koloni. Migracija ih prevodi,
    // ali ekran ne sme da padne ako se takvo šta ipak pojavi.
    load('nešto sasvim treće');

    expect(form().controls.sex.value).toBe('');
  });

  it('čuva pol kao broj koji server očekuje', () => {
    load(Sex.Male);

    form().controls.sex.setValue('1');
    (fixture.componentInstance as any).saveProfile();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/profile') && candidate.method === 'PUT',
    );

    expect(request.request.body.sex).toBe(Sex.Female);
    // Polje je uklonjeno sa oba ekrana i iz DTO-a; ne sme da se vrati kroz telo zahteva.
    expect(request.request.body.trainingDaysPerWeek).toBeUndefined();

    request.flush({ id: 'u1', email: 'ja@primer.com', sex: Sex.Female, age: 23, bodyweightKg: 120 });
  });

  it('"ne želim da navedem" šalje prazno, a ne nulu', () => {
    load(Sex.Male);

    form().controls.sex.setValue('');
    (fixture.componentInstance as any).saveProfile();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/profile') && candidate.method === 'PUT',
    );

    // Number('') je nula, a nula je Male - zato se prazan string proverava pre pretvaranja.
    expect(request.request.body.sex).toBeNull();

    request.flush({ id: 'u1', email: 'ja@primer.com', sex: null, age: 23, bodyweightKg: 120 });
  });

  /**
   * `PUT /api/auth/profile` je potpuna zamena profila: polje koje se ne pošalje server
   * upisuje kao prazno.
   *
   * Registracija je postala čarobnjak koji traži ime i visinu, a ovaj ekran ih pre toga
   * nije imao — pa je prvo čuvanje telesne mase brisalo oboje, a korisnik je video samo
   * poruku da je profil sačuvan. Test ide istim putem: odgovor servera -> polje -> telo
   * zahteva.
   */
  it('čuvanje profila ne briše ime i visinu unete u registraciji', () => {
    load(Sex.Male, { displayName: 'Mateja', heightCm: 183 });

    expect(form().controls.displayName.value).toBe('Mateja');
    expect(form().controls.heightCm.value).toBe('183');

    (fixture.componentInstance as any).saveProfile();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/profile') && candidate.method === 'PUT',
    );

    expect(request.request.body.displayName).toBe('Mateja');
    expect(request.request.body.heightCm).toBe(183);

    request.flush({
      id: 'u1',
      email: 'ja@primer.com',
      displayName: 'Mateja',
      heightCm: 183,
      sex: Sex.Male,
      age: 23,
      bodyweightKg: 120,
    });
  });

  it('prazno ime i prazna visina idu kao null, a ne kao prazan string ili nula', () => {
    // Number('') je nula, a nula je vrednost koju [Range] odbija — greška bi stigla sa
    // servera umesto da polje uopšte ne bude poslato kao broj.
    load(Sex.Male);

    expect(form().controls.displayName.value).toBe('');

    (fixture.componentInstance as any).saveProfile();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/profile') && candidate.method === 'PUT',
    );

    expect(request.request.body.displayName).toBeNull();
    expect(request.request.body.heightCm).toBeNull();

    request.flush({ id: 'u1', email: 'ja@primer.com', sex: Sex.Male, age: 23, bodyweightKg: 120 });
  });
});
