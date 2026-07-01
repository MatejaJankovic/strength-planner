import {
  Component,
  ElementRef,
  effect,
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

Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Tooltip, Filler);

@Component({
  selector: 'app-line-chart',
  template: `<div class="chart"><canvas #canvas></canvas></div>`,
  styleUrl: './line-chart.scss',
})
export class LineChart implements OnDestroy {
  readonly labels = input<string[]>([]);
  readonly values = input<number[]>([]);
  readonly seriesLabel = input('e1RM (kg)');

  private readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('canvas');
  private chart?: Chart;

  constructor() {
    effect(() => this.render(this.labels(), this.values()));
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

    const css = getComputedStyle(document.documentElement);
    const primary = css.getPropertyValue('--color-primary').trim() || '#0e5c63';
    const ink = css.getPropertyValue('--color-ink').trim() || '#14151a';
    const muted = css.getPropertyValue('--color-muted').trim() || '#66707c';
    const border = css.getPropertyValue('--color-border').trim() || '#dce1e6';
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
            backgroundColor: ink,
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
