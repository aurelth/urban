import { Component } from '@angular/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { FadeInDirective } from '../../shared/animations/fade-in.directive';

@Component({
  selector: 'urban-dashboard-page',
  imports: [MatCard, MatCardHeader, MatCardTitle, MatCardContent, FadeInDirective],
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  protected readonly modulos = [
    { titulo: 'Moradores', descricao: 'Cadastro, unidades e historico.' },
    { titulo: 'Financeiro', descricao: 'Despesas, cobrancas e inadimplencia.' },
    { titulo: 'Manutencao', descricao: 'Agenda e historico de servicos.' },
  ];
}
