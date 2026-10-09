import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { ConfirmService } from '../../core/services/confirm.service';
import { NotificationService } from '../../core/services/notification.service';
import { AccountService } from '../../core/services/account.service';
import { CategoryService } from '../../core/services/transaction.service';
import { SeedService } from '../../core/services/import.service';
import { PeriodFilterService } from '../../core/services/period-filter.service';
import { Account, Category } from '../../core/models/models';
import { ToastHost } from '../../shared/components/toast-host/toast-host';

@Component({
  selector: 'app-shell',
  imports: [RouterModule, ToastHost],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell implements OnInit {
  readonly filters = inject(PeriodFilterService);
  readonly notifications = inject(NotificationService);
  readonly accountsService = inject(AccountService);
  readonly categoriesService = inject(CategoryService);
  readonly seed = inject(SeedService);
  private readonly confirm = inject(ConfirmService);
  private readonly router = inject(Router);

  readonly accounts = signal<Account[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly demoActive = signal(false);
  readonly demoTransactions = signal(0);
  readonly sidebarOpen = signal(false);
  readonly filterPanelOpen = signal(false);
  readonly customOpen = signal(false);

  readonly navItems = [
    { path: '/dashboard', label: 'Dashboard', icon: 'grid' },
    { path: '/transactions', label: 'Transactions', icon: 'list' },
    { path: '/import', label: 'Import CSV', icon: 'upload' },
    { path: '/categories', label: 'Categories', icon: 'tag' },
    { path: '/budgets', label: 'Budgets', icon: 'target' },
    { path: '/reports', label: 'Reports', icon: 'chart' },
  ];

  readonly selectedCategories = computed(() =>
    this.filters.categoryIds().filter((id) => this.categories().some((c) => c.id === id)),
  );

  ngOnInit(): void {
    this.accountsService.list().subscribe((accounts) => this.accounts.set(accounts));
    this.categoriesService.list().subscribe((categories) => this.categories.set(categories));
    this.loadDemoStatus();
    this.router.events.subscribe(() => this.sidebarOpen.set(false));
  }

  loadDemoStatus(): void {
    this.seed.status().subscribe((status) => {
      this.demoActive.set(status.hasDemoData);
      this.demoTransactions.set(status.demoTransactions);
    });
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  toggleFilterPanel(): void {
    this.filterPanelOpen.update((open) => !open);
  }

  onPeriodChange(value: string): void {
    this.filters.setPeriod(value as never);
    this.customOpen.set(value === 'custom');
  }

  onCustomRange(from: string, to: string): void {
    this.filters.setCustomRange(from, to);
  }

  onTypeChange(value: string): void {
    this.filters.setType(value === '' ? null : Number(value));
  }

  onAccountChange(value: string): void {
    this.filters.setAccount(value === '' ? null : Number(value));
  }

  toggleCategory(id: number): void {
    this.filters.toggleCategory(id);
  }

  clearFilters(): void {
    this.filters.reset();
    this.customOpen.set(false);
  }

  async removeDemoData(): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Remove demo data',
      message:
        'All demo categories, budgets and transactions will be deleted. Your imported data stays untouched.',
      confirmLabel: 'Remove demo data',
      danger: true,
    });
    if (!confirmed) return;

    this.seed.removeDemo().subscribe((removed) => {
      this.notifications.success(`Demo data removed (${removed} transactions).`);
      this.loadDemoStatus();
    });
  }

  trackByNav(_index: number, item: { path: string }): string {
    return item.path;
  }
}
