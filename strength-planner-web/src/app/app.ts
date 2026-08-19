import { Component, computed, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { filter, map, startWith } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [MatIconModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  /**
   * Screens that carry their own chrome and must not get the app frame around it.
   *
   * Login and registration are standalone: a bottom nav to protected routes would be
   * pointing at screens the visitor cannot open yet. The 1RM screen belongs here too, but
   * only while it is the last step of registration (`?wizard=1`) — it then supplies its
   * own progress bar and continue button, and the app topbar plus a bottom nav to Trening
   * and Analitika would frame a step of a flow the user has not finished. Opened from the
   * profile the same screen is an ordinary destination and keeps the frame.
   */
  protected readonly isBareRoute = computed(() => {
    const url = this.currentUrl();

    return (
      url.startsWith('/login') ||
      url.startsWith('/register') ||
      (url.startsWith('/onboarding') && url.includes('wizard=1'))
    );
  });

  protected readonly navItems = [
    { label: 'Trening', icon: 'fitness_center', route: '/workout' },
    { label: 'Plan', icon: 'calendar_month', route: '/plan' },
    { label: 'Analitika', icon: 'monitoring', route: '/analytics' },
    { label: 'Profil', icon: 'person', route: '/profile' },
  ];
}
