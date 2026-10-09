import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Account,
  Budget,
  BudgetStatus,
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/accounts`;

  list(includeArchived = true): Observable<Account[]> {
    return this.http.get<Account[]>(this.base, { params: { includeArchived } });
  }

  create(account: {
    name: string;
    accountType?: string;
    isArchived?: boolean;
  }): Observable<Account> {
    return this.http.post<Account>(this.base, account);
  }

  update(
    id: number,
    account: { name: string; accountType?: string; isArchived?: boolean },
  ): Observable<Account> {
    return this.http.put<Account>(`${this.base}/${id}`, account);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class BudgetService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/budgets`;

  list(year?: number, month?: number): Observable<Budget[]> {
    let params = new HttpParams();
    if (year != null) params = params.set('year', year);
    if (month != null) params = params.set('month', month);
    return this.http.get<Budget[]>(this.base, { params });
  }

  status(year: number, month: number): Observable<BudgetStatus[]> {
    return this.http.get<BudgetStatus[]>(`${this.base}/status`, {
      params: { year, month },
    });
  }

  create(budget: {
    categoryId: number | null;
    year: number;
    month: number;
    limitAmount: number;
  }): Observable<Budget> {
    return this.http.post<Budget>(this.base, budget);
  }

  update(
    id: number,
    budget: { categoryId: number | null; year: number; month: number; limitAmount: number },
  ): Observable<Budget> {
    return this.http.put<Budget>(`${this.base}/${id}`, budget);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
