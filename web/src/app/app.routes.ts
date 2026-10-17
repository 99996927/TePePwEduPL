import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { APPROVER_ROLES } from './core/roles';

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
    ],
  },
  { path: '**', redirectTo: '' },
];
