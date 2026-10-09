import { Component, input } from '@angular/core';

@Component({
  selector: 'app-stat-card',
  template: `
    <div class="card stat-card" [style.--accent]="accent()">
      <div class="stat-label">
        <span class="dot"></span>
        {{ label() }}
      </div>
      <div class="stat-value" [class]="valueClass()">
        {{ value() }}
      </div>
      <div class="stat-foot">
        @if (delta()) {
          <span class="delta" [class]="deltaClass()">{{ delta() }}</span>
        }
        <span>{{ footnote() }}</span>
      </div>
      @if (progress() !== null) {
        <div class="progress" style="margin-top: 6px">
          <div
            class="progress-fill"
            [style.width.%]="progress()"
            [style.background]="accent()"
          ></div>
        </div>
      }
    </div>
  `,
})
export class StatCard {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly accent = input<string>('#22d3ee');
  readonly footnote = input<string>('');
  readonly delta = input<string | null>(null);
  /** 'good' | 'bad' | 'flat' or '' */
  readonly deltaClass = input<string>('');
  readonly valueClass = input<string>('');
  readonly progress = input<number | null>(null);
}
