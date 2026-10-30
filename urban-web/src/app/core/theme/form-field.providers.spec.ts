import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatFormField } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { provideFormFieldDefaults } from './form-field.providers';

@Component({
  imports: [MatFormField, MatInput],
  template: `<mat-form-field><input matInput /></mat-form-field>`,
})
class Host {}

const DYNAMIC = 'mat-mdc-form-field-subscript-dynamic-size';

/** O Material marca a área do erro e da dica (não o campo todo) quando ela cresce com o texto. */
function subscript(): HTMLElement {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return fixture.nativeElement.querySelector('.mat-mdc-form-field-subscript-wrapper');
}

describe('provideFormFieldDefaults', () => {
  it('faz a área do erro crescer com o texto, para uma mensagem de duas linhas não invadir o campo de baixo', () => {
    TestBed.configureTestingModule({ providers: [provideFormFieldDefaults()] });

    expect(subscript().classList).toContain(DYNAMIC);
  });

  it('sem ele, o Material reserva só uma linha para o erro (é por isso que ele existe)', () => {
    expect(subscript().classList).not.toContain(DYNAMIC);
  });
});
