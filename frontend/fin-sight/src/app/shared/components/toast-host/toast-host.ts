import { Component, inject } from '@angular/core';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-toast-host',
  template: `
    <div class="toast-host" role="status" aria-live="polite">
      @for (toast of notifications.activeToasts(); track toast.id) {
        <div class="toast {{ toast.kind }}">
          @if (toast.kind === 'success') {
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="#34d399" stroke-width="2.2"><path d="M20 6 9 17l-5-5"/></svg>
          } @else if (toast.kind === 'error') {
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="#fb7185" stroke-width="2.2"><circle cx="12" cy="12" r="9"/><path d="M12 8v5m0 3h.01"/></svg>
          } @else {
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="#22d3ee" stroke-width="2.2"><circle cx="12" cy="12" r="9"/><path d="M12 11v5m0-8h.01"/></svg>
          }
          <span>{{ toast.message }}</span>
          <button class="icon-btn" type="button" aria-label="Dismiss" (click)="notifications.dismiss(toast.id)">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastHost {
  readonly notifications = inject(NotificationService);
}

