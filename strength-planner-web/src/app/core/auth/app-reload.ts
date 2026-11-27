import { DOCUMENT, inject, InjectionToken } from '@angular/core';

/**
 * Loads the app again from its start page, dropping everything held in memory. A token so that tests
 * can see that it was asked for without leaving the page.
 */
export const RELOAD_APP = new InjectionToken<() => void>('strength-planner.reload-app', {
  providedIn: 'root',
  factory: () => {
    const document = inject(DOCUMENT);
    return () => document.defaultView?.location.assign('/');
  },
});
