import { Component, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { PeriodFilterService } from '../../core/services/period-filter.service';
import { AccountService } from '../../core/services/account.service';
import { CategoryService, TransactionService } from '../../core/services/transaction.service';
import { NotificationService } from '../../core/services/notification.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { Account, Category, Transaction } from '../../core/models/models';
import { formatCurrency, formatDate } from '../../core/utils/format';
import { EmptyState, Loading } from '../../shared/components/states/states';
import { TransactionFormComponent } from './transaction-form.component';

@Component({
  selector: 'app-transactions',
  imports: [RouterModule, EmptyState, Loading, TransactionFormComponent],
  templateUrl: './transactions.html',
  styleUrl: './transactions.css',
})
export class Transactions implements OnInit {
  private readonly transactionService = inject(TransactionService);
  private readonly categoriesService = inject(CategoryService);
  private readonly accountsService = inject(AccountService);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);
  readonly filters = inject(PeriodFilterService);

  readonly loading = signal(true);
  readonly transactions = signal<Transaction[]>([]);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly page = signal(1);
  readonly pageSize = signal(15);
  readonly categories = signal<Category[]>([]);
  readonly accounts = signal<Account[]>([]);

  readonly categoryLookup = computed(() => {
    const map = new Map<number, Category>();
    for (const category of this.categories()) map.set(category.id, category);
    return map;
  });

  readonly hasData = computed(() => this.totalCount() > 0);

  @ViewChild('formRef') formRef!: TransactionFormComponent;

  ngOnInit(): void {
    this.categoriesService.list().subscribe((categories) => this.categories.set(categories));
    this.accountsService.list().subscribe((accounts) => this.accounts.set(accounts));
    this.load();
  }

  onFiltersChanged(): void {
    this.page.set(1);
    this.load();
  }

  load(): void {
    const range = this.filters.range();
    this.loading.set(true);
    this.transactionService
      .query({
        from: range.from,
        to: range.to,
        categoryIds: this.filters.categoryIds(),
        type: this.filters.type() === 0 ? 'Income' : this.filters.type() === 1 ? 'Expense' : null,
        accountId: this.filters.accountId(),
        search: this.filters.search(),
        page: this.page(),
        pageSize: this.pageSize(),
        sortBy: 'date',
        descending: true,
      })
      .subscribe((result) => {
        this.transactions.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.loading.set(false);
      });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.page.set(page);
    this.load();
  }

  openCreate(): void {
    this.formRef?.open({
      accounts: this.accounts(),
      categories: this.categories(),
      onSaved: () => this.afterSave('Transaction created.'),
    });
  }

  edit(transaction: Transaction): void {
    this.formRef?.open({
      transaction,
      accounts: this.accounts(),
      categories: this.categories(),
      onSaved: () => this.afterSave('Transaction updated.'),
    });
  }

  async remove(transaction: Transaction): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Delete transaction',
      message: `"${transaction.description}" (${formatCurrency(transaction.amount)}) will be permanently removed.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!confirmed) return;

    this.transactionService.remove(transaction.id).subscribe(() => {
      this.notifications.success('Transaction deleted.');
      this.load();
    });
  }

  private afterSave(message: string): void {
    this.notifications.success(message);
    this.load();
  }

  protected readonly formatCurrency = formatCurrency;
  protected readonly formatDate = formatDate;
  protected readonly Number = Number;
}
