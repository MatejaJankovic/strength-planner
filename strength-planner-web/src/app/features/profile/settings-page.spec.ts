import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { SettingsPage } from './settings-page';
import { ACCOUNT_DELETION_WORD } from '../../core/models/auth.models';

/**
 * Podešavanja nose jedinu nepovratnu operaciju u aplikaciji, pa je ovde i najviše da se
 * pokrije: kada dugme za brisanje sme da oživi, šta ide u zahtev, i šta ostaje na ekranu
 * kada server odbije.
 *
 * `canDelete` je `computed` nad `toSignal(deleteForm.valueChanges)`, a ne nad
 * `deleteForm.valid` — jer `AbstractControl.valid` nije signal, pa bi se izračunao jednom
 * nad praznom formom i dugme nikada ne bi oživelo. Ta greška je već jednom stigla do
 * ekrana, u čarobnjaku za registraciju, i uhvaćena je samo klikanjem kroz aplikaciju.
 * Testovi ispod je hvataju.
 */
describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // logout() i back() preusmeravaju; bez ruta router baca neuhvaćeno odbijanje.
        provideRouter([
          { path: 'login', children: [] },
          { path: 'profile', children: [] },
        ]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SettingsPage);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function component(): any {
    return fixture.componentInstance as any;
  }

  function fillDelete(password: string, confirmation: string): void {
    component().deleteForm.setValue({ currentPassword: password, confirmation });
    fixture.detectChanges();
  }

  // --- kapija za brisanje ----------------------------------------------------

  it('prazna forma ne pušta brisanje', () => {
    expect(component().canDelete()).toBe(false);
  });

  it('samo lozinka nije dovoljna', () => {
    fillDelete('DiplomskiRad2026', '');

    expect(component().canDelete()).toBe(false);
  });

  it('samo potvrdna reč nije dovoljna', () => {
    fillDelete('', ACCOUNT_DELETION_WORD);

    expect(component().canDelete()).toBe(false);
  });

  it('pogrešna reč ne pušta brisanje', () => {
    fillDelete('DiplomskiRad2026', 'DELETE');

    expect(component().canDelete()).toBe(false);
  });

  it('reč bez dijakritike ne prolazi', () => {
    // Reč koju ekran prikazuje je „OBRIŠI"; „OBRISI" je druga reč.
    fillDelete('DiplomskiRad2026', 'OBRISI');

    expect(component().canDelete()).toBe(false);
  });

  it('tačna reč pušta brisanje bez obzira na velika i mala slova', () => {
    for (const word of ['OBRIŠI', 'obriši', 'Obriši', ' obriši ']) {
      fillDelete('DiplomskiRad2026', word);

      expect(component().canDelete(), `reč "${word}" nije prihvaćena`).toBe(true);
    }
  });

  /**
   * Poređenje ne sme da zavisi od jezika. Server poredi ordinalno upravo zato što na
   * turskom „i" i „I" nisu isto slovo, a reč se završava tim slovom — pa bi ekran i server
   * mogli da se raziđu na istom unosu iako im je reč ista.
   */
  it('poređenje ne zavisi od jezika prikaza', () => {
    fillDelete('DiplomskiRad2026', 'obriši');

    expect(component().canDelete()).toBe(true);
    expect(ACCOUNT_DELETION_WORD.toLowerCase().toUpperCase()).toBe(ACCOUNT_DELETION_WORD);
  });

  // --- zahtev ----------------------------------------------------------------

  it('brisanje šalje lozinku i potvrdu, i odjavljuje', () => {
    fillDelete('DiplomskiRad2026', 'obriši');
    component().deleteAccount();

    const request = http.expectOne(
      (candidate) =>
        candidate.url.endsWith('/auth/delete-account') && candidate.method === 'POST',
    );

    expect(request.request.body).toEqual({
      currentPassword: 'DiplomskiRad2026',
      confirmation: 'obriši',
    });

    request.flush(null, { status: 204, statusText: 'No Content' });

    // Odjava je u servisu, jer token, keševi i slika moraju da odu istim putem kao pri
    // običnoj odjavi.
    expect(localStorage.getItem('strength-planner.token')).toBeNull();
  });

  it('odbijeno brisanje ostavlja poruku i ne dira formu', () => {
    fillDelete('PogresnaLozinka1', 'obriši');
    component().deleteAccount();

    http
      .expectOne((candidate) => candidate.url.endsWith('/auth/delete-account'))
      .flush(
        { errors: ['Pogrešan email ili lozinka.'] },
        { status: 400, statusText: 'Bad Request' },
      );

    expect(component().deleting()).toBe(false);
    expect(component().deleteError()).toContain('Pogrešan');
    // Korisnik mora da može da ispravi lozinku bez ponovnog kucanja reči.
    expect(component().deleteForm.controls.confirmation.value).toBe('obriši');
  });

  it('brisanje se ne šalje dok kapija ne propusti', () => {
    fillDelete('DiplomskiRad2026', 'DELETE');

    component().deleteAccount();

    // Nema zahteva - `afterEach` sa `http.verify()` bi pao da je nešto poslato.
    expect(component().deleting()).toBe(false);
  });

  it('odustajanje zatvara formu i briše upisanu lozinku', () => {
    component().openDelete();
    fillDelete('DiplomskiRad2026', 'obriši');

    component().cancelDelete();

    expect(component().deleteOpen()).toBe(false);
    expect(component().deleteForm.controls.currentPassword.value).toBe('');
    expect(component().deleteError()).toBeNull();
  });

  // --- lozinka ---------------------------------------------------------------

  it('promena lozinke ne šalje ništa dok se potvrda ne poklapa', () => {
    component().passwordForm.setValue({
      currentPassword: 'StaraLozinka1234',
      newPassword: 'NovaLozinka1234',
      confirmPassword: 'NestoDrugo1234',
    });

    component().changePassword();

    // Nema zahteva; `http.verify()` u `afterEach` to potvrđuje.
    expect(component().passwordForm.hasError('passwordMismatch')).toBe(true);
  });

  it('promena lozinke prazni formu posle uspeha', () => {
    component().passwordForm.setValue({
      currentPassword: 'StaraLozinka1234',
      newPassword: 'NovaLozinka1234',
      confirmPassword: 'NovaLozinka1234',
    });

    component().changePassword();

    const request = http.expectOne((candidate) =>
      candidate.url.endsWith('/auth/change-password'),
    );
    // Potvrda se ne šalje serveru; ona postoji samo da uhvati grešku u kucanju.
    expect(request.request.body).toEqual({
      currentPassword: 'StaraLozinka1234',
      newPassword: 'NovaLozinka1234',
    });

    request.flush({ userId: 'u1', email: 'ja@primer.com', token: 'nov', expiresAt: '' });

    // Lozinka ne sme da ostane u formi posle uspešne izmene.
    expect(component().passwordForm.controls.currentPassword.value).toBe('');
    expect(component().passwordSaved()).toBe(true);
  });
});
