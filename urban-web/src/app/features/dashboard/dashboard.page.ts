import { Component } from '@angular/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { FadeInDirective } from '../../shared/animations/fade-in.directive';

@Component({
  selector: 'urban-dashboard-page',
  imports: [MatCard, MatCardHeader, MatCardTitle, MatCardContent, FadeInDirective],
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  protected readonly modules = [
    { title: 'Moradores', description: 'Cadastro, unidades e histórico.' },
    { title: 'Financeiro', description: 'Despesas, cobranças e inadimplência.' },
    { title: 'Manutenção', description: 'Agenda e histórico de serviços.' },
  ];
}
