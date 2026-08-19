import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ProfileHome } from './profile-home';
import { Sex } from '../../core/models/auth.models';

/**
 * Ekran profila je posle izdvajanja forme na `/profile/edit` postao pregled: naslov,
 * slika i pročitani podaci o vežbaču.
 *
 * Testovi forme (pol koji se vraća serveru, ime i visina koje čuvanje ne sme da obriše)
 * preseljeni su u `profile-edit.spec.ts` zajedno sa formom.
 */
describe('ProfileHome — pregled profila', () => {
  let fixture: ComponentFixture<ProfileHome>;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function load(me: Record<string, unknown>, avatar?: Blob): void {
    fixture = TestBed.createComponent(ProfileHome);

    http.expectOne((request) => request.url.endsWith('/auth/me')).flush(me);
    http.expectOne((request) => request.url.endsWith('/exercises/muscle-groups')).flush([]);
    http.expectOne((request) => request.url.endsWith('/exercises')).flush([]);

    const request = http.expectOne((candidate) => candidate.url.endsWith('/auth/avatar'));
    if (avatar) {
      request.flush(avatar);
    } else {
      request.flush(null, { status: 404, statusText: 'Not Found' });
    }
  }

  function component(): any {
    return fixture.componentInstance as any;
  }

  it('naslov je ime kada ga nalog ima', () => {
    load({ id: 'u1', email: 'ja@primer.com', displayName: 'Mateja', age: 27, bodyweightKg: 82.5 });

    expect(component().title()).toBe('Mateja');
  });

  it('naslov pada na email kada imena nema', () => {
    // Nalozi napravljeni pre uvođenja imena ga nemaju i nema odakle da im se izvede;
    // prazan naslov bi bio gori od email adrese.
    load({ id: 'u1', email: 'ja@primer.com', age: 27, bodyweightKg: 82.5 });

    expect(component().title()).toBe('ja@primer.com');
  });

  it('naslov pada na email i kada je ime samo razmak', () => {
    load({ id: 'u1', email: 'ja@primer.com', displayName: '   ', age: 27, bodyweightKg: 82.5 });

    expect(component().title()).toBe('ja@primer.com');
  });

  it('slovo u krugu je prvo slovo naslova, veliko', () => {
    load({ id: 'u1', email: 'ja@primer.com', displayName: 'mateja', age: 27, bodyweightKg: 82.5 });

    expect(component().initial()).toBe('M');
  });

  it('pregled prikazuje popunjene podatke sa jedinicama', () => {
    load({
      id: 'u1',
      email: 'ja@primer.com',
      displayName: 'Mateja',
      sex: Sex.Male,
      age: 27,
      bodyweightKg: 82.5,
      heightCm: 183,
      experienceLevel: 1,
    });

    const rows = component().summary() as { label: string; value: string }[];
    const byLabel = new Map(rows.map((row) => [row.label, row.value]));

    expect(byLabel.get('Uzrast')).toBe('27');
    expect(byLabel.get('Telesna masa')).toBe('82.5 kg');
    expect(byLabel.get('Visina')).toBe('183 cm');
    expect(byLabel.get('Nivo iskustva')).toBe('Srednji nivo');
    expect(byLabel.get('Pol')).toBe('Muški');
  });

  it('prazna polja se ne prikazuju kao prazni redovi', () => {
    // Visina i pol su opcioni na serveru. Red sa praznom vrednošću izgleda kao greška.
    load({ id: 'u1', email: 'ja@primer.com', age: 27, bodyweightKg: 82.5, experienceLevel: 0 });

    const labels = (component().summary() as { label: string }[]).map((row) => row.label);

    expect(labels).not.toContain('Visina');
    expect(labels).not.toContain('Pol');
    expect(labels).toContain('Uzrast');
  });

  it('nalog bez slike ne obara ekran', () => {
    // `forkJoin` pada na prvoj grešci, pa bi 404 na slici oborio i učitavanje profila.
    load({ id: 'u1', email: 'ja@primer.com', age: 27, bodyweightKg: 82.5 });

    expect(component().error()).toBeNull();
    expect(component().loading()).toBe(false);
    expect(component().avatarUrl()).toBeNull();
  });

  it('slika naloga se prikazuje kada postoji', () => {
    load(
      { id: 'u1', email: 'ja@primer.com', age: 27, bodyweightKg: 82.5, hasAvatar: true },
      new Blob([new Uint8Array([0xff, 0xd8, 0xff])], { type: 'image/jpeg' }),
    );

    expect(component().avatarUrl()).toMatch(/^blob:/);
  });
});
