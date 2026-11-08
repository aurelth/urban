import {
  ChangeDetectionStrategy,
  Component,
  DoCheck,
  ElementRef,
  OnDestroy,
  computed,
  inject,
  input,
  model,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import {
  ControlValueAccessor,
  FormGroupDirective,
  NgControl,
  NgForm,
  Validators,
} from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatFormField, MatFormFieldControl } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { Subject } from 'rxjs';

export interface MenuOption {
  readonly value: string;
  readonly label: string;
  /** Ícone do Material Symbols mostrado antes do texto, no botão e no menu. */
  readonly icon?: string;
  /** Classes que pintam o texto e o ícone da opção escolhida, no botão. */
  readonly tone?: string;
  /** Classes que pintam o item no menu (variáveis `--mat-menu-item-*`). */
  readonly menuTone?: string;
}

let nextId = 0;

/**
 * Lista suspensa do Urban, com o visual do menu de situação: texto (e ícone) na cor da opção, seta cinza que vira
 * índigo com o menu aberto e itens do menu do Material. Serve a formulários reativos (`formControlName`) e a
 * telas sem formulário (`value` e `valueChange`). Dentro de um `mat-form-field` é o controle dele, com rótulo
 * flutuante, dica e erro; fora, desenha a própria borda.
 */
@Component({
  selector: 'urban-menu-select',
  imports: [MatIcon, MatMenu, MatMenuItem, MatMenuTrigger],
  templateUrl: './menu-select.html',
  providers: [{ provide: MatFormFieldControl, useExisting: MenuSelect }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block min-w-0' },
})
export class MenuSelect
  implements MatFormFieldControl<string>, ControlValueAccessor, DoCheck, OnDestroy
{
  readonly options = input.required<readonly MenuOption[]>();
  readonly value = model('');
  readonly ariaLabel = input<string | null>(null);

  readonly id = `urban-menu-select-${nextId++}`;
  readonly controlType = 'urban-menu-select';
  readonly stateChanges = new Subject<void>();
  readonly ngControl = inject(NgControl, { optional: true, self: true });

  protected readonly framed = inject(MatFormField, { optional: true }) === null;
  protected readonly selected = computed(() =>
    this.options().find((option) => option.value === this.value()),
  );

  readonly focused = signal(false);
  readonly empty = computed(() => this.selected() === undefined);
  readonly shouldLabelFloat = signal(true);
  readonly errorState = signal(false);
  readonly disabled = computed(() => this.controlDisabled());
  readonly userAriaDescribedBy = signal('');

  private readonly controlDisabled = signal(false);
  private readonly trigger = viewChild.required(MatMenuTrigger);
  private readonly button = viewChild.required<ElementRef<HTMLButtonElement>>('button');
  private readonly menuItems = viewChildren(MatMenuItem);
  private readonly errorMatcher = inject(ErrorStateMatcher);
  private readonly parentForm =
    inject(FormGroupDirective, { optional: true }) ?? inject(NgForm, { optional: true });
  private focusTimer: ReturnType<typeof setTimeout> | null = null;
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor() {
    if (this.ngControl !== null) {
      this.ngControl.valueAccessor = this;
    }
  }

  get required(): boolean {
    return this.ngControl?.control?.hasValidator(Validators.required) ?? false;
  }

  ngDoCheck(): void {
    const state = this.errorMatcher.isErrorState(this.ngControl?.control ?? null, this.parentForm);
    if (state !== this.errorState()) {
      this.errorState.set(state);
    }
    this.stateChanges.next();
  }

  ngOnDestroy(): void {
    this.cancelFocus();
    this.stateChanges.complete();
  }

  setDescribedByIds(ids: string[]): void {
    this.userAriaDescribedBy.set(ids.join(' '));
  }

  onContainerClick(): void {
    if (!this.trigger().menuOpen) {
      this.button().nativeElement.focus();
    }
  }

  writeValue(value: string | null): void {
    this.value.set(value ?? '');
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.controlDisabled.set(disabled);
  }

  protected choose(option: MenuOption): void {
    if (option.value !== this.value()) {
      this.value.set(option.value);
      this.onChange(option.value);
    }
  }

  protected opened(): void {
    this.focused.set(true);
    this.focusTimer = setTimeout(() => this.focusChosen());
  }

  protected closed(): void {
    this.cancelFocus();
    this.focused.set(false);
    this.onTouched();
  }

  private cancelFocus(): void {
    if (this.focusTimer !== null) {
      clearTimeout(this.focusTimer);
      this.focusTimer = null;
    }
  }

  private focusChosen(): void {
    this.focusTimer = null;
    if (!this.trigger().menuOpen) {
      return;
    }
    const index = this.options().findIndex((option) => option.value === this.value());
    if (index >= 0) {
      this.menuItems()[index]?.focus('program');
    }
  }
}
