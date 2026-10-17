import { Component, DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Subscription } from 'rxjs';
import { AuditEntryDto } from '../../api/models/audit-entry-dto';
import { AuditService } from '../../api/services/audit.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatInstantDateTime } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { AUDIT_ENTITY_TYPES, auditEntityLabel, auditEventLabel } from '../../core/labels';
import { Notifier } from '../../core/notifier';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function guidOrEmpty(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value.trim();
  return value === '' || GUID.test(value) ? null : { guid: true };
}

@Injectable()
class PolishPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = 'Wpisów na stronie:';
  override nextPageLabel = 'Następna strona';
  override previousPageLabel = 'Poprzednia strona';
  override firstPageLabel = 'Pierwsza strona';
  override lastPageLabel = 'Ostatnia strona';
  override getRangeLabel = (page: number, size: number, length: number): string => {
    if (length === 0) return `0 z 0`;
    const start = page * size;
    return `${start + 1} – ${Math.min(start + size, length)} z ${length}`;
  };
}

interface AppliedFilters {
  entityType: string;
  entityId: string;
  actorUserId: string;
  from: string | null;
  to: string | null;
}

const NO_FILTERS: AppliedFilters = { entityType: '', entityId: '', actorUserId: '', from: null, to: null };

/** Pretty-prints a JSON string; anything unparsable is returned as raw text, empty values as a dash. */
export function prettyJson(value: string | null | undefined): string {
  if (value === null || value === undefined || value.trim() === '') return '—';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

@Component({
  selector: 'app-audit',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
  ],
  providers: [{ provide: MatPaginatorIntl, useClass: PolishPaginatorIntl }],
  templateUrl: './audit.html',
  styleUrl: './audit.scss',
})
export class Audit {
  private readonly api = inject(AuditService);
  private readonly notifier = inject(Notifier);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly columns = ['toggle', 'time', 'actor', 'event', 'entityType', 'entityId'];
  protected readonly entityTypes = AUDIT_ENTITY_TYPES;
  protected readonly pageSizes = [25, 50, 100];
  protected readonly entityLabel = auditEntityLabel;
  protected readonly eventLabel = auditEventLabel;
  protected readonly formatTime = formatInstantDateTime;
  protected readonly pretty = prettyJson;

  protected readonly entityType = new FormControl('', { nonNullable: true });
  protected readonly entityId = new FormControl('', { nonNullable: true, validators: [guidOrEmpty] });
  protected readonly actor = new FormControl('', { nonNullable: true });
  protected readonly range = new FormGroup({
    start: new FormControl<Date | null>(null),
    end: new FormControl<Date | null>(null),
  });

  protected readonly users = computed(() =>
    this.directory
      .employees()
      .filter((e) => !!e.userId)
      .map((e) => ({ userId: e.userId as string, name: `${e.firstName} ${e.lastName}` })),
  );

  protected readonly items = signal<AuditEntryDto[]>([]);
  protected readonly total = signal(0);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(25);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly invalidId = signal(false);
  private readonly expanded = signal<ReadonlySet<number>>(new Set());

  private applied: AppliedFilters = NO_FILTERS;
  private request: Subscription | null = null;

  /** A fresh array on every expand/collapse, so the table re-evaluates which rows get a detail row. */
  protected readonly rows = computed(() => {
    this.expanded();
    return [...this.items()];
  });

  protected readonly isExpanded = (_index: number, row: AuditEntryDto): boolean => this.expanded().has(row.id);

  constructor() {
    this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    this.load();
  }

  protected actorLabel(id: string | null): string {
    return id === null ? 'System (seed)' : this.directory.userLabel(id);
  }

  protected shortId(id: string): string {
    return id.length > 8 ? `${id.slice(0, 8)}…` : id;
  }

  protected isOpen(row: AuditEntryDto): boolean {
    return this.expanded().has(row.id);
  }

  protected toggle(row: AuditEntryDto): void {
    const next = new Set(this.expanded());
    if (!next.delete(row.id)) next.add(row.id);
    this.expanded.set(next);
  }

  protected search(): void {
    this.entityId.markAsTouched();
    if (this.entityId.invalid) {
      this.invalidId.set(true);
      return;
    }
    this.invalidId.set(false);
    const { start, end } = this.range.getRawValue();
    this.applied = {
      entityType: this.entityType.value,
      entityId: this.entityId.value.trim(),
      actorUserId: this.actor.value,
      from: start ? new Date(start.getFullYear(), start.getMonth(), start.getDate()).toISOString() : null,
      to: end ? new Date(end.getFullYear(), end.getMonth(), end.getDate(), 23, 59, 59, 999).toISOString() : null,
    };
    this.pageIndex.set(0);
    this.load();
  }

  protected clear(): void {
    this.entityType.setValue('');
    this.entityId.reset('');
    this.actor.setValue('');
    this.range.reset();
    this.invalidId.set(false);
    this.applied = NO_FILTERS;
    this.pageIndex.set(0);
    this.load();
  }

  protected page(event: PageEvent): void {
    if (event.pageSize !== this.pageSize()) {
      this.pageSize.set(event.pageSize);
      this.pageIndex.set(0);
    } else {
      this.pageIndex.set(event.pageIndex);
    }
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected load(): void {
    this.request?.unsubscribe();
    const f = this.applied;
    this.loading.set(true);
    this.request = this.api
      .auditQuery({
        entityType: f.entityType || undefined,
        entityId: f.entityId || undefined,
        actorUserId: f.actorUserId || undefined,
        from: f.from ?? undefined,
        to: f.to ?? undefined,
        skip: this.pageIndex() * this.pageSize(),
        take: this.pageSize(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          this.items.set(page.items);
          this.total.set(page.total);
          this.expanded.set(new Set());
          this.error.set(null);
          this.loading.set(false);
        },
        error: (e: unknown) => {
          this.error.set(messageOf(problemOf(e)));
          this.loading.set(false);
        },
      });
  }
}
