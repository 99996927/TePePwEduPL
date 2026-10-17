import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type StatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

const TONES: Readonly<Record<string, StatusTone>> = {
  Approved: 'success',
  Pending: 'warning',
  Rejected: 'danger',
  Cancelled: 'neutral',
  Draft: 'neutral',
};

export function statusTone(status: string): StatusTone {
  return Object.hasOwn(TONES, status) ? TONES[status] : 'neutral';
}

@Component({
  selector: 'app-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="status-chip status-chip--{{ tone() }}" [attr.data-tone]="tone()">{{ label() }}</span>`,
})
export class StatusChip {
  readonly status = input.required<string>();
  readonly label = input.required<string>();

  protected readonly tone = computed(() => statusTone(this.status()));
}
