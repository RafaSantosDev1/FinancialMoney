import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  input,
} from '@angular/core';
import {
  Chart,
  ChartConfiguration,
  ChartData,
  ChartOptions,
  ChartType,
  registerables,
} from 'chart.js';

Chart.register(...registerables);

let chartCounter = 0;

@Component({
  selector: 'app-chart',
  template: `<div class="chart-wrap" [class.tall]="tall()">
    <canvas #canvas [attr.aria-label]="ariaLabel()" role="img"></canvas>
  </div>`,
})
export class ChartComponent implements AfterViewInit, OnDestroy {
  readonly type = input.required<ChartType>();
  // Data is intentionally loosely typed: chart.js unions per chart type are impractical
  // to thread through component inputs.
  readonly data = input.required<unknown>();
  readonly options = input<unknown>({});
  readonly tall = input(false);
  readonly ariaLabel = input<string>('Chart');

  @ViewChild('canvas', { static: true })
  private readonly canvasRef!: ElementRef<HTMLCanvasElement>;

  private chart: Chart | null = null;

  ngAfterViewInit(): void {
    this.render();
  }

  ngOnChanges(): void {
    if (this.chart) {
      this.chart.data = this.data() as ChartData<'bar', number[], string>;
      this.chart.options = (this.options() ?? {}) as ChartOptions;
      this.chart.update();
    }
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
    this.chart = null;
  }

  private render(): void {
    const canvas = this.canvasRef.nativeElement;
    if (!canvas.id) canvas.id = `chart-${++chartCounter}`;

    this.chart?.destroy();
    this.chart = new Chart(canvas, {
      type: this.type(),
      data: this.data() as ChartData<'bar', number[], string>,
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: {
            labels: {
              color: '#93a4bd',
              font: { family: 'Inter, sans-serif', size: 12 },
              usePointStyle: true,
              boxWidth: 8,
            },
          },
          tooltip: {
            backgroundColor: 'rgba(10, 17, 32, 0.96)',
            borderColor: 'rgba(148, 163, 184, 0.28)',
            borderWidth: 1,
            titleColor: '#e8eefb',
            bodyColor: '#cbd5e1',
            padding: 12,
            cornerRadius: 10,
          },
        },
        ...(this.options() as ChartConfiguration['options'] | undefined),
      },
    });
  }
}



