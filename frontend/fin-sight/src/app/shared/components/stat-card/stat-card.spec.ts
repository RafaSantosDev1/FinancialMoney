import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatCard } from './stat-card';

describe('StatCard', () => {
  let fixture: ComponentFixture<StatCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatCard],
    }).compileComponents();

    fixture = TestBed.createComponent(StatCard);
  });

  it('renders the label, value and footnote', () => {
    fixture.componentRef.setInput('label', 'Total income');
    fixture.componentRef.setInput('value', '1000,00 €');
    fixture.componentRef.setInput('accent', '#34d399');
    fixture.componentRef.setInput('footnote', 'vs previous period');
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Total income');
    expect(element.textContent).toContain('1000,00 €');
    expect(element.textContent).toContain('vs previous period');
    expect(element.querySelector('.progress')).toBeNull();
  });

  it('renders a progress bar when a progress value is provided', () => {
    fixture.componentRef.setInput('label', 'Budget used');
    fixture.componentRef.setInput('value', '42%');
    fixture.componentRef.setInput('progress', 42);
    fixture.detectChanges();

    const fill = fixture.nativeElement.querySelector('.progress-fill') as HTMLElement | null;
    expect(fill).not.toBeNull();
    expect(fill!.style.width).toBe('42%');
  });
});
