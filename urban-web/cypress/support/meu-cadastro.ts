import { StubOccupancy, StubResident, stubResident } from './moradores';

export const MY_DATA_PATH = '/meus-dados';
export const MY_REGISTRATION_API = '/api/residents/me';

/** A unidade onde o morador mora hoje. */
export const MY_CURRENT_OCCUPANCY: StubOccupancy = {
  id: '0199cccc-0000-7000-8000-000000000101',
  unitId: '0199aaaa-0000-7000-8000-000000000001',
  unitBlock: 'A',
  unitNumber: '101',
  kind: 'owner',
  startDate: '2024-03-01',
  endDate: null,
};

/** A unidade que ele já deixou. */
export const MY_FORMER_OCCUPANCY: StubOccupancy = {
  id: '0199cccc-0000-7000-8000-000000000102',
  unitId: '0199aaaa-0000-7000-8000-000000000002',
  unitBlock: 'A',
  unitNumber: '102',
  kind: 'tenant',
  startDate: '2022-01-10',
  endDate: '2023-12-31',
};

/** O cadastro do morador logado: dados completos, uma unidade onde mora e uma que já deixou. */
export const MY_REGISTRATION: StubResident = stubResident({
  id: '0199bbbb-0000-7000-8000-000000000301',
  fullName: 'João Pereira Souza',
  maskedCpf: '***.456.789-**',
  birthDate: '1985-08-30',
  phone: '47991234567',
  email: 'morador@example.com',
  profession: 'Professor',
  emergencyContact: { name: 'Carla Souza', relationship: 'Esposa', phone: '4733334444' },
  accessStatus: 'active',
  occupancies: [MY_CURRENT_OCCUPANCY, MY_FORMER_OCCUPANCY],
});

/** O cadastro do síndico que ainda não mora no condomínio: a pessoa existe, sem nenhuma unidade. */
export const MANAGER_REGISTRATION: StubResident = stubResident({
  id: '0199bbbb-0000-7000-8000-000000000302',
  fullName: 'Marina Souza',
  maskedCpf: '***.982.247-**',
  birthDate: '1980-06-15',
  phone: '47991234567',
  email: null,
  profession: null,
  emergencyContact: null,
  accessStatus: 'active',
  occupancies: [],
});

/** O cadastro de quem está logado (GET /api/residents/me). */
export function stubMyRegistration(registration: StubResident = MY_REGISTRATION): void {
  cy.intercept('GET', MY_REGISTRATION_API, { statusCode: 200, body: registration }).as(
    'myRegistration',
  );
}

export function stubMyRegistrationRejected(status: number, code: string): void {
  cy.intercept('GET', MY_REGISTRATION_API, {
    statusCode: status,
    body: { status, code, detail: 'Recusado' },
  }).as('myRegistration');
}
