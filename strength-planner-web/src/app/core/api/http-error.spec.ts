import { HttpErrorResponse } from '@angular/common/http';
import { extractErrorMessage } from './http-error';

function problem(title: string, status: number): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: { title, status, detail: title } });
}

describe('extractErrorMessage', () => {
  /**
   * Server u produkciji šalje samo opšti engleski naslov (Detail je isti kao Title). Do sada je ekran
   * pokazivao „Request conflicts with current state." posle preskakanja i zamene vežbe.
   */
  it.each([
    ['Request conflicts with current state.', 409, 'Radnja nije moguća u trenutnom stanju. Osveži ekran i pokušaj ponovo.'],
    ['Resource was not found.', 404, 'Traženo ne postoji ili više nije dostupno.'],
    ['Validation failed.', 400, 'Uneti podaci nisu ispravni.'],
    ['Invalid request.', 400, 'Zahtev nije ispravan.'],
    ['Mesocycle generation failed.', 400, 'Blok nije mogao da se napravi.'],
    ['Unauthorized.', 401, 'Nisi prijavljen ili je prijava istekla.'],
    ['Unexpected server error.', 500, 'Greška na serveru. Pokušaj ponovo.'],
  ])('prevodi opšti naslov "%s" na srpski', (title, status, expected) => {
    expect(extractErrorMessage(problem(title, status), 'rezerva')).toBe(expected);
  });

  it('ostavlja naslov koji nije opšti kakav jeste', () => {
    expect(extractErrorMessage(problem('Nešto posebno.', 400), 'rezerva')).toBe('Nešto posebno.');
  });

  it('poruka i spisak grešaka imaju prednost nad naslovom', () => {
    expect(
      extractErrorMessage(
        new HttpErrorResponse({ status: 400, error: { title: 'Validation failed.', message: 'Poruka.' } }),
        'rezerva',
      ),
    ).toBe('Poruka.');
    expect(
      extractErrorMessage(
        new HttpErrorResponse({ status: 400, error: { title: 'Model validation failed.', errors: { a: ['Greška polja.'] } } }),
        'rezerva',
      ),
    ).toBe('Greška polja.');
  });

  it('bez odgovora (mreža) vraća rezervu', () => {
    expect(extractErrorMessage(new HttpErrorResponse({ status: 0 }), 'rezerva')).toBe('rezerva');
  });
});
