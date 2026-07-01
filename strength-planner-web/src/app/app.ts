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

  // Auth screens are standalone: no topbar, no bottom nav to protected routes.
  protected readonly isAuthRoute = computed(() => {
    const url = this.currentUrl();
    return url.startsWith('/login') || url.startsWith('/register');
  });

  protected readonly navItems = [
    { label: 'Trening', icon: 'fitness_center', route: '/workout' },
    { label: 'Analitika', icon: 'monitoring', route: '/analytics' },
    { label: 'Profil', icon: 'person', route: '/profile' },
  ];
}
