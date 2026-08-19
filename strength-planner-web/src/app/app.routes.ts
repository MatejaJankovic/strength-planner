import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { guestGuard } from './core/auth/guest.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register-wizard').then((m) => m.RegisterWizard),
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'workout',
  },
  {
    path: 'workout',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/workout/workout-dashboard').then((m) => m.WorkoutDashboard),
  },
  {
    path: 'plan',
    canActivate: [authGuard],
    loadComponent: () => import('./features/macrocycle/plan-home').then((m) => m.PlanHome),
  },
  {
    path: 'analytics',
    canActivate: [authGuard],
    loadComponent: () => import('./features/analytics/analytics-home').then((m) => m.AnalyticsHome),
  },
  // Konkretnija putanja ide prva. Sa obrnutim redom radi samo zato što je 'profile'
  // terminalna ruta: poklapanje po prefiksu uspe na segmentu 'profile', ne uspe da potroši
  // 'edit', i router pređe na naredni zapis. Onog dana kada 'profile' dobije `children`,
  // poklapanje uspeva na roditelju i pada među decom - pa '/profile/edit' počne da završava
  // na catch-all ruti, a ništa u diff-u ne pokazuje zašto.
  {
    path: 'profile/edit',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile-edit').then((m) => m.ProfileEdit),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile-home').then((m) => m.ProfileHome),
  },
  {
    path: 'onboarding',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/onboarding/one-rep-max-setup').then((m) => m.OneRepMaxSetup),
  },
  {
    path: 'templates',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/templates/custom-templates').then((m) => m.CustomTemplates),
  },
  {
    path: 'session/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/workout/workout-session').then((m) => m.WorkoutSession),
  },
  {
    path: '**',
    redirectTo: 'workout',
  },
];
