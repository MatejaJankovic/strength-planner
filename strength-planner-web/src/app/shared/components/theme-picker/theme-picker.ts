import { Component, inject } from '@angular/core';
import { THEME_OPTIONS, ThemePreference, ThemeService } from '../../../core/theme/theme.service';

/** Izbor teme: kao sistem, svetla ili tamna. Menja se odmah, bez dugmeta za čuvanje. */
@Component({
  selector: 'app-theme-picker',
  templateUrl: './theme-picker.html',
  styleUrl: './theme-picker.scss',
})
export class ThemePicker {
  private readonly theme = inject(ThemeService);

  protected readonly options = THEME_OPTIONS;
  protected readonly preference = this.theme.preference;

  protected choose(preference: ThemePreference): void {
    this.theme.set(preference);
  }
}
