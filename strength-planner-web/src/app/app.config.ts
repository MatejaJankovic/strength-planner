import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { authInterceptor } from './core/auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAppInitializer(() => {
      // Hydrate the current user from a persisted token. Fire-and-forget so a
      // slow or failing /me call never blocks the app from rendering; an invalid
      // token is cleared by the interceptor's 401 handling.
      const auth = inject(AuthService);
      if (auth.token()) {
        auth.loadMe().subscribe({ error: () => undefined });
      }
    }),
  ],
};
