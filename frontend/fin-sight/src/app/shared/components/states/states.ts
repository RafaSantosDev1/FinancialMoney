import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-empty-state',
  imports: [RouterLink],
  template: `
    <div class="state">
      <svg width="46" height="46" viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="1.5">
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-3.5-3.5" />
      </svg>
      <div style="font-weight: 650; color: var(--text)">{{ title() }}</div>
      <p class="text-muted" style="max-width: 420px">{{ message() }}</p>
      @if (actionLabel()) {
        <a class="btn btn-primary btn-sm" [routerLink]="actionLink()">{{ actionLabel() }}</a>
      }
    </div>
  `,
})
export class EmptyState {
  readonly title = input<string>('Nothing here yet');
  readonly message = input<string>('');
  readonly actionLabel = input<string>('');
  readonly actionLink = input<string>('/import');
}

@Component({
  selector: 'app-loading',
  template: `
    <div class="state">
      <div class="spinner" role="status" aria-label="Loading"></div>
      <span class="text-muted">{{ message() }}</span>
    </div>
  `,
})
export class Loading {
  readonly message = input<string>('Loading…');
}

