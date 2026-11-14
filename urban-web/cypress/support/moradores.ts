export const RESIDENTS_PATH = '/moradores';

export interface StubOccupancy {
  id: string;
  unitId: string;
  unitBlock: string | null;
  unitNumber: string;
  kind: 'owner' | 'tenant';
  startDate: string;
  endDate: string | null;
}

export interface StubResident {
  id: string;
  fullName: string;
  maskedCpf: string;
  birthDate: string;
  phone: string;
  email: string | null;
  profession: string | null;
  emergencyContact: { name: string; relationship: string; phone: string } | null;
  occupancies: StubOccupancy[];
  version: string;
}

export function stubResident(overrides: Partial<StubResident> = {}): StubResident {
  return {
    id: '0199bbbb-0000-7000-8000-000000000001',
    fullName: 'Ana Lima',
    maskedCpf: '***.123.456-**',
    birthDate: '1990-05-20',
    phone: '47998765432',
    email: 'ana@exemplo.com',
    profession: 'Engenheira',
    emergencyContact: { name: 'Carlos Lima', relationship: 'Irmão', phone: '4733334444' },
    occupancies: [
      {
        id: '0199cccc-0000-7000-8000-000000000001',
        unitId: '0199aaaa-0000-7000-8000-000000000001',
        unitBlock: 'A',
        unitNumber: '101',
        kind: 'owner',
        startDate: '2024-03-01',
        endDate: null,
      },
    ],
    version: '7',
    ...overrides,
  };
}

export const CURRENT_RESIDENT = stubResident();

export const FORMER_RESIDENT = stubResident({
  id: '0199bbbb-0000-7000-8000-000000000002',
  fullName: 'Beto Rocha',
  maskedCpf: '***.987.654-**',
  phone: '4733334444',
  email: null,
  occupancies: [
    {
      id: '0199cccc-0000-7000-8000-000000000002',
      unitId: '0199aaaa-0000-7000-8000-000000000002',
      unitBlock: null,
      unitNumber: '15',
      kind: 'tenant',
      startDate: '2023-01-10',
      endDate: '2026-10-31',
    },
  ],
  version: '8',
});

const LIST_URL = /^\/api\/residents(\?.*)?$/;

/**
 * A lista de moradores, que responde conforme a situação pedida: "active" traz só quem mora, "former" só quem saiu e
 * "all" todos. O E2E do CI sobe só o front, sem API.
 */
export function stubResidents(
  items: StubResident[] = [CURRENT_RESIDENT, FORMER_RESIDENT],
  alias = 'residents',
): void {
  cy.intercept('GET', LIST_URL, (request) => {
    const status = new URL(request.url, 'http://localhost').searchParams.get('status');
    const lives = (item: StubResident): boolean => item.occupancies.some((o) => o.endDate === null);
    const shown = items.filter((item) =>
      status === 'former' ? !lives(item) : status === 'all' ? true : lives(item),
    );
    request.reply({
      statusCode: 200,
      body: { items: shown, total: shown.length, page: 1, pageSize: 50 },
    });
  }).as(alias);
}

export const NEW_RESIDENT_PATH = '/moradores/novo';

export function editResidentPath(id: string): string {
  return `${RESIDENTS_PATH}/${id}/editar`;
}

/** O morador que a tela de edição busca, pelo id. */
export function stubResidentDetail(resident: StubResident = CURRENT_RESIDENT): void {
  cy.intercept('GET', `/api/residents/${resident.id}`, { statusCode: 200, body: resident }).as(
    'residentDetail',
  );
}

/** O cadastro que dá certo: devolve o morador criado, com o vínculo à unidade pedida. */
export function stubResidentCreated(created: StubResident = CURRENT_RESIDENT): void {
  cy.intercept('POST', '/api/residents', { statusCode: 201, body: created }).as('createResident');
}

/** O cadastro que a API recusa, com o código do motivo. */
export function stubResidentCreateRejected(status: number, code: string): void {
  cy.intercept('POST', '/api/residents', {
    statusCode: status,
    body: { status, code, detail: 'Recusado' },
  }).as('createResident');
}

/** A edição que dá certo: devolve o morador com a versão seguinte. */
export function stubResidentUpdated(resident: StubResident, changes: Partial<StubResident>): void {
  cy.intercept('PUT', `/api/residents/${resident.id}`, {
    statusCode: 200,
    body: { ...resident, ...changes, version: String(Number(resident.version) + 1) },
  }).as('updateResident');
}

/** A edição que a API recusa, com o código do motivo. */
export function stubResidentUpdateRejected(
  resident: StubResident,
  status: number,
  code: string,
): void {
  cy.intercept('PUT', `/api/residents/${resident.id}`, {
    statusCode: status,
    body: { status, code, detail: 'Recusado' },
  }).as('updateResident');
}

function field(name: string): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get(`[formControlName="${name}"]`);
}

/** Digita num campo de texto: o rótulo flutuante do Material cobre o meio do campo vazio, então o clique vai à direita. */
export function typeInto(name: string, value: string): void {
  field(name).click('right').type(`{selectall}${value}`);
}

/** Preenche os campos obrigatórios do cadastro, menos a unidade e o tipo de vínculo (que são menus). */
export function fillResidentTexts(): void {
  typeInto('fullName', 'Maria Souza');
  typeInto('cpf', '52998224725');
  typeInto('birthDate', '20051990');
  typeInto('phone', '47998765432');
  typeInto('profession', 'Engenheira');
  typeInto('contactName', 'Carlos Souza');
  typeInto('contactRelationship', 'Irmão');
  typeInto('contactPhone', '4733334444');
  typeInto('startDate', '01032024');
}
