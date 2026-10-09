import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { TransactionService } from './transaction.service';
import { PagedResult, Transaction } from '../models/models';
import { environment } from '../../../environments/environment';

describe('TransactionService', () => {
  let service: TransactionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TransactionService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TransactionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends filters, pagination and repeated category ids as query parameters', () => {
    service
      .query({
        from: '2026-01-01',
        to: '2026-01-31',
        categoryIds: [1, 2],
        type: 'Expense',
        accountId: 3,
        search: 'rent',
        page: 2,
        pageSize: 10,
        sortBy: 'amount',
        descending: false,
      })
      .subscribe();

    const request = httpMock.expectOne(
      (req) =>
        req.url === `${environment.apiUrl}/transactions` &&
        req.params.get('from') === '2026-01-01' &&
        req.params.get('to') === '2026-01-31' &&
        req.params.get('type') === '1' &&
        req.params.get('accountId') === '3' &&
        req.params.get('search') === 'rent' &&
        req.params.get('page') === '2' &&
        req.params.get('pageSize') === '10' &&
        req.params.get('sortBy') === 'amount' &&
        req.params.get('descending') === 'false' &&
        req.params.getAll('categoryIds')!.length === 2,
    );
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0 } as PagedResult<Transaction>);
  });

  it('maps income to the numeric enum value expected by the API', () => {
    service.query({ type: 'Income', page: 1, pageSize: 5 }).subscribe();

    const request = httpMock.expectOne((req) => req.url === `${environment.apiUrl}/transactions`);
    expect(request.request.params.get('type')).toBe('0');
    request.flush({ items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 });
  });
});
