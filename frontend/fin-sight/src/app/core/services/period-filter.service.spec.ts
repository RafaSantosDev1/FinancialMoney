import { TestBed } from '@angular/core/testing';
import { PeriodFilterService } from './period-filter.service';

describe('PeriodFilterService', () => {
  let service: PeriodFilterService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [PeriodFilterService] });
    service = TestBed.inject(PeriodFilterService);
  });

  it('starts on the current month covering the whole month', () => {
    const today = new Date();
    const expectedFrom = `${today.getFullYear()}-${`${today.getMonth() + 1}`.padStart(2, '0')}-01`;
    const expectedTo = `${today.getFullYear()}-${`${today.getMonth() + 1}`.padStart(2, '0')}-${new Date(today.getFullYear(), today.getMonth() + 1, 0).getDate()}`;
    const range = service.range();
    expect(service.period()).toBe('thisMonth');
    expect(range.from).toBe(expectedFrom);
    expect(range.to).toBe(expectedTo);
  });

  it('moves the window to the last month', () => {
    service.setPeriod('lastMonth');
    const today = new Date();
    const month = today.getMonth() === 0 ? 12 : today.getMonth();
    const year = today.getMonth() === 0 ? today.getFullYear() - 1 : today.getFullYear();
    const daysInMonth = new Date(year, month, 0).getDate();
    expect(service.range()).toEqual({
      from: `${year}-${`${month}`.padStart(2, '0')}-01`,
      to: `${year}-${`${month}`.padStart(2, '0')}-${daysInMonth}`,
    });
  });

  it('keeps only the requested months in the last 3 months window', () => {
    service.setPeriod('last3');
    const range = service.range();
    const from = new Date(range.from);
    const to = new Date(range.to);
    const months = (to.getFullYear() - from.getFullYear()) * 12 + (to.getMonth() - from.getMonth()) + 1;
    expect(months).toBe(3);
    expect(service.label()).toBe('Last 3 months');
  });

  it('supports custom ranges', () => {
    service.setCustomRange('2026-01-01', '2026-03-31');
    expect(service.period()).toBe('custom');
    expect(service.range()).toEqual({ from: '2026-01-01', to: '2026-03-31' });
    expect(service.label()).toBe('2026-01-01 to 2026-03-31');
  });

  it('toggles category filters', () => {
    service.toggleCategory(3);
    expect(service.categoryIds()).toEqual([3]);
    service.toggleCategory(7);
    expect(service.categoryIds()).toEqual([3, 7]);
    service.toggleCategory(3);
    expect(service.categoryIds()).toEqual([7]);
  });

  it('resets every filter', () => {
    service.setPeriod('thisYear');
    service.toggleCategory(1);
    service.setType(1);
    service.setAccount(2);
    service.setSearch('rent');

    service.reset();

    expect(service.period()).toBe('thisMonth');
    expect(service.categoryIds()).toEqual([]);
    expect(service.type()).toBeNull();
    expect(service.accountId()).toBeNull();
    expect(service.search()).toBe('');
  });

  it('derives the month used by the budgets page', () => {
    service.setCustomRange('2026-02-10', '2026-02-20');
    expect(service.monthYear()).toEqual({ year: 2026, month: 2 });
  });
});
