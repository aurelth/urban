import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideUrbanIcons } from '../core/theme/icons';
import { flushPromises } from './async';

export interface Harness<T> {
  fixture: ComponentFixture<T>;
  http: HttpTestingController;
  html: HTMLElement;
}

/** Monta o componente com rotas, HTTP simulado e os ícones do tema, como a aplicação o monta. */
export function setupComponent<T>(component: Type<T>): Harness<T> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
    ],
  });
  const fixture = TestBed.createComponent(component);
  return { fixture, http: TestBed.inject(HttpTestingController), html: fixture.nativeElement };
}

export function typeEmail(html: HTMLElement, value: string): void {
  const input = html.querySelector<HTMLInputElement>('input[type="email"]')!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

export async function submitForm<T>(fixture: ComponentFixture<T>): Promise<void> {
  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await fixture.whenStable();
}

/** Espera as promessas pendentes e a tela terminarem de reagir à resposta da API simulada. */
export async function settle<T>(fixture: ComponentFixture<T>): Promise<void> {
  await flushPromises();
  await fixture.whenStable();
  fixture.detectChanges();
}

export function accepted(): { status: number; statusText: string } {
  return { status: 202, statusText: 'Accepted' };
}
