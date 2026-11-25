import {
  Component,
  ElementRef,
  effect,
  inject,
  input,
  OnDestroy,
  viewChild,
} from '@angular/core';
import {
  CategoryScale,
  Chart,
  Filler,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Tooltip,
} from 'chart.js';
import { ThemeService } from '../../../core/theme/theme.service';

Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Tooltip, Filler);

export interface ChartColors {
  primary: string;
  ink: string;
  /** Tekst na `ink` pozadini (tooltip): u tamnoj temi je `ink` svetao, pa tekst na njemu mora da bude taman. */
  onInk: string;
  muted: string;
  border: string;
}

/**
 * Boje grafikona iz tokena teme. Platno ne zna za CSS, pa se vrednosti čitaju pri crtanju; rezervne su
 * svetle vrednosti iz `styles.scss`, za slučaj da token ne postoji.
 */
export function chartColors(css: { getPropertyValue(name: string): string }): ChartColors {
  const read = (name: string, fallback: string) => css.getPropertyValue(name).trim() || fallback;

  return {
    primary: read('--color-primary', '#0e5c63'),
    ink: read('--color-ink', '#14151a'),
    onInk: read('--color-on-ink', '#ffffff'),
    muted: read('--color-muted', '#66707c'),
    border: read('--color-border', '#dce1e6'),
  };
}

@Component({
  selector: 'app-line-chart',
  template: `<div class="chart"><canvas #canvas></canvas></div>`,
  styleUrl: './line-chart.scss',
})
export class LineChart implements OnDestroy {
  readonly labels = input<string[]>([]);
  readonly values = input<number[]>([]);
  readonly seriesLabel = input('e1RM (kg)');

  private readonly theme = inject(ThemeService);
  private readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('canvas');
  private chart?: Chart;

  constructor() {
    effect(() => {
      // Boje se čitaju iz CSS-a pri crtanju, a platno se ne boji samo od sebe kad se tema promeni: zato se
      // iscrtava ponovo i kad se promeni tema, ne samo kad stignu novi podaci.
      this.theme.effective();
      this.render(this.labels(), this.values());
    });
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private render(labels: string[], values: number[]): void {
    const canvas = this.canvas()?.nativeElement;
    if (!canvas) {
      return;
    }

    this.chart?.destroy();

    const { primary, ink, onInk, muted, border } = chartColors(getComputedStyle(document.documentElement));
    const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const tabular = { family: 'Space Grotesk, Inter, sans-serif' };

    this.chart = new Chart(canvas, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: this.seriesLabel(),
            data: values,
            borderColor: primary,
            backgroundColor: `${primary}1f`,
            borderWidth: 2,
            tension: 0.25,
            fill: true,
            pointRadius: 3,
            pointHoverRadius: 5,
            pointBackgroundColor: primary,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: reduceMotion ? false : { duration: 350 },
        plugins: {
          legend: { display: false },
          tooltip: {
            // Pozadina je „ink" (u tamnoj temi svetla), pa i tekst na njoj mora da prati: podrazumevani beli
            // tekst na svetloj pozadini ne bi bio čitljiv.
            backgroundColor: ink,
            titleColor: onInk,
            bodyColor: onInk,
            titleFont: tabular,
            bodyFont: tabular,
            callbacks: { label: (item) => `${item.formattedValue} kg` },
          },
        },
        scales: {
          x: {
            grid: { display: false },
            ticks: { color: muted, font: { size: 11 }, maxRotation: 0, autoSkipPadding: 12 },
          },
          y: {
            grid: { color: border },
            ticks: { color: muted, font: tabular },
          },
        },
      },
    });
  }
}
