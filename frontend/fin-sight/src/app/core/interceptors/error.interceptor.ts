import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { NotificationService } from '../services/notification.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly notifications = inject(NotificationService);

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError((error: HttpErrorResponse) => {
        const message = this.describe(error);
        this.notifications.error(message);
        return throwError(() => error);
      }),
    );
  }

  private describe(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Cannot reach the FinSight API. Is the backend running?';
    }
    const body = error.error;
    if (body && typeof body === 'object') {
      const errors = (body as { errors?: string[] }).errors;
      if (Array.isArray(errors) && errors.length > 0) return errors.join(' ');
      const title = (body as { title?: string }).title;
      if (title) return title;
    }
    if (typeof body === 'string' && body.length > 0 && body.length < 300) return body;
    return `Request failed with status ${error.status}.`;
  }
}
