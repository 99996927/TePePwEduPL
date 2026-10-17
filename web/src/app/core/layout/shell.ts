import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { problemOf } from '../api-problem';
import { Notifier } from '../notifier';
import { APPROVER_ROLES, HR_ROLES } from '../roles';
import { SessionService } from '../session';

interface MenuItem {
  label: string;
  icon: string;
  path: string;
  exact: boolean;
  approversOnly: boolean;
  hrOnly: boolean;
}

const ROLE_LABELS: Readonly<Record<string, string>> = {
  Employee: 'Pracownik',
  Manager: 'Kierownik',
  HR: 'Kadry',
  HRApprover: 'Zatwierdzający kadr',
};

const APP_NAME = 'Asystent kadrowo-płacowy';

const MENU: readonly MenuItem[] = [
  { label: 'Pulpit', icon: 'dashboard', path: '/', exact: true, approversOnly: false, hrOnly: false },
  { label: 'Urlopy', icon: 'beach_access', path: '/urlopy', exact: true, approversOnly: false, hrOnly: false },
  { label: 'Czas pracy', icon: 'schedule', path: '/czas-pracy', exact: false, approversOnly: false, hrOnly: false },
  { label: 'Naliczenia', icon: 'payments', path: '/naliczenia', exact: false, approversOnly: false, hrOnly: false },
  { label: 'Agent', icon: 'smart_toy', path: '/agent', exact: false, approversOnly: false, hrOnly: false },
  { label: 'Zatwierdzanie urlopów', icon: 'task_alt', path: '/urlopy/zatwierdzanie', exact: false, approversOnly: true, hrOnly: false },
  { label: 'Przegląd zespołu', icon: 'groups', path: '/zespol', exact: false, approversOnly: true, hrOnly: false },
  { label: 'Zmiany płacowe', icon: 'request_quote', path: '/zmiany-platowe', exact: false, approversOnly: false, hrOnly: true },
  { label: 'Szkice korekt', icon: 'edit_note', path: '/szkice-korekt', exact: false, approversOnly: false, hrOnly: true },
  { label: 'Audyt', icon: 'fact_check', path: '/audyt', exact: false, approversOnly: false, hrOnly: true },
];

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);

  protected readonly user = this.session.user;
  protected readonly narrow = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 800px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );
  protected readonly roleLabel = computed(() => this.user()?.roles.map((r) => ROLE_LABELS[r] ?? r).join(', ') ?? '');
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );
  protected readonly title = computed(() => {
    const path = this.url().split(/[?#]/)[0];
    const matching = MENU.filter((item) =>
      item.path === '/' ? path === '/' : path === item.path || path.startsWith(`${item.path}/`),
    );
    const best = matching.reduce<MenuItem | null>(
      (acc, item) => (acc === null || item.path.length > acc.path.length ? item : acc),
      null,
    );
    return best?.label ?? APP_NAME;
  });
  protected readonly appName = APP_NAME;
  protected readonly items = computed(() =>
    MENU.filter(
      (item) =>
        (!item.approversOnly || this.session.hasRole(...APPROVER_ROLES)) && (!item.hrOnly || this.session.hasRole(...HR_ROLES)),
    ),
  );

  protected closeIfNarrow(sidenav: MatSidenav): void {
    if (this.narrow()) {
      void sidenav.close();
    }
  }

  protected async logout(): Promise<void> {
    try {
      await this.session.logout();
    } catch (error) {
      const problem = problemOf(error);
      if (problem.status !== 401) {
        this.notifier.error(problem);
      }
    }
    await this.router.navigateByUrl('/logowanie');
  }
}
