import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Category,
  PagedResult,
  Transaction,
  TransactionCreateRequest,
  TransactionQuery,
  TransactionUpdateRequest,
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/transactions`;

  query(query: Partial<TransactionQuery>): Observable<PagedResult<Transaction>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 20)
      .set('sortBy', query.sortBy ?? 'date')
      .set('descending', query.descending ?? true);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    if (query.search) params = params.set('search', query.search);
    if (query.type) params = params.set('type', query.type === 'Expense' ? 1 : 0);
    if (query.accountId != null) params = params.set('accountId', query.accountId);
    for (const id of query.categoryIds ?? []) params = params.append('categoryIds', id);
    return this.http.get<PagedResult<Transaction>>(this.base, { params });
  }

  get(id: number): Observable<Transaction> {
    return this.http.get<Transaction>(`${this.base}/${id}`);
  }

  create(transaction: TransactionCreateRequest): Observable<Transaction> {
    return this.http.post<Transaction>(this.base, transaction);
  }

  update(id: number, transaction: TransactionUpdateRequest): Observable<Transaction> {
    return this.http.put<Transaction>(`${this.base}/${id}`, transaction);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/categories`;

  list(includeArchived = true): Observable<Category[]> {
    return this.http.get<Category[]>(this.base, { params: { includeArchived } });
  }

  create(category: {
    name: string;
    color?: string;
    appliesTo?: string;
    isArchived?: boolean;
  }): Observable<Category> {
    return this.http.post<Category>(this.base, category);
  }

  update(
    id: number,
    category: { name: string; color?: string; appliesTo?: string; isArchived?: boolean },
  ): Observable<Category> {
    return this.http.put<Category>(`${this.base}/${id}`, category);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
