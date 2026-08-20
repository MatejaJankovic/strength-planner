import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the brand on non-auth routes', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand strong')?.textContent).toContain('Strength Planner');
  });

  /**
   * Analitika je uklonjena iz trake na dnu — Statistika na dashboardu profila je sada
   * jedini ulaz. Traka i dalje mora da prikaže tačno onoliko taba koliko app.scss ima
   * kolona (`grid-template-columns: repeat(3, ...)`): manje kolona nego stavki gura
   * poslednji tab u drugi red i traka preraste u dvostruku visinu preko sadržaja — greška
   * koja je već jednom postojala kad je Analitika bila peta stavka u četvorokolonoj traci.
   */
  it('bottom nav shows exactly three tabs, without Analitika', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;

    const labels = Array.from(compiled.querySelectorAll('.bottom-nav__item span')).map(
      (el) => el.textContent?.trim(),
    );

    expect(labels).toEqual(['Trening', 'Plan', 'Profil']);
  });
});
