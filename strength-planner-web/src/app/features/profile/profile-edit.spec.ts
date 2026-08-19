import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ProfileEdit } from './profile-edit';
import { Sex } from '../../core/models/auth.models';

/**
 * Ekran za izmenu profila. Testovi su prethodno stajali uz ekran profila, jer je forma
 * bila tamo; sa izdvajanjem forme na svoju rutu preseljeni su ovamo, jer i dalje drže dve
 * prijavljene greške.
 *
 * Prva: izabrani pol se pri povratku nije prikazivao. Registracija je upisivala
 * "male"/"female", a profil nudio "M"/"F"; Angular za vrednost koja ne odgovara nijednoj
 * opciji ostavlja meni prazan, bez ijedne poruke.
 *
 * Druga: `PUT /api/auth/profile` zamenjuje profil u celini, pa je čuvanje bez novih polja
 * brisalo ime i visinu unete pri registraciji, uz poruku da je profil sačuvan.
 */
describe('ProfileEdit', () => {
  let fixture: ComponentFixture<ProfileEdit>;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // save() i cancel() se vraćaju na /profile; bez te rute router baca neuhvaćeno
        // odbijanje koje ne obori test ali zaprlja izlaz.
        provideRouter([{ path: 'profile', children: [] }]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Diže ekran i odgovara na oba zahteva koja on šalje pri učitavanju. */
  function load(sex: unknown, extra: Record<string, unknown> = {}): void {
    fixture = TestBed.createComponent(ProfileEdit);

    http.expectOne((request) => request.url.endsWith('/auth/me')).flush({
      id: 'u1',
      email: 'ja@primer.com',
      sex,
      age: 23,
      bodyweightKg: 120,
      experienceLevel: 2,
      ...extra,
    });

    // Nalog bez slike vraća 404. To je odgovor, ne greška.
    http
      .expectOne((request) => request.url.endsWith('/auth/avatar'))
      .flush(null, { status: 404, statusText: 'Not Found' });
  }

  function form(): any {
    return (fixture.componentInstance as any).form;
  }

  function save(): void {
    (fixture.componentInstance as any).save();
  }

  function expectProfilePut() {
    return http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/profile') && candidate.method === 'PUT',
    );
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
    save();

    const request = expectProfilePut();

    expect(request.request.body.sex).toBe(Sex.Female);
    // Polje je uklonjeno sa oba ekrana i iz DTO-a; ne sme da se vrati kroz telo zahteva.
    expect(request.request.body.trainingDaysPerWeek).toBeUndefined();

    request.flush({ id: 'u1', email: 'ja@primer.com', sex: Sex.Female, age: 23, bodyweightKg: 120 });
  });

  it('"ne želim da navedem" šalje prazno, a ne nulu', () => {
    load(Sex.Male);

    form().controls.sex.setValue('');
    save();

    const request = expectProfilePut();

    // Number('') je nula, a nula je Male - zato se prazan string proverava pre pretvaranja.
    expect(request.request.body.sex).toBeNull();

    request.flush({ id: 'u1', email: 'ja@primer.com', sex: null, age: 23, bodyweightKg: 120 });
  });

  it('čuvanje ne briše ime i visinu unete u registraciji', () => {
    load(Sex.Male, { displayName: 'Mateja', heightCm: 183 });

    expect(form().controls.displayName.value).toBe('Mateja');
    expect(form().controls.heightCm.value).toBe('183');

    save();

    const request = expectProfilePut();

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
    // Number('') je nula, a nulu [Range] na serveru odbija - greška bi stigla sa servera
    // umesto da polje uopšte ne bude poslato kao broj.
    load(Sex.Male);

    expect(form().controls.displayName.value).toBe('');

    save();

    const request = expectProfilePut();

    expect(request.request.body.displayName).toBeNull();
    expect(request.request.body.heightCm).toBeNull();

    request.flush({ id: 'u1', email: 'ja@primer.com', sex: Sex.Male, age: 23, bodyweightKg: 120 });
  });

  it('nalog bez slike ne obara ekran', () => {
    // `forkJoin` pada na prvoj grešci, pa bi 404 na slici oborio i učitavanje profila.
    load(Sex.Male);

    expect((fixture.componentInstance as any).error()).toBeNull();
    expect((fixture.componentInstance as any).loading()).toBe(false);
  });

  it('slika se otprema kao multipart i prikaz se osvežava sa servera', () => {
    load(Sex.Male);

    const input = { files: [new File([new Uint8Array([0xff, 0xd8, 0xff])], 'lice.jpg')], value: 'x' };
    (fixture.componentInstance as any).pickAvatar(input);

    // Birač se prazni odmah, inače izbor iste datoteke drugi put ne pokreće `change`.
    expect(input.value).toBe('');

    const upload = http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/avatar') && candidate.method === 'PUT',
    );
    expect(upload.request.body).toBeInstanceOf(FormData);
    upload.flush({ id: 'u1', email: 'ja@primer.com', hasAvatar: true });

    // Prikaz se čita sa servera, a ne iz izabranog fajla: server je odlučio da sadržaj
    // jeste slika i pod kojim tipom je vraća.
    http
      .expectOne((candidate) => candidate.url.endsWith('/auth/avatar') && candidate.method === 'GET')
      .flush(new Blob([new Uint8Array([0xff, 0xd8, 0xff])], { type: 'image/jpeg' }));

    expect((fixture.componentInstance as any).avatarBusy()).toBe(false);
  });

  it('prevelika slika se odbija bez ijednog zahteva', () => {
    load(Sex.Male);

    const tooBig = new File([new Uint8Array(3 * 1024 * 1024)], 'ogromna.png');
    (fixture.componentInstance as any).pickAvatar({ files: [tooBig], value: 'x' });

    // Nema zahteva - `afterEach` sa `http.verify()` bi pao da je nešto poslato.
    expect((fixture.componentInstance as any).avatarError()).toContain('2 MB');
  });
});
