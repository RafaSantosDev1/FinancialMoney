import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ImportService } from '../../core/services/import.service';
import { NotificationService } from '../../core/services/notification.service';
import {
  ColumnMapping,
  ImportConfirmRequest,
  ImportOptions,
  ImportPreviewRequest,
  ImportPreviewResponse,
  ImportPreviewRow,
  ImportResult,
} from '../../core/models/models';

type Step = 'select' | 'preview' | 'result';

const DATE_FORMATS = [
  'auto',
  'dd/MM/yyyy',
  'd/M/yyyy',
  'dd-MM-yyyy',
  'dd.MM.yyyy',
  'yyyy-MM-dd',
  'yyyy/MM/dd',
  'MM/dd/yyyy',
  'dd/MM/yyyy HH:mm:ss',
  'yyyy-MM-dd HH:mm:ss',
];

const MAPPING_FIELDS: { key: keyof ColumnMapping; label: string; optional: boolean }[] = [
  { key: 'dateColumn', label: 'Date', optional: false },
  { key: 'descriptionColumn', label: 'Description', optional: false },
  { key: 'amountColumn', label: 'Amount', optional: false },
  { key: 'debitColumn', label: 'Debit', optional: true },
  { key: 'creditColumn', label: 'Credit', optional: true },
  { key: 'typeColumn', label: 'Type', optional: true },
  { key: 'categoryColumn', label: 'Category', optional: true },
  { key: 'accountColumn', label: 'Account', optional: true },
  { key: 'referenceColumn', label: 'Reference', optional: true },
];

@Component({
  selector: 'app-import-page',
  imports: [RouterLink],
  templateUrl: './import.html',
  styleUrl: './import.css',
})
export class ImportPage {
  private readonly importService = inject(ImportService);
  private readonly notifications = inject(NotificationService);

  readonly step = signal<Step>('select');
  readonly fileName = signal('');
  readonly content = signal('');
  readonly fileError = signal('');
  readonly dragging = signal(false);
  readonly previewing = signal(false);
  readonly importing = signal(false);

  readonly delimiter = signal('auto');
  readonly dateFormat = signal('auto');
  readonly decimalSeparator = signal('auto');
  readonly hasHeader = signal(true);
  readonly defaultType = signal<string>('');

  readonly preview = signal<ImportPreviewResponse | null>(null);
  readonly mapping = signal<ColumnMapping | null>(null);
  readonly importDuplicates = signal(false);
  readonly result = signal<ImportResult | null>(null);

  @ViewChild('fileInput')
  private readonly fileInput!: ElementRef<HTMLInputElement>;

  readonly dateFormats = DATE_FORMATS;
  readonly mappingFields = MAPPING_FIELDS;

  readonly detectedInfo = signal<string>('');

  openFilePicker(): void {
    this.fileInput.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.readFile(file);
    input.value = '';
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.readFile(file);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  onDragLeave(): void {
    this.dragging.set(false);
  }

  private readFile(file: File): void {
    this.fileError.set('');

    const isCsv =
      file.type === 'text/csv' ||
      file.name.toLowerCase().endsWith('.csv') ||
      file.type === 'application/vnd.ms-excel';
    if (!isCsv) {
      this.fileError.set('Only CSV files are supported.');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.fileError.set('The file is larger than the 5 MB limit.');
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.content.set(String(reader.result ?? ''));
      this.fileName.set(file.name);
      this.resetImportState();
    };
    reader.onerror = () => this.fileError.set('The file could not be read.');
    reader.readAsText(file, 'UTF-8');
  }

  private resetImportState(): void {
    this.preview.set(null);
    this.mapping.set(null);
    this.result.set(null);
    this.step.set('select');
  }

  currentOptions(): ImportOptions {
    return {
      delimiter: this.delimiter() === 'auto' ? null : this.delimiter(),
      dateFormat: this.dateFormat() === 'auto' ? null : this.dateFormat(),
      decimalSeparator: this.decimalSeparator() === 'auto' ? null : this.decimalSeparator(),
      hasHeader: this.hasHeader(),
      defaultType: this.defaultType() === '' ? null : this.defaultType() === 'Income' ? 'Income' : 'Expense',
    };
  }

  runPreview(): void {
    if (!this.content()) {
      this.fileError.set('Select a CSV file first.');
      return;
    }

    this.previewing.set(true);
    const request: ImportPreviewRequest = {
      fileName: this.fileName(),
      content: this.content(),
      options: this.currentOptions(),
      mapping: null,
    };

    this.importService.preview(request).subscribe({
      next: (response) => {
        this.previewing.set(false);
        this.preview.set(response);
        this.mapping.set({ ...response.effectiveMapping });
        this.detectedInfo.set(
          `Detected: delimiter "${response.delimiter === '\t' ? 'TAB' : response.delimiter}", ` +
            `date format ${response.detectedDateFormat || 'unknown'}, ` +
            `decimal separator "${response.detectedDecimalSeparator}", ` +
            `type inference: ${response.typeInferenceSource}.`,
        );
        this.step.set('preview');
      },
      error: () => this.previewing.set(false),
    });
  }

  updateMapping(field: keyof ColumnMapping, value: string): void {
    const current = this.mapping();
    if (!current) return;
    const numeric = value === '' ? null : Number(value);
    this.mapping.set({ ...current, [field]: numeric } as ColumnMapping);
  }

  rePreview(): void {
    if (!this.content() || !this.mapping()) return;

    this.previewing.set(true);
    const request: ImportPreviewRequest = {
      fileName: this.fileName(),
      content: this.content(),
      options: this.currentOptions(),
      mapping: this.mapping(),
    };
    this.importService.preview(request).subscribe({
      next: (response) => {
        this.previewing.set(false);
        this.preview.set(response);
        this.step.set('preview');
      },
      error: () => this.previewing.set(false),
    });
  }

  confirmImport(): void {
    const preview = this.preview();
    const mapping = this.mapping();
    if (!preview || !mapping) return;

    this.importing.set(true);
    const request: ImportConfirmRequest = {
      fileName: this.fileName(),
      content: this.content(),
      options: this.currentOptions(),
      mapping,
      importDuplicates: this.importDuplicates(),
    };

    this.importService.confirm(request).subscribe({
      next: (result) => {
        this.importing.set(false);
        this.result.set(result);
        this.step.set('result');
        this.notifications.success(
          `${result.importedRows} transaction(s) imported successfully.`,
        );
      },
      error: () => this.importing.set(false),
    });
  }

  cancelImport(): void {
    this.content.set('');
    this.fileName.set('');
    this.resetImportState();
  }

  startOver(): void {
    this.cancelImport();
    this.notifications.info('You can select another file.');
  }

  rowStatus(status: number): string {
    switch (status) {
      case 0:
        return 'valid';
      case 1:
        return 'invalid';
      default:
        return 'duplicate';
    }
  }

  rowStatusLabel(status: number): string {
    switch (status) {
      case 0:
        return 'OK';
      case 1:
        return 'Invalid';
      default:
        return 'Duplicate';
    }
  }

  columnName(index: number | null): string {
    if (index == null || index < 0) return '—';
    return this.preview()?.headers[index] ?? `Column ${index + 1}`;
  }

  protected readonly formatRowDate = (row: ImportPreviewRow): string =>
    row.date ? row.date : (row.dateRaw ?? '—');

  protected readonly Number = Number;
}
