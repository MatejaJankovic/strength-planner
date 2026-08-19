import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthTokenStorage } from './auth-token-storage';

/**
 * Propušta samo neprijavljenog posetioca. Suprotno od <c>authGuard</c>.
 *
 * Stoji na prijavi i registraciji. Bez njega je ulogovan korisnik koji otvori
 * `/register` dobijao čarobnjaka od prvog pitanja i, ako bi ga prošao do kraja,
 * napravio drugi nalog — a token prvog bi bio prosto zamenjen, bez ijedne poruke.
 */
export const guestGuard: CanActivateFn = () => {
  const token = inject(AuthTokenStorage).token();

  if (!token) {
    return true;
  }

  return inject(Router).createUrlTree(['/workout']);
};
