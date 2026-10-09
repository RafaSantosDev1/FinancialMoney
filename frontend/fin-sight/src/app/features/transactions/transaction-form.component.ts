import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TransactionService } from '../../core/services/transaction.service';
import {
  Account,
  Category,
  Transaction,
  TransactionCreateRequest,
  TransactionUpdateRequest,
} from '../../core/models/models';
import { formatCurrency, formatDate } from '../../core/utils/format';

@Component({
  selector: 'app-transaction-form',
  imports: [ReactiveFormsModule],
  template: `
    @if (visible()) {
      <div class="modal-backdrop" (click)="close()">
        <div
          class="modal"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="isEdit() ? 'Edit transaction' : 'New transaction'"
          (click)="$event.stopPropagation()"
        >
          <div class="modal-header">
            <h3 class="card-title">{{ isEdit() ? 'Edit transaction' : 'New transaction' }}</h3>
            <button class="icon-btn" type="button" aria-label="Close" (click)="close()">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><path d="M18 6 6 18M6 6l12 12"/></svg>
            </button>
          </div>

          <form [formGroup]="form" (ngSubmit)="submit()">
            <div class="modal-body col">
              <div class="form-grid">
                <div class="field">
                  <label for="date">Date</label>
                  <input id="date" class="input" type="date" formControlName="date" />
                  @if (form.controls.date.touched && form.controls.date.invalid) {
                    <span class="field-error">The date is required.</span>
                  }
                </div>

                <div class="field">
                  <label for="type">Type</label>
                  <select id="type" class="select" formControlName="type">
                    <option value="Expense">Expense</option>
                    <option value="Income">Income</option>
                  </select>
                </div>

                <div class="field">
                  <label for="amount">Amount (EUR)</label>
                  <input
                    id="amount"
                    class="input"
                    type="number"
                    step="0.01"
                    min="0"
                    inputmode="decimal"
                    formControlName="amount"
                    [class.invalid]="form.controls.amount.touched && form.controls.amount.invalid"
                  />
                  @if (form.controls.amount.touched && form.controls.amount.invalid) {
                    <span class="field-error">Enter an amount greater than zero.</span>
                  }
                </div>

                <div class="field">
                  <label for="category">Category</label>
                  <select
                    id="category"
                    class="select"
                    formControlName="categoryId"
                    [class.invalid]="form.controls.categoryId.touched && form.controls.categoryId.invalid"
                  >
                    <option [ngValue]="null" disabled>Select a category</option>
                    @for (category of availableCategories(); track category.id) {
                      <option [ngValue]="category.id">{{ category.name }}</option>
                    }
                  </select>
                </div>

                <div class="field" style="grid-column: span 2">
                  <label for="description">Description</label>
                  <input
                    id="description"
                    class="input"
                    type="text"
                    formControlName="description"
                    [class.invalid]="form.controls.description.touched && form.controls.description.invalid"
                  />
                  @if (form.controls.description.touched && form.controls.description.invalid) {
                    <span class="field-error">The description is required.</span>
                  }
                </div>

                <div class="field">
                  <label for="account">Account (optional)</label>
                  <select id="account" class="select" formControlName="accountId">
                    <option [ngValue]="null">No account</option>
                    @for (account of accounts(); track account.id) {
                      <option [ngValue]="account.id">{{ account.name }}</option>
                    }
                  </select>
                </div>

                <div class="field">
                  <label for="notes">Notes (optional)</label>
                  <input id="notes" class="input" type="text" formControlName="notes" />
                </div>
              </div>
            </div>

            <div class="modal-footer">
              <button class="btn" type="button" (click)="close()">Cancel</button>
              <button class="btn btn-primary" type="submit" [disabled]="form.invalid || saving()">
                {{ saving() ? 'Saving…' : isEdit() ? 'Save changes' : 'Add transaction' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    }
  `,
})
export class TransactionFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly transactions = inject(TransactionService);

  readonly visible = signal(false);
  readonly accounts = signal<Account[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly isEdit = signal(false);
  readonly saving = signal(false);
  private editId: number | null = null;

  readonly availableCategories = computed(() => {
    const type = this.form.controls.type.value;
    const base = this.categories().filter((c) => {
      if (type === 'Income') return c.appliesTo === 'Income' || c.appliesTo === 'Both';
      return c.appliesTo === 'Expense' || c.appliesTo === 'Both';
    });
    return base.length ? base : this.categories();
  });

  readonly form = this.fb.nonNullable.group({
    date: ['', Validators.required],
    type: ['Expense'],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    description: ['', [Validators.required, Validators.maxLength(300)]],
    categoryId: [null as number | null, Validators.required],
    accountId: [null as number | null],
    notes: [''],
  });

  private onSaved: ((transaction: Transaction) => void) | null = null;

  open(options: {
    transaction?: Transaction;
    accounts: Account[];
    categories: Category[];
    onSaved: (transaction: Transaction) => void;
  }): void {
    this.accounts.set(options.accounts);
    this.categories.set(options.categories);
    this.onSaved = options.onSaved;

    const transaction = options.transaction;
    this.isEdit.set(!!transaction);
    this.editId = transaction?.id ?? null;

    this.form.reset({
      date: transaction?.date ?? new Date().toISOString().slice(0, 10),
      type: transaction?.type ?? 'Expense',
      amount: transaction?.amount ?? 0,
      description: transaction?.description ?? '',
      categoryId: transaction?.categoryId ?? null,
      accountId: transaction?.accountId ?? null,
      notes: transaction?.notes ?? '',
    });

    this.visible.set(true);
  }

  close(): void {
    this.visible.set(false);
    this.saving.set(false);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const payload = {
      date: value.date,
      description: value.description.trim(),
      amount: Number(value.amount),
      type: value.type as 'Income' | 'Expense',
      categoryId: value.categoryId as number,
      accountId: value.accountId as number | null,
      notes: value.notes?.trim() || null,
    } satisfies TransactionCreateRequest | TransactionUpdateRequest;

    this.saving.set(true);
    const request = this.editId
      ? this.transactions.update(this.editId, payload)
      : this.transactions.create(payload);

    request.subscribe({
      next: (transaction) => {
        this.saving.set(false);
        this.onSaved?.(transaction);
        this.close();
      },
      error: () => this.saving.set(false),
    });
  }
}



