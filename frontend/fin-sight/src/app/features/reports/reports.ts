import { Component, computed, effect, inject, signal } from '@angular/core';
import { DashboardService, DashboardFilters } from '../../core/services/dashboard.service';
import { PeriodFilterService } from '../../core/services/period-filter.service';
import {
  CategoryBreakdownItem,
  DashboardSummary,
  Insight,
  MonthlyTrendPoint,
  TopExpenseItem,
} from '../../core/models/models';
import { formatCurrency, formatDate, formatNumber, formatPercent } from '../../core/utils/format';
import { EmptyState, Loading } from '../../shared/components/states/states';
import { ChartComponent } from '../../shared/components/chart/chart';
import { ChartData, ChartOptions } from 'chart.js';

@Component({
  selector: 'app-reports',
  imports: [EmptyState, Loading, ChartComponent],
  templateUrl: './reports.html',
  styleUrl: './reports.css',
})
export class Reports {
  private readonly dashboardService = inject(DashboardService);
  readonly filters = inject(PeriodFilterService);

  readonly loading = signal(true);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly trends = signal<MonthlyTrendPoint[]>([]);
  readonly breakdown = signal<CategoryBreakdownItem[]>([]);
  readonly topExpenses = signal<TopExpenseItem[]>([]);
  readonly insights = signal<Insight[]>([]);

  private readonly filtersModel = computed<DashboardFilters>(() => {
    const range = this.filters.range();
    return {
      from: range.from,
      to: range.to,
      categoryIds: this.filters.categoryIds(),
      type: this.filters.type(),
      accountId: this.filters.accountId(),
      search: this.filters.search(),
    };
  });

  readonly hasData = computed(() => (this.summary()?.transactionCount ?? 0) > 0);

  readonly balanceChart = computed<ChartData<'bar', number[], string>>(() => {
    const data = this.trends();
    return {
      labels: data.map((t) => t.label),
      datasets: [
        {
          label: 'Net balance',
          data: data.map((t) => t.balance),
          backgroundColor: data.map((t) => (t.balance >= 0 ? 'rgba(52, 211, 153, 0.5)' : 'rgba(251, 113, 133, 0.5)')),
          borderColor: data.map((t) => (t.balance >= 0 ? '#34d399' : '#fb7185')),
          borderWidth: 1,
          borderRadius: 6,
        },
      ],
    };
  });

  constructor() {
    // Reload whenever the global period or any filter changes.
    effect(() => {
      const filters = this.filtersModel();
      this.load(filters);
    });
  }

  private load(filters: DashboardFilters): void {
    this.loading.set(true);
    this.dashboardService.summary(filters).subscribe((s) => this.summary.set(s));
    this.dashboardService.monthlyTrends(filters).subscribe((t) => this.trends.set(t));
    this.dashboardService.categoryBreakdown(filters).subscribe((b) => this.breakdown.set(b));
    this.dashboardService.topExpenses(filters, 10).subscribe((e) => this.topExpenses.set(e));
    this.dashboardService.insights(filters).subscribe((i) => this.insights.set(i));
    this.loading.set(false);
  }

  deltaClass(higherIsBetter: boolean, change: number, percent: number | null): string {
    if (percent == null || change === 0) return 'flat';
    return higherIsBetter === change > 0 ? 'good' : 'bad';
  }

  protected readonly severityLabels: Record<string, string> = {
    Info: 'info',
    Positive: 'positive',
    Warning: 'warning',
    Danger: 'danger',
  };

  protected readonly formatCurrency = formatCurrency;
  protected readonly formatDate = formatDate;
  protected readonly formatNumber = formatNumber;
  protected readonly formatPercent = formatPercent;

  protected readonly barOptions: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: 'rgba(10, 17, 32, 0.96)',
        borderColor: 'rgba(148, 163, 184, 0.28)',
        borderWidth: 1,
        padding: 12,
        cornerRadius: 10,
        callbacks: {
          label: (context) => ` ${formatCurrency(Number(context.parsed.y))}`,
        },
      },
    },
    scales: {
      x: { ticks: { color: '#64748b' }, grid: { color: 'rgba(148, 163, 184, 0.08)' } },
      y: {
        ticks: { color: '#64748b', callback: (value) => formatCurrency(Number(value)) },
        grid: { color: 'rgba(148, 163, 184, 0.08)' },
      },
    },
  };
}
