import { Component, inject } from '@angular/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { SessionStore } from '../../core/auth/session.store';
import { FadeInDirective } from '../../shared/animations/fade-in.directive';

@Component({
  selector: 'urban-dashboard-page',
  imports: [MatCard, MatCardHeader, MatCardTitle, MatCardContent, MatIcon, FadeInDirective],
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  protected readonly session = inject(SessionStore);

  protected readonly modules = [
    { title: 'Moradores', description: 'Cadastro, unidades e histórico.' },
    { title: 'Financeiro', description: 'Despesas, cobranças e inadimplência.' },
    { title: 'Manutenção', description: 'Agenda e histórico de serviços.' },
  ];
}
