import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { problemOf } from '../api-problem';
import { Notifier } from '../notifier';
import { APPROVER_ROLES, HR_ROLES } from '../roles';
import { SessionService } from '../session';

interface MenuItem {
  label: string;
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

const MENU: readonly MenuItem[] = [
  { label: 'Pulpit', path: '/', exact: true, approversOnly: false, hrOnly: false },
  { label: 'Urlopy', path: '/urlopy', exact: true, approversOnly: false, hrOnly: false },
  { label: 'Czas pracy', path: '/czas-pracy', exact: false, approversOnly: false, hrOnly: false },
  { label: 'Naliczenia', path: '/naliczenia', exact: false, approversOnly: false, hrOnly: false },
  { label: 'Agent', path: '/agent', exact: false, approversOnly: false, hrOnly: false },
  { label: 'Zatwierdzanie urlopów', path: '/urlopy/zatwierdzanie', exact: false, approversOnly: true, hrOnly: false },
  { label: 'Przegląd zespołu', path: '/zespol', exact: false, approversOnly: true, hrOnly: false },
  { label: 'Zmiany płacowe', path: '/zmiany-platowe', exact: false, approversOnly: false, hrOnly: true },
  { label: 'Szkice korekt', path: '/szkice-korekt', exact: false, approversOnly: false, hrOnly: true },
  { label: 'Audyt', path: '/audyt', exact: false, approversOnly: false, hrOnly: true },
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
