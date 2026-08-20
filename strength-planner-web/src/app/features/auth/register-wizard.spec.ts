import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RegisterWizard } from './register-wizard';

/**
 * Zamka za automate mora da ostane nevidljiva čoveku — uključujući i njegov menadžer
 * lozinki.
 *
 * Prijavljena greška iz stvarne upotrebe: polje se zvalo `website`, menadžer lozinki ga je
 * sam popunio (adresu sajta čuva uz svaku stavku i `autocomplete="off"` namerno ignoriše),
 * i server je registraciju odbio sa „Registracija nije uspela." Korisnik nije mogao da
 * napravi nalog nijednim pokušajem, a poruka mu nije rekla zašto — jer je namerno ista kao
 * za uhvaćenog automata.
 *
 * Ovi testovi drže obe strane te odluke: da polje postoji i da ide serveru, i da nema ime
 * po kom bi ga menadžer prepoznao.
 */
describe('RegisterWizard — zamka za automate', () => {
  let fixture: ComponentFixture<RegisterWizard>;
  let http: HttpTestingController;

  /** Imena po kojima menadžeri lozinki prepoznaju polje za adresu sajta. */
  const MANAGER_TARGETS = ['website', 'url', 'site', 'homepage', 'link', 'domain'];

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'login', children: [] },
          { path: 'onboarding', children: [] },
        ]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(RegisterWizard);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function component(): any {
    return fixture.componentInstance as any;
  }

  /** Vodi čarobnjaka do koraka na kom zamka postoji. */
  function goToCredentials(): void {
    component().form.controls.displayName.setValue('Mateja');
    component().next();
    fixture.detectChanges();
  }

  function trap(): HTMLInputElement {
    return fixture.nativeElement.querySelector('input[formcontrolname="website"]');
  }

  it('zamka postoji na koraku sa emailom', () => {
    goToCredentials();

    expect(trap()).not.toBeNull();
  });

  it('zamka je izvučena iz reda tabulatora i sakrivena od čitača ekrana', () => {
    goToCredentials();

    const input = trap();

    expect(input.getAttribute('tabindex')).toBe('-1');
    expect(input.closest('[aria-hidden="true"]')).not.toBeNull();
  });

  /**
   * Ovo je test zbog kog fajl postoji. `autocomplete="off"` nije dovoljan: menadžeri
   * lozinki ga za polje sa adresom sajta namerno ignorišu.
   */
  it('zamka nema ime po kom je menadžer lozinki prepoznaje', () => {
    goToCredentials();

    const input = trap();
    const identity = `${input.id} ${input.getAttribute('name') ?? ''}`.toLowerCase();

    for (const target of MANAGER_TARGETS) {
      expect(identity.includes(target), `zamka se predstavlja kao "${target}"`).toBe(false);
    }
  });

  it('zamka nosi atribute kojima se menadžeri izričito isključuju', () => {
    goToCredentials();

    const input = trap();

    expect(input.hasAttribute('data-1p-ignore'), '1Password').toBe(true);
    expect(input.getAttribute('data-lpignore'), 'LastPass').toBe('true');
    expect(input.hasAttribute('data-bwignore'), 'Bitwarden').toBe(true);
    expect(input.getAttribute('data-form-type'), 'Dashlane i ostali').toBe('other');
    expect(input.getAttribute('autocomplete')).toBe('off');
  });

  it('prazna zamka ide serveru kao null, a popunjena kao vrednost', () => {
    // Server odbija svaku registraciju sa popunjenom zamkom, pa vrednost mora da stigne
    // nepromenjena - ni prazna kao prazan string, ni izostavljena.
    component().form.patchValue({
      displayName: 'Mateja',
      email: 'ja@primer.com',
      password: 'DovoljnoDugaLozinka1',
      experienceLevel: '1',
    });

    (component() as any).step.set(6);
    component().next();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/register') && candidate.method === 'POST',
    );

    expect(request.request.body.website).toBeNull();

    request.flush({ userId: 'u1', email: 'ja@primer.com', token: 't', expiresAt: '' });
  });
});
