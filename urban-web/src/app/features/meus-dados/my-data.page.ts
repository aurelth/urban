import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MyDataStore } from './my-data.store';

/** "Meus dados" do morador: o próprio cadastro, só para ler. */
@Component({
  selector: 'urban-my-data-page',
  template: '',
  providers: [MyDataStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyDataPage implements OnInit {
  protected readonly store = inject(MyDataStore);

  ngOnInit(): void {
    void this.store.load();
  }
}
