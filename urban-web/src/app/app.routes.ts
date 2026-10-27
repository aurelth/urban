import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';
import { AuthLayout } from './core/layout/auth.layout';
import { ShellLayout } from './core/layout/shell.layout';

export const routes: Routes = [
  // A área logada vem primeiro: "/" precisa cair nela (e no redirecionamento para o painel),
  // senão o layout da entrada, que também tem caminho vazio, "casaria" com a raiz e ficaria em branco.
  {
    path: '',
    component: ShellLayout,
    canActivate: [authGuard],
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
  {
    path: '',
    component: AuthLayout,
    children: [
      {
        path: 'entrar',
        title: 'Entrar | Urban',
        canActivate: [guestGuard],
        loadComponent: () => import('./features/auth/login/login.page').then((m) => m.LoginPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
