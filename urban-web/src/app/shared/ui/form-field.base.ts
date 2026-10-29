import { ChangeDetectorRef, DestroyRef, Directive, OnInit, Provider, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, ControlContainer, FormGroupDirective } from '@angular/forms';
import { EMPTY, merge } from 'rxjs';

/** Faz o campo usar o formulário do componente que o contém, em vez de criar um próprio. */
export const USE_PARENT_FORM: Provider = {
  provide: ControlContainer,
  useFactory: () => inject(ControlContainer, { skipSelf: true }),
};

/**
 * Base dos campos que se ligam a um controle do formulário (ou do grupo) em que são colocados. Sendo OnPush, o
 * campo só se atualiza quando avisado; o Material decide mostrar o erro quando o formulário é
 * enviado ou o controle muda, e nenhum dos dois avisaria o campo sozinho.
 */
@Directive()
export abstract class FormFieldBase implements OnInit {
  protected abstract readonly controlName: string;
  protected control: AbstractControl | null = null;

  private readonly form = inject(FormGroupDirective);
  private readonly container = inject(ControlContainer);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.control = this.container.control?.get(this.controlName) ?? null;
    merge(this.form.ngSubmit, this.control?.events ?? EMPTY)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.changeDetector.markForCheck());
  }
}
