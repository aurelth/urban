import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MyMonthStore } from './my-month.store';

/** A fatura do morador (esqueleto da fatia de testes; a tela vem com a implementação). */
@Component({
  selector: 'urban-my-month-page',
  template: '<section class="mx-auto max-w-5xl p-6"><h1>Minha fatura</h1></section>',
  providers: [MyMonthStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyMonthPage implements OnInit {
  protected readonly store = inject(MyMonthStore);

  ngOnInit(): void {
    void this.store.show('');
  }
}
