import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  kind: 'success' | 'error' | 'info';
  message: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly toasts = signal<Toast[]>([]);
  private counter = 0;

  readonly activeToasts = this.toasts.asReadonly();

  success(message: string): void {
    this.push('success', message);
  }

  error(message: string): void {
    this.push('error', message);
  }

  info(message: string): void {
    this.push('info', message);
  }

  dismiss(id: number): void {
    this.toasts.update((current) => current.filter((t) => t.id !== id));
  }

  private push(kind: Toast['kind'], message: string): void {
    const id = ++this.counter;
    this.toasts.update((current) => [...current, { id, kind, message }]);
    setTimeout(() => this.dismiss(id), 6000);
  }
}
