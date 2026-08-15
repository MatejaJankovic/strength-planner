import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import { CustomTemplateDto, SaveCustomTemplateRequest } from '../models/training.models';

/**
 * Lični šabloni treninga.
 *
 * Namerno bez keša: spisak se čita na jednom ekranu i menja se samo sa njega, pa keš ne bi
 * štedeo poziv nego bi dodao još jedno mesto koje mora da se prazni pri odjavi.
 */
@Injectable({ providedIn: 'root' })
export class CustomTemplateService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  list(): Observable<CustomTemplateDto[]> {
    return this.http.get<CustomTemplateDto[]>(`${this.apiUrl}/templates/custom`);
  }

  create(request: SaveCustomTemplateRequest): Observable<CustomTemplateDto> {
    return this.http.post<CustomTemplateDto>(`${this.apiUrl}/templates/custom`, request);
  }

  update(id: string, request: SaveCustomTemplateRequest): Observable<CustomTemplateDto> {
    return this.http.put<CustomTemplateDto>(`${this.apiUrl}/templates/custom/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/templates/custom/${id}`);
  }
}
