import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ConfirmRequest, ConfirmService } from '../../../core/services/confirm.service';

@Component({
  selector: 'app-confirm-dialog',
  template: `
    @if (request(); as req) {
      <div class="modal-backdrop" (click)="cancel()">
        <div
          class="modal"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="req.title"
          (click)="$event.stopPropagation()"
        >
          <div class="modal-header">
            <h3 class="card-title">{{ req.title }}</h3>
            <button class="icon-btn" type="button" aria-label="Close" (click)="cancel()">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><path d="M18 6 6 18M6 6l12 12"/></svg>
            </button>
          </div>
          <div class="modal-body">
            <p class="text-muted">{{ req.message }}</p>
          </div>
          <div class="modal-footer">
            <button class="btn" type="button" (click)="cancel()">
              {{ req.cancelLabel ?? 'Cancel' }}
            </button>
            <button
              class="btn"
              [class.btn-danger]="req.danger"
              [class.btn-primary]="!req.danger"
              type="button"
              (click)="accept()"
            >
              {{ req.confirmLabel ?? 'Confirm' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialog implements OnInit, OnDestroy {
  private readonly confirmService = inject(ConfirmService);
  private subscription: Subscription | null = null;

  readonly request = signal<(ConfirmRequest & { resolve: (value: boolean) => void }) | null>(null);

  ngOnInit(): void {
    this.subscription = this.confirmService.requests.subscribe((request) => {
      this.request.set(request);
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  accept(): void {
    this.request()?.resolve(true);
    this.request.set(null);
  }

  cancel(): void {
    this.request()?.resolve(false);
    this.request.set(null);
  }
}


