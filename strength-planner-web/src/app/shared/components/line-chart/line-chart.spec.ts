import { TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from '../../../core/theme/theme.service';
import { LineChart, chartColors, tooltipColors } from './line-chart';

describe('chartColors', () => {
  const read = (values: Record<string, string>) => ({ getPropertyValue: (name: string) => values[name] ?? '' });

  it('čita boje iz tokena teme, sa razmacima skinutim', () => {
    expect(
      chartColors(
        read({
          '--color-primary': ' #4fb8c0 ',
          '--color-ink': '#e8ebef',
          '--color-on-ink': '#14151a',
          '--color-muted': '#9aa4af',
          '--color-border': '#2a3137',
        }),
      ),
    ).toEqual({ primary: '#4fb8c0', ink: '#e8ebef', onInk: '#14151a', muted: '#9aa4af', border: '#2a3137' });
  });

  it('bez tokena važe svetle vrednosti, a tekst tooltip-a je beo', () => {
    expect(chartColors(read({}))).toEqual({
      primary: '#0e5c63',
      ink: '#14151a',
      onInk: '#ffffff',
      muted: '#66707c',
      border: '#dce1e6',
    });
  });

  it('tooltip: pozadina je „ink", a tekst naslova i sadržaja „on-ink" (ne beo u obe teme)', () => {
    const dark = chartColors(read({ '--color-ink': '#e8ebef', '--color-on-ink': '#14151a' }));

    expect(tooltipColors(dark)).toEqual({ backgroundColor: '#e8ebef', titleColor: '#14151a', bodyColor: '#14151a' });
  });

  it('tekst tooltip-a se ne poklapa sa njegovom pozadinom u obe teme', () => {
    const light = chartColors(read({ '--color-ink': '#14151a', '--color-on-ink': '#ffffff' }));
    const dark = chartColors(read({ '--color-ink': '#e8ebef', '--color-on-ink': '#14151a' }));

    expect(light.onInk).not.toBe(light.ink);
    expect(dark.onInk).not.toBe(dark.ink);
  });
});

/**
 * Platno se ne boji samo od sebe: boje se čitaju iz CSS-a pri crtanju, pa se grafikon mora nacrtati ponovo
 * kad se tema promeni. Chart.js ovde ne treba (jsdom nema 2D platno), pa se broji koliko puta se zove
 * crtanje.
 */
describe('LineChart', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
  });

  it('crta se ponovo kad se promeni tema, i samo tada (ne na svaku promenu prikaza)', () => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    const theme = TestBed.inject(ThemeService);
    const fixture = TestBed.createComponent(LineChart);
    const render = vi
      .spyOn(fixture.componentInstance as unknown as { render(): void }, 'render')
      .mockImplementation(() => undefined);
    fixture.componentRef.setInput('labels', ['a', 'b']);
    fixture.componentRef.setInput('values', [1, 2]);

    fixture.detectChanges();
    TestBed.tick();
    const drawn = render.mock.calls.length;
    expect(drawn).toBeGreaterThan(0);

    fixture.detectChanges();
    TestBed.tick();
    expect(render.mock.calls.length).toBe(drawn);

    theme.set('dark');
    TestBed.tick();
    expect(render.mock.calls.length).toBe(drawn + 1);

    theme.set('light');
    TestBed.tick();
    expect(render.mock.calls.length).toBe(drawn + 2);
  });
});
