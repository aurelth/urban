import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlContainer, ReactiveFormsModule } from '@angular/forms';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { USE_PARENT_FORM } from '../../shared/ui/form-field.base';
import { TextField } from '../../shared/ui/text-field/text.field';
import { BandArray, BandGroup, MAX_BANDS, addBand, removeBand } from './tariff.form';

/**
 * Faixas de uma tarifa (a da água ou a do esgoto): uma linha por faixa, com o limite de cima, o valor fixo e o preço por m³.
 * Liga-se à lista de faixas de nome `name` do formulário em que for colocado (veja `bandArray`); a última faixa não tem limite.
 */
@Component({
  selector: 'urban-bands-editor',
  imports: [ReactiveFormsModule, MatButton, MatIconButton, MatIcon, TextField],
  templateUrl: './bands.editor.html',
  viewProviders: [USE_PARENT_FORM],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BandsEditor implements OnInit {
  readonly name = input.required<'water' | 'sewage'>();
  readonly title = input.required<string>();

  private readonly container = inject(ControlContainer);
  private readonly destroyRef = inject(DestroyRef);
  private array!: BandArray;

  protected readonly rows = signal<readonly BandGroup[]>([]);
  protected readonly orderError = signal(false);
  protected readonly maxBands = MAX_BANDS;

  protected readonly limitErrors = {
    decimal: 'Informe o limite em m³, maior que zero, com até 3 casas',
  };
  protected readonly fixedErrors = {
    decimal: 'Informe o valor fixo, com até 2 casas (use 0 se não houver)',
  };
  protected readonly priceErrors = {
    decimal: 'Informe o preço por m³, com até 4 casas (use 0 se não houver)',
  };

  ngOnInit(): void {
    this.array = this.container.control!.get(this.name()) as BandArray;
    this.sync();
    this.array.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.sync();
    });
  }

  protected add(): void {
    addBand(this.array);
  }

  protected remove(index: number): void {
    removeBand(this.array, index);
  }

  /** Atualiza o que a tela mostra: as linhas (quando mudam de quantidade ou são trocadas) e o aviso de limites fora de ordem. */
  private sync(): void {
    const current = this.rows();
    const next = this.array.controls;
    if (next.length !== current.length || next.some((row, index) => row !== current[index])) {
      this.rows.set([...next]);
    }
    this.orderError.set(this.array.hasError('ascending') && this.array.touched);
  }
}
