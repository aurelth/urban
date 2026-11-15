import { Component, ElementRef, input, signal, viewChild } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatIconButton } from '@angular/material/button';
import { MatError, MatFormField, MatHint, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MaskDirective } from '../../directives/mask.directive';
import { dateFromText, textFromDate } from '../../forms/date-text';
import { MaskKind } from '../../forms/masks';
import { CalendarPopup } from '../calendar-popup/calendar.popup';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';

const FALLBACK_ERROR = 'Confira este campo';

/**
 * Campo de texto dos formulários de cadastro. Liga-se ao controle de nome `name` do formulário (ou
 * do grupo) em que for colocado. O texto de cada erro vem de quem usa: `errors` mapeia a chave do
 * erro do controle para a mensagem, e vale a primeira chave, na ordem do mapa, que o controle tiver.
 */
@Component({
  selector: 'urban-text-field',
  imports: [
    ReactiveFormsModule,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatInput,
    MatIconButton,
    MatIcon,
    MatSuffix,
    MaskDirective,
    CalendarPopup,
  ],
  templateUrl: './text.field.html',
  viewProviders: [USE_PARENT_FORM],
  // Sem dica, a área do erro não reserva espaço (`subscriptSizing="dynamic"` no template): o espaço de baixo é do campo.
  host: { class: 'relative block', '[class.mb-5]': '!hint()', '[class.mb-3]': 'hint()' },
})
export class TextField extends FormFieldBase {
  readonly name = input.required<string>();
  readonly label = input.required<string>();
  readonly hint = input<string | null>(null);
  readonly mask = input<MaskKind | null>(null);
  readonly autocomplete = input('off');
  /** Teclado do celular nos campos sem máscara (a máscara escolhe o seu). */
  readonly inputmode = input<string | null>(null);
  readonly optional = input(false);
  readonly errors = input<Readonly<Record<string, string>>>({});
  /** Só vale com `mask="date"`: o calendário abre na lista de anos, para quem nasceu há décadas. */
  readonly birthDate = input(false);

  protected readonly calendarRequested = signal(false);
  private readonly calendarButton = viewChild('calendarButton', { read: ElementRef });

  protected calendarValue(): Date | null {
    return dateFromText(String(this.control?.value ?? ''));
  }

  protected openCalendar(): void {
    this.calendarRequested.set(true);
  }

  protected pickDate(date: Date): void {
    this.control?.setValue(textFromDate(date));
    this.control?.markAsDirty();
    this.control?.markAsTouched();
  }

  protected closeCalendar(): void {
    this.calendarRequested.set(false);
    this.calendarButton()?.nativeElement.focus();
  }

  protected get controlName(): string {
    return this.name();
  }

  protected errorMessage(): string {
    const messages = this.errors();
    const key = Object.keys(messages).find((candidate) => this.control?.hasError(candidate));
    return key === undefined ? FALLBACK_ERROR : messages[key];
  }
}
