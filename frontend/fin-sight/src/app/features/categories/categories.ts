import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CategoryService, TransactionService } from '../../core/services/transaction.service';
import { NotificationService } from '../../core/services/notification.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { Category } from '../../core/models/models';

@Component({
  selector: 'app-categories',
  imports: [ReactiveFormsModule],
  templateUrl: './categories.html',
  styleUrl: './categories.css',
})
export class Categories implements OnInit {
  private readonly categoryService = inject(CategoryService);
  private readonly transactionService = inject(TransactionService);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);
  private readonly fb = inject(FormBuilder);

  readonly categories = signal<Category[]>([]);
  readonly loading = signal(true);
  readonly editingId = signal<number | null>(null);
  readonly usage = signal<Record<number, number>>({});

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    color: ['#38bdf8', [Validators.required]],
    appliesTo: ['Both'],
    isArchived: [false],
  });

  readonly palette = [
    '#38bdf8', '#a78bfa', '#34d399', '#fb7185', '#fbbf24',
    '#818cf8', '#f472b6', '#2dd4bf', '#fb923c', '#4ade80', '#94a3b8',
  ];

  readonly appliesToLabels: Record<string, string> = {
    Expense: 'Expenses only',
    Income: 'Income only',
    Both: 'Income & expenses',
  };

  readonly sorted = computed(() =>
    [...this.categories()].sort((a, b) => Number(a.isArchived) - Number(b.isArchived) || a.name.localeCompare(b.name)),
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.categoryService.list(true).subscribe((categories) => {
      this.categories.set(categories);
      this.loading.set(false);
      this.computeUsage(categories);
    });
  }

  private computeUsage(categories: Category[]): void {
    const entries: Record<number, number> = {};
    for (const category of categories) {
      this.transactionService.query({ categoryIds: [category.id], page: 1, pageSize: 1 }).subscribe((result) => {
        entries[category.id] = result.totalCount;
        this.usage.set({ ...entries });
      });
    }
  }

  usageCount(categoryId: number): number {
    return this.usage()[categoryId] ?? 0;
  }

  edit(category: Category): void {    this.editingId.set(category.id);
    this.form.reset({
      name: category.name,
      color: category.color,
      appliesTo: category.appliesTo,
      isArchived: category.isArchived,
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ name: '', color: '#38bdf8', appliesTo: 'Both', isArchived: false });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const id = this.editingId();
    const request = id
      ? this.categoryService.update(id, value)
      : this.categoryService.create(value);

    request.subscribe({
      next: () => {
        this.notifications.success(id ? 'Category updated.' : 'Category created.');
        this.cancelEdit();
        this.load();
      },
    });
  }

  async remove(category: Category): Promise<void> {
    const used = this.usageCount(category.id);
    const confirmed = await this.confirm.confirm({
      title: 'Delete category',
      message:
        used > 0
          ? `"${category.name}" is used by ${used} transaction(s) and cannot be deleted. Archive it instead.`
          : `Category "${category.name}" will be deleted.`,
      confirmLabel: used > 0 ? 'Understood' : 'Delete',
      danger: used === 0,
    });
    if (!confirmed || used > 0) return;

    this.categoryService.remove(category.id).subscribe(() => {
      this.notifications.success('Category deleted.');
      this.load();
    });
  }
}
