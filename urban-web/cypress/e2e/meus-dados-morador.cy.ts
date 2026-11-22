import { PALETTE } from '../support/layout';
import {
  MY_DATA_PATH,
  MY_REGISTRATION,
  stubMyRegistration,
  stubMyRegistrationRejected,
} from '../support/meu-cadastro';
import { DESKTOP, chooseUserMenu, expectText } from '../support/navegacao';
import { RESIDENT, stubSignedIn } from '../support/session';

function section(name: string): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get(`[data-testid="${name}-section"]`);
}

describe('Meus dados do morador', () => {
  beforeEach(() => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    stubSignedIn(RESIDENT);
    stubMyRegistration();
  });

  it('abre pelo menu do avatar e mostra o cadastro do morador', () => {
    cy.visit('/dashboard');
    chooseUserMenu('menu-my-data');

    cy.location('pathname').should('eq', MY_DATA_PATH);
    cy.contains('h1', 'Meus dados').should('be.visible');
    cy.wait('@myRegistration');
    expectText('[data-testid="full-name"]', 'João Pereira Souza');
    expectText('[data-testid="cpf"]', '***.456.789-**');
    expectText('[data-testid="birth-date"]', '30/08/1985');
    expectText('[data-testid="phone"]', '(47) 99123-4567');
    expectText('[data-testid="email"]', 'morador@example.com');
    expectText('[data-testid="profession"]', 'Professor');
  });

  it('mostra o contato de emergência', () => {
    cy.visit(MY_DATA_PATH);

    section('emergency')
      .should('contain.text', 'Contato de emergência')
      .and('contain.text', 'Carla Souza')
      .and('contain.text', 'Esposa')
      .and('contain.text', '(47) 3333-4444');
  });

  it('mostra as unidades, com "Mora aqui" em verde e "Já saiu" em laranja', () => {
    cy.visit(MY_DATA_PATH);

    cy.get('[data-testid="occupancy"]').should('have.length', 2);
    cy.get('[data-testid="occupancy"]')
      .first()
      .should('contain.text', 'Bloco A · 101')
      .and('contain.text', 'Proprietário, desde 01/03/2024');
    cy.contains('[data-testid="occupancy"] p', 'Mora aqui').should(
      'have.css',
      'color',
      PALETTE.light.success,
    );
    cy.get('[data-testid="occupancy"]')
      .last()
      .should('contain.text', 'Bloco A · 102')
      .and('contain.text', 'Inquilino, de 10/01/2022 a 31/12/2023');
    cy.contains('[data-testid="occupancy"] p', 'Já saiu').should(
      'have.css',
      'color',
      PALETTE.light.warning,
    );
  });

  it('é só leitura e manda a correção para o síndico', () => {
    cy.visit(MY_DATA_PATH);
    cy.get('[data-testid="full-name"]').should('exist');

    cy.get('[data-testid="correction-note"]').should(
      'contain.text',
      'Para corrigir qualquer dado, fale com o síndico',
    );
    cy.get('main form, main input, main button').should('not.exist');
  });

  it('avisa quando a conta não tem cadastro de morador', () => {
    stubMyRegistrationRejected(404, 'condominium.resident.not_found');
    cy.visit(MY_DATA_PATH);

    cy.get('[data-testid="not-found"]').should(
      'contain.text',
      'Não encontramos o seu cadastro de morador. Fale com o síndico',
    );
  });

  it('avisa quando o cadastro não carrega e "Tentar de novo" busca outra vez', () => {
    stubMyRegistrationRejected(500, 'internal');
    cy.visit(MY_DATA_PATH);
    cy.get('[data-testid="load-error"]').should(
      'contain.text',
      'Não foi possível carregar seus dados',
    );

    stubMyRegistration(MY_REGISTRATION);
    cy.get('[data-testid="retry"]').click();

    expectText('[data-testid="full-name"]', 'João Pereira Souza');
  });
});
