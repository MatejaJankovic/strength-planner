import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthTokenStorage } from '../../core/auth/auth-token-storage';
import { PrivacyPage } from './privacy-page';

describe('PrivacyPage', () => {
  let fixture: ComponentFixture<PrivacyPage>;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PrivacyPage);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function answer(controllerName: string | null, contactEmail: string | null): void {
    http
      .expectOne((request) => request.url.endsWith('/privacy'))
      .flush({ version: '2026-03-05', controllerName, contactEmail });
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');
  }

  it('prikazuje rukovaoca, adresu i datum verzije sa servera', () => {
    answer('Primer Osoba', 'privatnost@primer.com');

    expect(text()).toContain('Primer Osoba');
    expect(text()).toContain('verzija od 5. 3. 2026.');
    expect(
      fixture.nativeElement.querySelector('a[href="mailto:privatnost@primer.com"]'),
    ).not.toBeNull();
  });

  it('kaže da kontakt nije podešen umesto da prikaže prazno polje', () => {
    answer(null, null);

    expect(text()).toContain('nisu podešeni na ovom serveru');
    expect(fixture.nativeElement.querySelector('a[href^="mailto:"]')).toBeNull();
  });

  it('tekst ostaje čitljiv i kada server ne odgovori', () => {
    http
      .expectOne((request) => request.url.endsWith('/privacy'))
      .flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();

    expect(text()).toContain('trenutno nisu dostupni');
    expect(text()).toContain('Aplikacija ne zamenjuje trenera ni lekara');
    expect(text()).toContain('navršenih 16 godina');
  });

  it('strelica nazad vodi na podešavanja prijavljenog, a ostale na prijavu', () => {
    answer(null, null);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const back: HTMLButtonElement = fixture.nativeElement.querySelector('.sub__back');

    back.click();
    expect(navigate).toHaveBeenLastCalledWith('/login');

    vi.spyOn(TestBed.inject(AuthTokenStorage), 'token').mockReturnValue('token');
    back.click();
    expect(navigate).toHaveBeenLastCalledWith('/settings');
  });
});
