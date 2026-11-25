import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NoteService } from './note.service';

/**
 * Dve različite adrese i dva različita id-a: beleška uz trening ide na trening, a beleška uz vežbu
 * na vežbu. Zamena jedne za drugu bi upisala belešku uz pogrešan red, a server bi odgovorio 404
 * ili, gore, prihvatio.
 */
describe('NoteService', () => {
  let service: NoteService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(NoteService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('beleška uz trening ide na adresu treninga', () => {
    let result: unknown;
    service.setSessionNote('s-1', 'Težak dan').subscribe((value) => (result = value));

    const request = http.expectOne((candidate) => candidate.url.endsWith('/sessions/s-1/note'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ note: 'Težak dan' });
    request.flush({ note: 'Težak dan' });

    expect(result).toEqual({ note: 'Težak dan' });
  });

  it('beleška uz vežbu ide na adresu vežbe, ne treninga', () => {
    service.setExerciseNote('e-9', 'Sedište 4').subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/exercises/e-9/note'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ note: 'Sedište 4' });
    request.flush({ note: 'Sedište 4' });
  });

  it('prazna beleška se šalje kao null, ne kao izostavljeno polje', () => {
    service.setSessionNote('s-1', null).subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/sessions/s-1/note'));
    expect(request.request.body).toEqual({ note: null });
    request.flush({ note: null });
  });
});
