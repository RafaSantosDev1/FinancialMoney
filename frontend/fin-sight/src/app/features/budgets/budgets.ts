import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BudgetService } from '../../core/services/account.service';
import { CategoryService } from '../../core/services/transaction.service';
import { NotificationService } from '../../core/services/notification.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { Budget, BudgetStatus, Category } from '../../core/models/models';
import { formatCurrency, formatPercent } from '../../core/utils/format';

@Component({
  selector: 'app-budgets',
  imports: [ReactiveFormsModule],
  templateUrl: './budgets.html',
  styleUrl: './budgets.css',
})
export class Budgets implements OnInit {
  private readonly budgetService = inject(BudgetService);
  private readonly categoryService = inject(CategoryService);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);
  private readonly fb = inject(FormBuilder);

  readonly categories = signal<Category[]>([]);
  readonly budgets = signal<Budget[]>([]);
  readonly status = signal<BudgetStatus[]>([]);
  readonly loading = signal(true);
  readonly editingGlobal = signal(false);

  readonly year = signal(new Date().getFullYear());
  readonly month = signal(new Date().getMonth() + 1);

  readonly monthOptions = Array.from({ length: 12 }, (_, i) => ({
    value: i + 1,
    label: new Date(2024, i, 1).toLocaleDateString('en-GB', { month: 'long' }),
  }));

  readonly globalForm = this.fb.nonNullable.group({
    limitAmount: [0, [Validators.required, Validators.min(0)]],
  });

  readonly globalStatus = computed(() => this.status().find((b) => b.categoryId === null) ?? null);
  readonly globalBudget = computed(() => this.budgets().find((b) => b.categoryId === null) ?? null);

  readonly categoryStatus = computed(() => this.status().filter((b) => b.categoryId !== null));

  readonly withoutBudget = computed(() => {
    const withBudget = new Set(this.budgets().map((b) => b.categoryId));
    return this.categories().filter((c) => !c.isArchived && !withBudget.has(c.id));
  });

  private readonly categoryForms = new Map<
    number,
    ReturnType<Budgets['createCategoryForm']>
  >();

  createCategoryForm(limit = 0) {
    return this.fb.nonNullable.group({
      limitAmount: [limit, [Validators.required, Validators.min(0)]],
    });
  }

  categoryForm(categoryId: number) {
    let form = this.categoryForms.get(categoryId);
    if (!form) {
      const existing = this.status().find((b) => b.categoryId === categoryId);
      form = this.createCategoryForm(existing?.limitAmount ?? 0);
      this.categoryForms.set(categoryId, form);
    }
    return form;
  }

  ngOnInit(): void {
    this.categoryService.list().subscribe((categories) => this.categories.set(categories));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.categoryForms.clear();
    this.budgetService.list(this.year(), this.month()).subscribe((budgets) => this.budgets.set(budgets));
    this.budgetService.status(this.year(), this.month()).subscribe((status) => {
      this.status.set(status);
      this.loading.set(false);
      const global = status.find((b) => b.categoryId === null);
      if (global) this.globalForm.reset({ limitAmount: global.limitAmount });
    });
  }

  onMonthChange(month: string): void {
    this.month.set(Number(month));
    this.load();
  }

  onYearChange(year: string): void {
    this.year.set(Number(year));
    this.load();
  }

  saveGlobal(): void {
    const limit = Number(this.globalForm.getRawValue().limitAmount);
    const payload = { categoryId: null, year: this.year(), month: this.month(), limitAmount: limit };
    const existing = this.globalBudget();
    const request = existing
      ? this.budgetService.update(existing.id, payload)
      : this.budgetService.create(payload);

    request.subscribe({
      next: () => {
        this.notifications.success('Global budget saved.');
        this.editingGlobal.set(false);
        this.load();
      },
    });
  }

  saveCategory(category: Category): void {
    const limit = Number(this.categoryForm(category.id).getRawValue().limitAmount);
    const payload = {
      categoryId: category.id,
      year: this.year(),
      month: this.month(),
      limitAmount: limit,
    };
    const existing = this.budgets().find((b) => b.categoryId === category.id);
    const request = existing
      ? this.budgetService.update(existing.id, payload)
      : this.budgetService.create(payload);

    request.subscribe({
      next: () => {
        this.notifications.success(`Budget for ${category.name} saved.`);
        this.load();
      },
    });
  }

  saveCategoryById(budget: BudgetStatus): void {
    const id = this.budgets().find((b) => (b.categoryId ?? null) === (budget.categoryId ?? null))?.id;
    const limit = Number(this.categoryForm(budget.categoryId!).getRawValue().limitAmount);
    const payload = {
      categoryId: budget.categoryId,
      year: this.year(),
      month: this.month(),
      limitAmount: limit,
    };
    const request = id ? this.budgetService.update(id, payload) : this.budgetService.create(payload);

    request.subscribe({
      next: () => {
        this.notifications.success('Category budget saved.');
        this.load();
      },
    });
  }

  async removeBudget(budget: BudgetStatus): Promise<void> {
    const id = this.budgets().find(
      (b) => (b.categoryId ?? null) === (budget.categoryId ?? null),
    )?.id;
    if (!id) return;

    const confirmed = await this.confirm.confirm({
      title: 'Delete budget',
      message: `The ${budget.categoryName ?? 'global'} budget limit will be removed. Transactions are not affected.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!confirmed) return;

    this.budgetService.remove(id).subscribe({
      next: () => {
        this.notifications.success('Budget deleted.');
        this.load();
      },
    });
  }

  healthClass(health: string): string {
    switch (health) {
      case 'Exceeded':
        return 'danger';
      case 'Warning':
        return 'warning';
      case 'NoLimit':
        return '';
      default:
        return 'ok';
    }
  }

  healthColor(health: string): string {
    switch (health) {
      case 'Exceeded':
        return 'var(--coral)';
      case 'Warning':
        return 'var(--amber)';
      default:
        return 'var(--green)';
    }
  }

  protected readonly Math = Math;
  protected readonly formatCurrency = formatCurrency;
  protected readonly formatPercent = formatPercent;
}
