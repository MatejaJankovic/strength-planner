import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthTokenStorage } from './auth-token-storage';

export const authGuard: CanActivateFn = (_route, state) => {
  const token = inject(AuthTokenStorage).token();

  if (token) {
    return true;
  }

  return inject(Router).createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};
