import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TrendDto, TrendWeekDto } from '../../core/models/analytics.models';
import { Trend } from './trend';

function week(start: string, overrides: Partial<TrendWeekDto> = {}): TrendWeekDto {
  return {
    weekStart: start,
    tonnageKg: 0,
    sets: 0,
    stimulativeSets: 0,
    stimulativeSetsByMuscle: {},
    blockName: null,
    planName: null,
    isDeload: false,
    blockStarts: false,
    ...overrides,
  };
}

const TREND: TrendDto = {
  muscles: ['Back', 'Chest'],
  weeks: [
    week('2026-11-09T00:00:00Z', {
      tonnageKg: 4000,
      sets: 12,
      stimulativeSets: 10,
      stimulativeSetsByMuscle: { Chest: 6, Back: 4 },
      blockName: 'Blok 1',
      planName: 'Plan jesen',
      blockStarts: true,
    }),
    week('2026-11-16T00:00:00Z'),
    week('2026-11-23T00:00:00Z', {
      tonnageKg: 2000,
      sets: 6,
      stimulativeSets: 5,
      stimulativeSetsByMuscle: { Chest: 3, Back: 2 },
      blockName: 'Blok 2',
      planName: 'Plan jesen',
      isDeload: true,
      blockStarts: true,
    }),
  ],
};

/**
 * Pregled kroz sve blokove: opseg i mera se biraju na ekranu, a server računa po kalendarskim
 * nedeljama. Čista logika redova je u `trend-rows.spec.ts`; ovde je ono što ekran radi sa izborom.
 */
describe('Trend', () => {
  let fixture: ComponentFixture<Trend>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function open(): void {
    fixture = TestBed.createComponent(Trend);
    fixture.detectChanges();
    TestBed.tick();
  }

  function request(weeks: number) {
    return http.expectOne(
      (candidate) =>
        candidate.url.endsWith('/analytics/trend') && candidate.params.get('weeks') === String(weeks),
    );
  }

  function text(): string {
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('podrazumevano traži šest meseci i prikazuje nedelje sa blokovima i deload-om', () => {
    open();
    request(26).flush(TREND);

    const rendered = text();

    expect(rendered).toContain('Blok 1 · Plan jesen');
    expect(rendered).toContain('Blok 2 · Plan jesen');
    expect(rendered).toContain('Deload');
    expect(rendered).toContain('bez treninga');
    expect(rendered).toContain('4,000 kg');
    expect(rendered).toContain('Ukupno u opsegu');
    expect(rendered).toContain('6,000 kg');
  });

  it('izbor opsega traži taj opseg; "Sve" traži 0 nedelja', () => {
    open();
    request(26).flush(TREND);
    text();

    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.range__btn'),
    );
    buttons.find((button) => button.textContent?.trim() === 'Sve')?.click();
    TestBed.tick();

    request(0).flush(TREND);
    expect(text()).toContain('Blok 1');
  });

  it('mera je spisak sa mišićima koje je server vratio, a izbor mišića menja brojeve', () => {
    open();
    request(26).flush(TREND);
    fixture.detectChanges();

    const select = (fixture.nativeElement as HTMLElement).querySelector<HTMLSelectElement>('#trend-metric')!;
    const options = Array.from(select.options).map((option) => option.textContent?.trim());
    expect(options).toEqual(['Tonaža', 'Serije (ukupno)', 'Serije: Back', 'Serije: Chest']);

    select.value = 'muscle:Chest';
    select.dispatchEvent(new Event('change'));
    const rendered = text();

    expect(rendered).toContain('6 serija');
    expect(rendered).toContain('3 serija');
    expect(rendered).not.toContain('4,000 kg');
  });

  it('opseg bez treninga kaže da ih nema', () => {
    open();
    request(26).flush({ muscles: [], weeks: [week('2026-11-23T00:00:00Z')] } satisfies TrendDto);

    expect(text()).toContain('Nema treninga u izabranom opsegu');
  });

  it('greška se prikazuje, a izbor opsega ponovo traži', () => {
    open();
    request(26).flush(null, { status: 500, statusText: 'Server Error' });

    expect(text()).toContain('Ne mogu da učitam pregled kroz blokove');

    const button = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.range__btn'),
    ).find((candidate) => candidate.textContent?.trim() === '8 nedelja');
    button?.click();
    TestBed.tick();

    request(8).flush(TREND);
    expect(text()).toContain('Blok 1');
  });
});
