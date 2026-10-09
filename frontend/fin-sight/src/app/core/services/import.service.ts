import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ImportBatch,
  ImportConfirmRequest,
  ImportPreviewRequest,
  ImportPreviewResponse,
  ImportResult,
  SeedResult,
  SeedStatus,
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class ImportService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/imports`;

  preview(request: ImportPreviewRequest): Observable<ImportPreviewResponse> {
    return this.http.post<ImportPreviewResponse>(`${this.base}/preview`, request);
  }

  confirm(request: ImportConfirmRequest): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.base}/confirm`, request);
  }

  get(id: number): Observable<ImportBatch> {
    return this.http.get<ImportBatch>(`${this.base}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class SeedService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/seed`;

  status(): Observable<SeedStatus> {
    return this.http.get<SeedStatus>(`${this.base}/status`);
  }

  seedDemo(): Observable<SeedResult> {
    return this.http.post<SeedResult>(`${this.base}/demo`, {});
  }

  removeDemo(): Observable<number> {
    return this.http.delete<number>(`${this.base}/demo`);
  }
}
