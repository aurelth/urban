import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { provideUrbanIcons } from '../core/theme/icons';
import { fieldValueOf, menuSelectOptions, setFieldValue } from './menu-select';

export function configureDialogTest(): void {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([{ path: '**', children: [] }]),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
}

export function dialogOf(tag: string): HTMLElement {
  return document.querySelector<HTMLElement>(tag)!;
}

export function fieldOf(tag: string, name: string): HTMLInputElement {
  return dialogOf(tag).querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
}

export function typeIn(tag: string, name: string, value: string): void {
  const field = fieldOf(tag, name);
  field.value = value;
  field.dispatchEvent(new Event('input'));
}

export function chooseIn(tag: string, name: string, value: string): void {
  setFieldValue(fieldOf(tag, name), value);
}

/** Valor escolhido numa lista (`urban-menu-select`) do diálogo. */
export function choiceOf(tag: string, name: string): string {
  return fieldValueOf(fieldOf(tag, name));
}

/** Opções (valor, texto e marca) da lista do diálogo; abre o menu e o fecha. */
export function choicesOf(
  tag: string,
  name: string,
): { value: string; label: string; checked: boolean }[] {
  return menuSelectOptions(fieldOf(tag, name));
}

/** A caixa de marcar (`mat-checkbox`) do diálogo que leva o `data-testid` dado. */
export function checkboxOf(tag: string, testId: string): HTMLInputElement {
  return dialogOf(tag).querySelector<HTMLInputElement>(
    `[data-testid="${testId}"] input[type="checkbox"]`,
  )!;
}

/** Marca ou desmarca a caixa, como um clique da pessoa. */
export function toggleIn(tag: string, testId: string): void {
  checkboxOf(tag, testId).click();
}

export function submitDialog(tag: string): void {
  dialogOf(tag).querySelector('form')!.dispatchEvent(new Event('submit'));
}

export function submitButtonOf(tag: string): HTMLButtonElement {
  return dialogOf(tag).querySelector<HTMLButtonElement>('button[type="submit"]')!;
}

export function alertOf(tag: string): string {
  return dialogOf(tag).querySelector('[role="alert"]')?.textContent?.trim() ?? '';
}

export function errorsOf(tag: string): string[] {
  return [...dialogOf(tag).querySelectorAll('mat-error')].map((error) => error.textContent!.trim());
}

export function removeOverlays(): void {
  document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
}
