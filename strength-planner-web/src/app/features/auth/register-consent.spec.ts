import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RegisterWizard } from './register-wizard';

/**
 * Nalog ne sme da nastane bez izričitog pristanka, a pristanak mora da bude radnja korisnika:
 * polje nije unapred čekirano.
 */
describe('RegisterWizard — pristanak na obradu podataka', () => {
  let fixture: ComponentFixture<RegisterWizard>;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'onboarding', children: [] }]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(RegisterWizard);
    component().form.patchValue({
      displayName: 'Mateja',
      email: 'ja@primer.com',
      password: 'DovoljnoDugaLozinka1',
      experienceLevel: '1',
    });
    component().step.set(6);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function component(): any {
    return fixture.componentInstance as any;
  }

  function checkbox(): HTMLInputElement {
    return fixture.nativeElement.querySelector('input[type="checkbox"]');
  }

  function submit(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.wiz__next');
  }

  it('polje nije unapred čekirano i dugme za nalog je ugašeno', () => {
    expect(checkbox().checked).toBe(false);
    expect(submit().disabled).toBe(true);
  });

  it('bez pristanka se ne šalje nikakav zahtev', () => {
    component().next();

    http.expectNone((request) => request.url.endsWith('/auth/register'));
  });

  it('čekiranje oživljava dugme i pristanak stiže serveru', () => {
    checkbox().click();
    fixture.detectChanges();

    expect(submit().disabled).toBe(false);
    submit().click();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/auth/register'));
    expect(request.request.body.privacyConsent).toBe(true);
    request.flush({ userId: 'u1', email: 'ja@primer.com', token: 't', expiresAt: '' });
  });

  it('link na politiku otvara novu karticu, pa odgovori ostaju', () => {
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('a.wizard-link');

    expect(link.getAttribute('href')).toBe('/privacy');
    expect(link.getAttribute('target')).toBe('_blank');
    // Link u oznaci polja bi dodirom i čekirao pristanak.
    expect(link.closest('label')).toBeNull();
  });
});
