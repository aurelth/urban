import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideUrbanIcons } from '../core/theme/icons';

export function configureDialogTest(): void {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
}

export function dialogOf(tag: string): HTMLElement {
  return document.querySelector<HTMLElement>(tag)!;
}

export function fieldOf(tag: string, name: string): HTMLInputElement & HTMLSelectElement {
  return dialogOf(tag).querySelector<HTMLInputElement & HTMLSelectElement>(
    `[formControlName="${name}"]`,
  )!;
}

export function typeIn(tag: string, name: string, value: string): void {
  const field = fieldOf(tag, name);
  field.value = value;
  field.dispatchEvent(new Event('input'));
}

export function chooseIn(tag: string, name: string, value: string): void {
  const field = fieldOf(tag, name);
  field.value = value;
  field.dispatchEvent(new Event('change'));
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
