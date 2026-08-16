import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register-page').then((m) => m.RegisterPage),
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
