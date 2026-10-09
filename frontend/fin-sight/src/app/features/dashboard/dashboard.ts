import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChartData, ChartDataset, ChartOptions } from 'chart.js';
import { DashboardService, DashboardFilters } from '../../core/services/dashboard.service';
import { PeriodFilterService } from '../../core/services/period-filter.service';
import { TransactionService } from '../../core/services/transaction.service';
import {
  DashboardSummary,
  Insight,
  MonthlyTrendPoint,
  TopExpenseItem,
  Transaction,
  WeekdaySpendingItem,
  CategoryBreakdownItem,
} from '../../core/models/models';
import {
  formatCompactCurrency,
  formatCurrency,
  formatDate,
  formatNumber,
  formatPercent,
  formatSignedCurrency,
} from '../../core/utils/format';
import { ChartComponent } from '../../shared/components/chart/chart';
import { EmptyState, Loading } from '../../shared/components/states/states';
import { StatCard } from '../../shared/components/stat-card/stat-card';

const CYAN = '#22d3ee';
const BLUE = '#3b82f6';
const GREEN = '#34d399';
const CORAL = '#fb7185';
const VIOLET = '#a78bfa';
const AMBER = '#fbbf24';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, ChartComponent, EmptyState, Loading, StatCard],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  private readonly dashboardService = inject(DashboardService);
  private readonly transactionService = inject(TransactionService);
  readonly filters = inject(PeriodFilterService);

  readonly loading = signal(true);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly trends = signal<MonthlyTrendPoint[]>([]);
  readonly breakdown = signal<CategoryBreakdownItem[]>([]);
  readonly weekdays = signal<WeekdaySpendingItem[]>([]);
  readonly topExpenses = signal<TopExpenseItem[]>([]);
  readonly insights = signal<Insight[]>([]);
  readonly recent = signal<Transaction[]>([]);

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

  readonly trendChart = computed<ChartData<'line', number[], string>>(() => {
    const data = this.trends();
    return {
      labels: data.map((t) => t.label),
      datasets: [
        {
          label: 'Income',
          data: data.map((t) => t.income),
          borderColor: GREEN,
          backgroundColor: 'rgba(52, 211, 153, 0.16)',
          fill: true,
          tension: 0.4,
          pointRadius: 3,
        },
        {
          label: 'Expenses',
          data: data.map((t) => t.expenses),
          borderColor: CORAL,
          backgroundColor: 'rgba(251, 113, 133, 0.16)',
          fill: true,
          tension: 0.4,
          pointRadius: 3,
        },
      ],
    };
  });

  readonly categoryChart = computed<ChartData<'doughnut', number[], string>>(() => {
    const data = this.breakdown();
    return {
      labels: data.map((d) => d.categoryName),
      datasets: [
        {
          data: data.map((d) => d.amount),
          backgroundColor: data.map((d) => d.color),
          borderColor: 'rgba(6, 10, 20, 0.9)',
          borderWidth: 2,
        },
      ],
    };
  });

  readonly monthlyExpenseChart = computed<ChartData<'bar', number[], string>>(() => {
    const data = this.trends();
    const barDataset: ChartDataset<'bar', number[]> = {
      type: 'bar',
      label: 'Expenses',
      data: data.map((t) => t.expenses),
      backgroundColor: 'rgba(251, 113, 133, 0.55)',
      borderColor: CORAL,
      borderWidth: 1,
      borderRadius: 6,
    };
    const lineDataset = {
      type: 'line',
      label: 'Net balance',
      data: data.map((t) => t.balance),
      borderColor: BLUE,
      backgroundColor: BLUE,
      tension: 0.4,
      pointRadius: 3,
    } as unknown as ChartDataset<'bar', number[]>;

    return {
      labels: data.map((t) => t.label),
      datasets: [barDataset, lineDataset],
    };
  });

  readonly weekdayChart = computed<ChartData<'bar', number[], string>>(() => {
    const data = this.weekdays();
    return {
      labels: data.map((d) => d.label),
      datasets: [
        {
          label: 'Average spending',
          data: data.map((d) => d.averagePerOccurrence),
          backgroundColor: 'rgba(167, 139, 250, 0.55)',
          borderColor: VIOLET,
          borderWidth: 1,
          borderRadius: 6,
        },
      ],
    };
  });

  readonly topExpenseChart = computed<ChartData<'bar', number[], string>>(() => {
    const data = this.topExpenses();
    return {
      labels: data.map((d) => d.description),
      datasets: [
        {
          label: 'Amount',
          data: data.map((d) => d.amount),
          backgroundColor: 'rgba(251, 191, 36, 0.5)',
          borderColor: AMBER,
          borderWidth: 1,
          borderRadius: 6,
        },
      ],
    };
  });

  protected readonly trendOptions: ChartOptions = this.moneyOptions();
  protected readonly barOptions: ChartOptions = {
    ...this.moneyOptions(),
    plugins: {
      ...this.moneyOptions().plugins,
      legend: {
        ...this.moneyOptions().plugins?.legend,
        display: false,
      },
    },
  };
  protected readonly doughnutOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '62%',
    plugins: {
      legend: { position: 'right', labels: { color: '#93a4bd', usePointStyle: true, boxWidth: 8 } },
      tooltip: {
        backgroundColor: 'rgba(10, 17, 32, 0.96)',
        borderColor: 'rgba(148, 163, 184, 0.28)',
        borderWidth: 1,
        padding: 12,
        cornerRadius: 10,
        callbacks: {
          label: (context) => ` ${formatCurrency(context.parsed as number)}`,
        },
      },
    },
  };

  constructor() {
    // Reload whenever the global period or any filter changes.
    effect(() => {
      const model = this.filtersModel();
      this.load(model);
    });
  }

  private load(filters: DashboardFilters): void {
    this.loading.set(true);
    this.dashboardService.summary(filters).subscribe((summary) => this.summary.set(summary));
    this.dashboardService.monthlyTrends(filters).subscribe((trends) => this.trends.set(trends));
    this.dashboardService.categoryBreakdown(filters).subscribe((data) => this.breakdown.set(data));
    this.dashboardService.weekdaySpending(filters).subscribe((data) => this.weekdays.set(data));
    this.dashboardService.topExpenses(filters, 6).subscribe((data) => this.topExpenses.set(data));
    this.dashboardService.insights(filters).subscribe((data) => this.insights.set(data));
    this.transactionService
      .query({
        from: filters.from,
        to: filters.to,
        page: 1,
        pageSize: 6,
        sortBy: 'date',
        descending: true,
      })
      .subscribe((result) => this.recent.set(result.items));
    this.loading.set(false);
  }

  private moneyOptions(): ChartOptions {
    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: {
          labels: { color: '#93a4bd', usePointStyle: true, boxWidth: 8 },
        },
        tooltip: {
          backgroundColor: 'rgba(10, 17, 32, 0.96)',
          borderColor: 'rgba(148, 163, 184, 0.28)',
          borderWidth: 1,
          padding: 12,
          cornerRadius: 10,
          callbacks: {
            label: (context) =>
              ` ${context.dataset.label}: ${formatCurrency(
                typeof context.parsed.y === 'number' ? context.parsed.y : null,
              )}`,
          },
        },
      },
      scales: {
        x: {
          ticks: { color: '#64748b' },
          grid: { color: 'rgba(148, 163, 184, 0.08)' },
        },
        y: {
          ticks: {
            color: '#64748b',
            callback: (value) => formatCompactCurrency(Number(value)),
          },
          grid: { color: 'rgba(148, 163, 184, 0.08)' },
        },
      },
    };
  }

  incomeDeltaClass(): string {
    const delta = this.summary()?.previousPeriod.incomeDelta;
    if (!delta || delta.percentChange == null) return 'flat';
    if (delta.percentChange === 0) return 'flat';
    return delta.higherIsBetter === delta.absoluteChange > 0 ? 'good' : 'bad';
  }

  expenseDeltaClass(): string {
    const delta = this.summary()?.previousPeriod.expenseDelta;
    if (!delta || delta.percentChange == null) return 'flat';
    if (delta.percentChange === 0) return 'flat';
    return delta.higherIsBetter === delta.absoluteChange > 0 ? 'good' : 'bad';
  }

  protected readonly severityLabels: Record<string, string> = {
    Info: 'info',
    Positive: 'positive',
    Warning: 'warning',
    Danger: 'danger',
  };

  protected readonly formatCurrency = formatCurrency;
  protected readonly formatNumber = formatNumber;
  protected readonly formatPercent = formatPercent;
  protected readonly formatSignedCurrency = formatSignedCurrency;
  protected readonly formatDate = formatDate;
  protected readonly cyan = CYAN;
  protected readonly coral = CORAL;
  protected readonly green = GREEN;
  protected readonly blue = BLUE;
  protected readonly violet = VIOLET;
  protected readonly amber = AMBER;
}

