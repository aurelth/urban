import { Routes } from '@angular/router';
import { adminGuard, adminHomeGuard } from './core/auth/admin.guards';
import { authGuard, guestGuard } from './core/auth/auth.guards';
import {
  condominiumGuard,
  condominiumSetupGuard,
  managerGuard,
} from './core/auth/condominium.guards';
import { AuthLayout } from './core/layout/auth.layout';

export const routes: Routes = [
  // A área logada vem primeiro: "/" precisa cair nela (e no redirecionamento para o painel),
  // senão o layout da entrada, que também tem caminho vazio, "casaria" com a raiz e ficaria em branco.
  {
    path: '',
    // A moldura (menu lateral, cabeçalho, menu do avatar) só é baixada por quem já passou pela guarda: a tela de
    // entrada não carrega o Material Sidenav nem o Material Menu.
    loadComponent: () => import('./core/layout/shell.layout').then((m) => m.ShellLayout),
    canActivate: [authGuard],
    children: [
      {
        path: 'condominio/cadastrar',
        title: 'Cadastrar condomínio',
        canActivate: [condominiumSetupGuard],
        loadComponent: () =>
          import('./features/condominium/register/register-condominium.page').then(
            (m) => m.RegisterCondominiumPage,
          ),
      },
      // E-mail e senha são de qualquer pessoa logada, com ou sem condomínio cadastrado.
      {
        path: 'conta',
        title: 'Minha conta',
        loadComponent: () => import('./features/account/account.page').then((m) => m.AccountPage),
      },
      // O resto da área logada exige o condomínio cadastrado: sem ele, a guarda leva ao cadastro.
      {
        path: '',
        canActivateChild: [condominiumGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
          {
            path: 'admin/visao-geral',
            title: 'Visão geral',
            canActivate: [adminGuard],
            loadComponent: () =>
              import('./features/admin/overview/overview.page').then((m) => m.OverviewPage),
          },
          {
            path: 'admin/sindicos',
            title: 'Síndicos',
            canActivate: [adminGuard],
            loadComponent: () =>
              import('./features/admin/managers/managers.page').then((m) => m.ManagersPage),
          },
          {
            path: 'admin/pedidos-de-cadastro',
            title: 'Pedidos de cadastro',
            canActivate: [adminGuard],
            loadComponent: () =>
              import('./features/admin/registration-requests/registration-requests.page').then(
                (m) => m.RegistrationRequestsPage,
              ),
          },
          {
            path: 'admin/mensagens',
            title: 'Mensagens',
            canActivate: [adminGuard],
            loadComponent: () =>
              import('./features/admin/contact-messages/contact-messages.page').then(
                (m) => m.ContactMessagesPage,
              ),
          },
          {
            path: 'contato',
            title: 'Contato',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/contact/contact.page').then((m) => m.ContactPage),
          },
          {
            path: 'condominio/meus-dados',
            title: 'Meus dados',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/condominium/profile/profile.page').then((m) => m.ProfilePage),
          },
          {
            path: 'unidades',
            title: 'Unidades',
            canActivate: [managerGuard],
            loadComponent: () => import('./features/units/units.page').then((m) => m.UnitsPage),
          },
          {
            path: 'unidades/lote',
            title: 'Cadastro em lote',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/units/units-batch.page').then((m) => m.UnitsBatchPage),
          },
          {
            path: 'moradores',
            title: 'Moradores',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/moradores/residents.page').then((m) => m.ResidentsPage),
          },
          {
            path: 'moradores/novo',
            title: 'Novo morador',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/moradores/resident-form.page').then((m) => m.ResidentFormPage),
          },
          {
            path: 'moradores/:id/editar',
            title: 'Editar morador',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/moradores/resident-form.page').then((m) => m.ResidentFormPage),
          },
          {
            path: 'veiculos',
            title: 'Veículos',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/vehicles/vehicles.page').then((m) => m.VehiclesPage),
          },
          { path: 'financeiro', pathMatch: 'full', redirectTo: 'financeiro/despesas' },
          {
            path: 'financeiro/despesas',
            title: 'Despesas',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/finance/expenses.page').then((m) => m.ExpensesPage),
          },
          {
            path: 'financeiro/tarifas',
            title: 'Tarifas de consumo',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/finance/tariffs.page').then((m) => m.TariffsPage),
          },
          {
            path: 'financeiro/leituras',
            title: 'Leituras do mês',
            canActivate: [managerGuard],
            loadComponent: () =>
              import('./features/finance/readings.page').then((m) => m.ReadingsPage),
          },
          {
            path: 'financeiro/lancamentos',
            title: 'Lançamentos do mês',
            canActivate: [managerGuard],
            loadComponent: () => import('./features/finance/month.page').then((m) => m.MonthPage),
          },
          {
            path: 'dashboard',
            title: 'Painel',
            canActivate: [adminHomeGuard],
            loadChildren: () =>
              import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
          },
        ],
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
        path: 'confirmar-troca-de-email',
        title: 'Confirmar novo e-mail',
        loadComponent: () =>
          import('./features/auth/confirm-email-change/confirm-email-change.page').then(
            (m) => m.ConfirmEmailChangePage,
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
