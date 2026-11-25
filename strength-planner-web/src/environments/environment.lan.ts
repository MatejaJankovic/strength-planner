// Okruženje za pristup sa telefona u lokalnoj mreži (`npm run start:lan`).
//
// API se traži na istoj adresi kao i aplikacija, a razvojni server prosleđuje /api na računar
// (proxy.conf.json). Telefon tako traži samo jednu adresu i jedan, bezbedan (HTTPS) izvor: ne
// treba mu adresa API-ja, a pregledač ne blokira mešovit sadržaj (HTTPS stranica koja traži
// http:// API) i ne traži CORS.
export const environment = {
  production: false,
  apiUrl: '/api',
};
