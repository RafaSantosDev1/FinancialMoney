import { Injectable, computed, signal } from '@angular/core';
import { TransactionTypeValue } from '../models/models';

export type PeriodKey =
  | 'thisMonth'
  | 'lastMonth'
  | 'last3'
  | 'last6'
  | 'thisYear'
  | 'custom';

export interface PeriodPreset {
  key: PeriodKey;
  label: string;
}

export const PERIOD_PRESETS: PeriodPreset[] = [
  { key: 'thisMonth', label: 'This month' },
  { key: 'lastMonth', label: 'Last month' },
  { key: 'last3', label: 'Last 3 months' },
  { key: 'last6', label: 'Last 6 months' },
  { key: 'thisYear', label: 'This year' },
  { key: 'custom', label: 'Custom range' },
];

function toIso(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

/**
 * Global, reactive filter state shared by the header selector and every page.
 */
@Injectable({ providedIn: 'root' })
export class PeriodFilterService {
  readonly period = signal<PeriodKey>('thisMonth');
  readonly customFrom = signal<string>(toIso(startOfMonth(new Date())));
  readonly customTo = signal<string>(toIso(new Date()));
  readonly categoryIds = signal<number[]>([]);
  readonly type = signal<number | null>(null);
  readonly accountId = signal<number | null>(null);
  readonly search = signal('');

  readonly range = computed<{ from: string; to: string }>(() => {
    const lastDayOfMonth = (date: Date) =>
      new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
    const monthEnd = (date: Date) =>
      `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, '0')}-${`${lastDayOfMonth(date)}`.padStart(2, '0')}`;
    const monthStart = (date: Date) =>
      toIso(startOfMonth(date));

    switch (this.period()) {
      case 'thisMonth': {
        const today = new Date();
        return { from: monthStart(today), to: monthEnd(today) };
      }
      case 'lastMonth': {
        const today = new Date();
        const month = new Date(today.getFullYear(), today.getMonth() - 1, 1);
        return { from: monthStart(month), to: monthEnd(month) };
      }
      case 'last3': {
        const today = new Date();
        const from = new Date(today.getFullYear(), today.getMonth() - 2, 1);
        return { from: monthStart(from), to: monthEnd(today) };
      }
      case 'last6': {
        const today = new Date();
        const from = new Date(today.getFullYear(), today.getMonth() - 5, 1);
        return { from: monthStart(from), to: monthEnd(today) };
      }
      case 'thisYear': {
        const today = new Date();
        return {
          from: toIso(new Date(today.getFullYear(), 0, 1)),
          to: toIso(new Date(today.getFullYear(), 11, 31)),
        };
      }
      case 'custom':
        return { from: this.customFrom(), to: this.customTo() };
    }
  });

  readonly label = computed(() => {
    const preset = PERIOD_PRESETS.find((p) => p.key === this.period());
    if (this.period() !== 'custom') return preset?.label ?? '';
    return `${this.customFrom()} to ${this.customTo()}`;
  });

  readonly monthYear = computed(() => {
    const { from } = this.range();
    const date = new Date(from);
    return { year: date.getFullYear(), month: date.getMonth() + 1 };
  });

  setPeriod(period: PeriodKey): void {
    this.period.set(period);
    if (period === 'custom') {
      const today = new Date();
      this.customFrom.set(toIso(startOfMonth(today)));
      this.customTo.set(toIso(today));
    }
  }

  setCustomRange(from: string, to: string): void {
    this.customFrom.set(from);
    this.customTo.set(to);
    this.period.set('custom');
  }

  toggleCategory(id: number): void {
    const current = this.categoryIds();
    this.categoryIds.set(
      current.includes(id) ? current.filter((c) => c !== id) : [...current, id],
    );
  }

  setType(type: number | null): void {
    this.type.set(type);
  }

  setAccount(accountId: number | null): void {
    this.accountId.set(accountId);
  }

  setSearch(search: string): void {
    this.search.set(search);
  }

  reset(): void {
    this.categoryIds.set([]);
    this.type.set(null);
    this.accountId.set(null);
    this.search.set('');
    this.period.set('thisMonth');
  }

  isExpenseActive(): boolean {
    return this.type() === TransactionTypeValue.Expense;
  }
}
