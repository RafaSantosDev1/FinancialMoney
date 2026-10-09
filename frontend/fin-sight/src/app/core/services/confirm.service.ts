import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface ConfirmRequest {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

interface PendingConfirm extends ConfirmRequest {
  resolve: (value: boolean) => void;
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly subject = new Subject<PendingConfirm>();
  readonly requests = this.subject.asObservable();

  confirm(request: ConfirmRequest): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      this.subject.next({ ...request, resolve });
    });
  }
}
