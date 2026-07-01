import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthTokenStorage } from './auth-token-storage';

const AUTH_ENDPOINTS = ['/auth/login', '/auth/register'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthTokenStorage).token();
  const authService = inject(AuthService);

  const authorized = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  const isAuthCall = AUTH_ENDPOINTS.some((path) => request.url.includes(path));

  return next(authorized).pipe(
    catchError((error: unknown) => {
      // Session expired or token rejected -> sign out. Skip the login/register
      // calls so their 401 surfaces as an inline "wrong credentials" message.
      if (error instanceof HttpErrorResponse && error.status === 401 && !isAuthCall) {
        authService.logout();
      }
      return throwError(() => error);
    }),
  );
};
