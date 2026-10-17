import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { StatusChip, statusTone } from './status-chip';

@Component({
  imports: [StatusChip],
  template: '<app-status-chip [status]="status" [label]="label" />',
})
class Host {
  status = 'Approved';
  label = 'Zatwierdzony';
}

describe('statusTone', () => {
  it('maps_known_statuses_to_tones', () => {
    expect(statusTone('Approved')).toBe('success');
    expect(statusTone('Pending')).toBe('warning');
    expect(statusTone('Rejected')).toBe('danger');
    expect(statusTone('Cancelled')).toBe('neutral');
    expect(statusTone('Draft')).toBe('neutral');
  });

  it('unknown_empty_or_inherited_keys_are_neutral', () => {
    expect(statusTone('Zażółć')).toBe('neutral');
    expect(statusTone('')).toBe('neutral');
    expect(statusTone('toString')).toBe('neutral');
  });
});

describe('StatusChip', () => {
  function render(status: string, label: string) {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.status = status;
    fixture.componentInstance.label = label;
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders_only_the_label_with_tone', () => {
    const el = render('Approved', 'Zatwierdzony');
    const chip = el.querySelector('.status-chip')!;
    expect(chip.textContent?.trim()).toBe('Zatwierdzony');
    expect(chip.getAttribute('data-tone')).toBe('success');
  });

  it('unknown_status_is_neutral_and_keeps_label', () => {
    const el = render('Nieznany', 'Nieznany');
    const chip = el.querySelector('.status-chip')!;
    expect(chip.getAttribute('data-tone')).toBe('neutral');
    expect(chip.textContent?.trim()).toBe('Nieznany');
  });
});
