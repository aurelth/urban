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
        title: 'Painel',
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
        title: 'Entrar',
        canActivate: [guestGuard],
        loadComponent: () => import('./features/auth/login/login.page').then((m) => m.LoginPage),
      },
      {
        path: 'cadastrar',
        title: 'Criar conta',
        canActivate: [guestGuard],
        loadComponent: () =>
          import('./features/auth/register/register.page').then((m) => m.RegisterPage),
      },
      {
        path: 'esqueci-a-senha',
        title: 'Esqueci a senha',
        canActivate: [guestGuard],
        loadComponent: () =>
          import('./features/auth/forgot-password/forgot-password.page').then(
            (m) => m.ForgotPasswordPage,
          ),
      },
      {
        path: 'redefinir-senha',
        title: 'Redefinir senha',
        loadComponent: () =>
          import('./features/auth/reset-password/reset-password.page').then(
            (m) => m.ResetPasswordPage,
          ),
      },
      {
        path: 'confirmar-email',
        title: 'Confirmar e-mail',
        loadComponent: () =>
          import('./features/auth/confirm-email/confirm-email.page').then(
            (m) => m.ConfirmEmailPage,
          ),
      },
    ],
  },
  {
    path: 'termos',
    title: 'Termos de uso',
    data: { document: 'terms' },
    loadComponent: () => import('./features/legal/legal.page').then((m) => m.LegalPage),
  },
  {
    path: 'privacidade',
    title: 'Política de privacidade',
    data: { document: 'privacy' },
    loadComponent: () => import('./features/legal/legal.page').then((m) => m.LegalPage),
  },
  { path: '**', redirectTo: '' },
];
