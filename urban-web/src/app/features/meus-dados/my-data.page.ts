import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import {
  formatDay,
  formatPhone,
  kindLabel,
  occupancyPeriod,
  occupancyUnit,
} from '../moradores/resident-values';
import { MyDataStore } from './my-data.store';

/**
 * "Meus dados" do morador: o próprio cadastro, só para ler (dados pessoais, contato de emergência e as unidades onde mora
 * ou já morou). Não há campo para editar: a correção é com o síndico, que vê os mesmos dados em "Moradores".
 */
@Component({
  selector: 'urban-my-data-page',
  imports: [MatButton, MatCard, MatCardContent, MatIcon],
  templateUrl: './my-data.page.html',
  providers: [MyDataStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyDataPage implements OnInit {
  protected readonly store = inject(MyDataStore);

  protected readonly day = formatDay;
  protected readonly phone = formatPhone;
  protected readonly unitName = occupancyUnit;
  protected readonly kindName = kindLabel;
  protected readonly period = occupancyPeriod;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    await this.store.load();
  }
}
