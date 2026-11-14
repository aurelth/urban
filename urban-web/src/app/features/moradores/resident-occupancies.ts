import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { OccupancyKind, ResidentOccupancy } from '../../core/residents/residents.models';
import { focusFirstInvalidInput } from '../../shared/forms/focus-first-invalid';
import { FormSubmit } from '../../shared/ui/form-submit/form-submit';
import { SelectField, SelectOption } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { unitLabel } from '../units/unit-values';
import { entryDate, exitDate } from './resident-field.validators';
import { RESIDENT_FORM_MESSAGES } from './resident-form.messages';
import { ResidentFormStore } from './resident-form.store';
import { HOUSING_ERRORS, KIND_OPTIONS } from './resident-housing.fields';
import { toEndPayload, toOccupancyPayload } from './resident-occupancy.payload';
import { formatDay, kindLabel, occupancyPeriod, occupancyUnit } from './resident-values';

/** Recusas que dizem "a tela está desatualizada": a lista de vínculos é lida de novo. */
const STALE_CODES: ReadonlySet<string> = new Set([
  'condominium.occupancy.already_active',
  'condominium.occupancy.already_ended',
  'condominium.occupancy.not_found',
]);

/**
 * Os vínculos do morador com as unidades, na tela de edição: a lista (em andamento e já encerrados), "Encerrar vínculo"
 * (a data de saída, que o vínculo guarda no histórico) e "Adicionar unidade" (outra unidade ou a volta a uma que ele já
 * deixou). Um formulário aberto por vez. Usa o `ResidentFormStore` que a página fornece: as chamadas de vínculo trocam só
 * os vínculos do morador lido, então não mexem no formulário dos dados pessoais nem na versão dele.
 */
@Component({
  selector: 'urban-resident-occupancies',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    TextField,
    SelectField,
    FormSubmit,
  ],
  templateUrl: './resident-occupancies.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResidentOccupancies {
  protected readonly store = inject(ResidentFormStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly builder = inject(NonNullableFormBuilder);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly endForm = this.builder.group({
    endDate: ['', [Validators.required]],
  });
  protected readonly addForm = this.builder.group({
    unitId: ['', [Validators.required]],
    kind: this.builder.control<OccupancyKind | ''>('', [Validators.required]),
    startDate: ['', [Validators.required, entryDate()]],
  });

  protected readonly endingId = signal<string | null>(null);
  protected readonly adding = signal(false);
  /** Aviso de sucesso do último vínculo gravado. */
  protected readonly notice = signal<string | null>(null);
  /** Recusa da API, mostrada no fim do formulário aberto. */
  protected readonly error = signal<string | null>(null);
  /** Recusa que deixou o formulário sem sentido (o vínculo já tinha terminado): fica no alto do cartão. */
  protected readonly listError = signal<string | null>(null);

  protected readonly occupancies = computed(() => this.store.resident()?.occupancies ?? []);
  protected readonly endingOccupancy = computed(
    () => this.occupancies().find((occupancy) => occupancy.id === this.endingId()) ?? null,
  );
  protected readonly endingOpen = computed(() => this.endingOccupancy()?.endDate === null);

  /** As unidades ativas onde o morador não mora hoje: onde ele já saiu, pode voltar. */
  protected readonly unitOptions = computed<readonly SelectOption[]>(() => {
    const occupied = new Set(
      this.occupancies()
        .filter((occupancy) => occupancy.endDate === null)
        .map((occupancy) => occupancy.unitId),
    );
    return this.store
      .units()
      .filter((unit) => !occupied.has(unit.id))
      .map((unit) => ({ value: unit.id, label: unitLabel(unit) }));
  });

  protected readonly endDateErrors = computed(() => ({
    required: 'Informe a data de saída',
    date: 'Informe uma data válida, como 31/01/2026',
    beforeStart: `A saída não pode ser antes da entrada, em ${formatDay(this.endingOccupancy()?.startDate ?? '')}`,
    future: 'A saída não pode ser no futuro',
  }));
  protected readonly unitErrors = HOUSING_ERRORS.unit;
  protected readonly kindErrors = HOUSING_ERRORS.kind;
  protected readonly startDateErrors = HOUSING_ERRORS.startDate;
  protected readonly kindOptions = KIND_OPTIONS;

  protected readonly unitName = occupancyUnit;
  protected readonly kindName = kindLabel;
  protected readonly period = occupancyPeriod;

  protected startEnding(occupancy: ResidentOccupancy): void {
    this.closeForms();
    this.endForm.reset();
    this.endForm.controls.endDate.setValidators([
      Validators.required,
      exitDate(occupancy.startDate),
    ]);
    this.endForm.controls.endDate.updateValueAndValidity({ emitEvent: false });
    this.endingId.set(occupancy.id);
  }

  protected async startAdding(): Promise<void> {
    this.closeForms();
    this.addForm.reset();
    this.adding.set(true);
    await this.store.loadUnits();
  }

  protected retryUnits(): void {
    void this.store.loadUnits();
  }

  protected closeForms(): void {
    this.endingId.set(null);
    this.adding.set(false);
    this.error.set(null);
    this.listError.set(null);
    this.notice.set(null);
  }

  protected async confirmEnd(): Promise<void> {
    const occupancy = this.endingOccupancy();
    const resident = this.store.resident();
    if (occupancy === null || resident === null || this.store.occupancyBusy()) {
      return;
    }
    if (this.endForm.invalid) {
      this.showInvalid(this.endForm);
      return;
    }

    this.error.set(null);
    const problem = await this.store.endOccupancy(
      resident.id,
      occupancy.id,
      toEndPayload(this.endForm.controls.endDate.value),
    );
    if (problem === null) {
      this.closeForms();
      this.notice.set(`Saída de ${occupancyUnit(occupancy)} registrada`);
      return;
    }
    await this.explain(problem, resident.id);
    if (!this.endingOpen()) {
      // O vínculo já tinha terminado em outro lugar: o formulário some, então a recusa passa para o alto do cartão.
      this.listError.set(this.error());
      this.error.set(null);
      this.endingId.set(null);
    }
  }

  protected async confirmAdd(): Promise<void> {
    const resident = this.store.resident();
    if (resident === null || this.store.occupancyBusy()) {
      return;
    }
    if (this.addForm.invalid) {
      this.showInvalid(this.addForm);
      return;
    }

    this.error.set(null);
    const value = this.addForm.getRawValue();
    const unit = this.store.units().find((candidate) => candidate.id === value.unitId);
    const problem = await this.store.addOccupancy(resident.id, toOccupancyPayload(value));
    if (problem === null) {
      this.closeForms();
      this.notice.set(
        `Vínculo com ${unit === undefined ? 'a unidade' : unitLabel(unit)} adicionado`,
      );
      return;
    }
    await this.explain(problem, resident.id);
  }

  private showInvalid(form: { markAllAsTouched(): void }): void {
    form.markAllAsTouched();
    // O foco procura a marca de campo inválido, que só chega ao HTML depois de a tela se atualizar.
    this.changeDetector.detectChanges();
    focusFirstInvalidInput(this.host.nativeElement);
  }

  /** Mostra o motivo da recusa e, quando a tela estava desatualizada, lê os vínculos de novo. */
  private async explain(problem: ApiProblem, residentId: string): Promise<void> {
    if (this.store.status() !== 'not-found') {
      this.error.set(describeProblem(problem, RESIDENT_FORM_MESSAGES));
    }
    if (problem.code !== null && STALE_CODES.has(problem.code)) {
      await this.store.refreshOccupancies(residentId);
    }
  }
}
