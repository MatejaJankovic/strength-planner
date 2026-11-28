import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RegisterWizard } from './register-wizard';

/**
 * Šta registracija upisuje kao odgovor korisnika: premlad uzrast ne sme tiho da postane dozvoljen,
 * a visina koju korisnik nije hteo da navede ne sme da se upiše.
 */
describe('RegisterWizard — uzrast i visina', () => {
  const HEIGHT_STEP = 4;
  const AGE_STEP = 5;
  const LAST_STEP = 6;

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
      privacyConsent: true,
    });
  });

  afterEach(() => http.verify());

  function component(): any {
    return fixture.componentInstance as any;
  }

  function open(step: number): void {
    component().step.set(step);
    fixture.detectChanges();
  }

  function element<T extends HTMLElement>(selector: string): T {
    return fixture.nativeElement.querySelector(selector);
  }

  function press(selector: string): void {
    element(selector).click();
    fixture.detectChanges();
  }

  function typeAge(value: string): void {
    const field = element<HTMLInputElement>('#wz-age');
    field.value = value;
    field.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function sentAfterLastStep(): any {
    open(LAST_STEP);
    press('.wiz__next');

    const request = http.expectOne((candidate) => candidate.url.endsWith('/auth/register'));
    request.flush({ userId: 'u1', email: 'ja@primer.com', token: 't', expiresAt: '' });
    return request.request.body;
  }

  it('upisanih 15 godina ostaje 15, uz poruku i bez prolaza', () => {
    open(AGE_STEP);
    typeAge('15');

    expect(component().form.controls.age.value).toBe(15);
    expect(element('.field__error').textContent).toContain('bar 16 godina');
    expect(element<HTMLButtonElement>('.wiz__next').disabled).toBe(true);

    component().next();
    expect(component().step()).toBe(AGE_STEP);
  });

  it('sa 16 godina korak pušta dalje', () => {
    open(AGE_STEP);
    typeAge('16');

    expect(element('.field__error')).toBeNull();
    press('.wiz__next');
    expect(component().step()).toBe(LAST_STEP);
  });

  it('preskočena visina se ne šalje', () => {
    open(HEIGHT_STEP);
    press('.wizard-skip');

    expect(component().step()).toBe(AGE_STEP);
    expect(sentAfterLastStep().heightCm).toBeNull();
  });

  it('potvrđena visina se šalje, i posle ranijeg preskakanja', () => {
    open(HEIGHT_STEP);
    press('.wizard-skip');
    open(HEIGHT_STEP);
    press('.wiz__next');

    expect(sentAfterLastStep().heightCm).toBe(175);
  });
});
