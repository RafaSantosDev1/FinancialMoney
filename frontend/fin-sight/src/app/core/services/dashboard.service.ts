import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CategoryBreakdownItem,
  DashboardSummary,
  Insight,
  MonthlyTrendPoint,
  TopExpenseItem,
  WeekdaySpendingItem,
} from '../models/models';

export interface DashboardFilters {
  from: string;
  to: string;
  categoryIds: number[];
  type: number | null;
  accountId: number | null;
  search: string;
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/dashboard`;

  summary(filters: DashboardFilters): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.base}/summary`, {
      params: this.params(filters),
    });
  }

  monthlyTrends(filters: DashboardFilters): Observable<MonthlyTrendPoint[]> {
    return this.http.get<MonthlyTrendPoint[]>(`${this.base}/monthly-trends`, {
      params: this.params(filters),
    });
  }

  categoryBreakdown(
    filters: DashboardFilters,
    type: 'Income' | 'Expense' = 'Expense',
  ): Observable<CategoryBreakdownItem[]> {
    const params = this.params(filters).set('type', type === 'Expense' ? 1 : 0);
    return this.http.get<CategoryBreakdownItem[]>(`${this.base}/category-breakdown`, { params });
  }

  weekdaySpending(filters: DashboardFilters): Observable<WeekdaySpendingItem[]> {
    return this.http.get<WeekdaySpendingItem[]>(`${this.base}/weekday-spending`, {
      params: this.params(filters),
    });
  }

  topExpenses(filters: DashboardFilters, count = 8): Observable<TopExpenseItem[]> {
    const params = this.params(filters).set('count', count);
    return this.http.get<TopExpenseItem[]>(`${this.base}/top-expenses`, { params });
  }

  insights(filters: DashboardFilters): Observable<Insight[]> {
    return this.http.get<Insight[]>(`${this.base}/insights`, { params: this.params(filters) });
  }

  private params(filters: DashboardFilters): HttpParams {
    let params = new HttpParams()
      .set('from', filters.from)
      .set('to', filters.to);
    for (const id of filters.categoryIds) params = params.append('categoryIds', id);
    if (filters.type != null) params = params.set('type', filters.type);
    if (filters.accountId != null) params = params.set('accountId', filters.accountId);
    if (filters.search) params = params.set('search', filters.search);
    return params;
  }
}
