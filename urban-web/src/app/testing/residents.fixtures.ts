import { Resident, ResidentOccupancy, ResidentPage } from '../core/residents/residents.models';

let sequence = 0;

/** Vínculo em andamento de teste, com a unidade A-101 e entrada em 01/03/2024, salvo o que for informado. */
export function occupancy(overrides: Partial<ResidentOccupancy> = {}): ResidentOccupancy {
  sequence += 1;
  return {
    id: `0199cccc-0000-7000-8000-${String(sequence).padStart(12, '0')}`,
    unitId: '0199aaaa-0000-7000-8000-000000000001',
    unitBlock: 'A',
    unitNumber: '101',
    kind: 'owner',
    startDate: '2024-03-01',
    endDate: null,
    ...overrides,
  };
}

/** Morador de teste com um vínculo em andamento; cada chamada ganha um id novo. */
export function resident(overrides: Partial<Resident> = {}): Resident {
  sequence += 1;
  return {
    id: `0199bbbb-0000-7000-8000-${String(sequence).padStart(12, '0')}`,
    fullName: 'Maria Souza',
    maskedCpf: '***.456.789-**',
    birthDate: '1990-05-20',
    phone: '47998765432',
    email: 'maria@exemplo.com',
    profession: 'Engenheira',
    emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
    occupancies: [occupancy()],
    version: String(sequence),
    ...overrides,
  };
}

export function residentPage(
  items: Resident[],
  total = items.length,
  page = 1,
  pageSize = 50,
): ResidentPage {
  return { items, total, page, pageSize };
}
