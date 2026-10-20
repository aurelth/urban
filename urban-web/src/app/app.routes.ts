import { Routes } from '@angular/router';
import { ShellLayout } from './core/layout/shell.layout';

export const routes: Routes = [
  {
    path: '',
    component: ShellLayout,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Painel | Urban',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
