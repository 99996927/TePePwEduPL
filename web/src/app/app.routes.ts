import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { APPROVER_ROLES, HR_ROLES } from './core/roles';

export const routes: Routes = [
  {
    path: 'logowanie',
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: '',
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'urlopy',
        pathMatch: 'full',
        loadComponent: () => import('./features/leave/leave-list').then((m) => m.LeaveList),
      },
      {
        path: 'urlopy/zatwierdzanie',
        canActivate: [roleGuard(...APPROVER_ROLES)],
        loadComponent: () => import('./features/leave/leave-approvals').then((m) => m.LeaveApprovals),
      },
      {
        path: 'czas-pracy',
        loadComponent: () => import('./features/timesheet/timesheet').then((m) => m.Timesheet),
      },
      {
        path: 'zespol',
        canActivate: [roleGuard(...APPROVER_ROLES)],
        loadComponent: () => import('./features/team/team-overview').then((m) => m.TeamOverview),
      },
      {
        path: 'naliczenia',
        loadComponent: () => import('./features/payroll/payroll').then((m) => m.Payroll),
      },
      {
        path: 'zmiany-platowe',
        canActivate: [roleGuard(...HR_ROLES)],
        loadComponent: () => import('./features/payroll-changes/payroll-changes').then((m) => m.PayrollChanges),
      },
      {
        path: 'szkice-korekt',
        canActivate: [roleGuard(...HR_ROLES)],
        loadComponent: () => import('./features/correction-drafts/correction-drafts').then((m) => m.CorrectionDrafts),
      },
      {
        path: 'audyt',
        canActivate: [roleGuard(...HR_ROLES)],
        loadComponent: () => import('./features/audit/audit').then((m) => m.Audit),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
