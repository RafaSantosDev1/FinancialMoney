import { formatCompactCurrency, formatCurrency, formatDate, formatPercent, formatSignedCurrency } from './format';

describe('formatting utils', () => {
  it('formats currency with two decimals', () => {
    const formatted = formatCurrency(1234.5);
    expect(formatted).toContain('1');
    expect(formatted).toMatch(/€/);
    expect(formatted.replace(/\s/g, '')).toBe('1234,50€');
  });

  it('formats null values as a dash', () => {
    expect(formatCurrency(null)).toBe('—');
    expect(formatCurrency(undefined)).toBe('—');
  });

  it('formats signed currency with an explicit sign', () => {
    expect(formatSignedCurrency(50)).toBe(formatCurrency(50).replace('50,00', '+50,00'));
    expect(formatSignedCurrency(-50)).toBe(formatCurrency(50).replace('50,00', '-50,00'));
  });

  it('formats percentages with an explicit sign for positive values', () => {
    expect(formatPercent(12.34)).toBe('+12.3%');
    expect(formatPercent(-5)).toBe('-5.0%');
    expect(formatPercent(0)).toBe('0.0%');
    expect(formatPercent(null)).toBe('—');
  });

  it('formats compact currency', () => {
    expect(formatCompactCurrency(1500)).toMatch(/€/);
  });

  it('formats ISO dates in a readable way', () => {
    const formatted = formatDate('2026-01-15');
    expect(formatted).toContain('15');
    expect(formatted).toContain('2026');
    expect(formatDate(null)).toBe('—');
    expect(formatDate('not-a-date')).toBe('not-a-date');
  });
});
