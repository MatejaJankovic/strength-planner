import { ComponentFixture, TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from '../../../core/theme/theme.service';
import { ThemePicker } from './theme-picker';

describe('ThemePicker', () => {
  let fixture: ComponentFixture<ThemePicker>;
  let theme: ThemeService;

  function radios(): HTMLInputElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('input[type="radio"]'));
  }

  function labelOf(radio: HTMLInputElement): string {
    return radio.closest('label')?.textContent?.trim() ?? '';
  }

  beforeEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
    theme = TestBed.inject(ThemeService);
    fixture = TestBed.createComponent(ThemePicker);
    fixture.detectChanges();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    document.documentElement.removeAttribute('data-theme');
    localStorage.removeItem(THEME_STORAGE_KEY);
  });

  it('nudi tri izbora, redom, i čitač ekrana ih vidi kao jednu grupu', () => {
    expect(radios().map(labelOf)).toEqual(['Kao sistem', 'Svetla', 'Tamna']);
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="radiogroup"]')?.getAttribute('aria-label')).toBe('Tema');
  });

  it('izabran je ono što servis drži, a početni izbor je „kao sistem"', () => {
    expect(radios().map((radio) => radio.checked)).toEqual([true, false, false]);
  });

  it('dodir na izbor menja temu odmah i pamti je', () => {
    const dark = radios()[2];

    dark.click();
    dark.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    TestBed.tick();

    expect(theme.preference()).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(radios().map((radio) => radio.checked)).toEqual([false, false, true]);
  });

  it('izabrano polje je označeno i bojom, ne samo tačkom (nijedan izbor ne sme da liči na ostale)', () => {
    radios()[1].dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const marked = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.theme__option--on'));
    expect(marked.length).toBe(1);
    expect(marked[0].textContent?.trim()).toBe('Svetla');
  });
});
