import { Vehicle, VehiclePage } from '../core/vehicles/vehicles.models';

let sequence = 0;

/** Veículo de teste; cada chamada ganha um id novo e uma placa diferente, se não forem informados. */
export function vehicle(overrides: Partial<Vehicle> = {}): Vehicle {
  sequence += 1;
  return {
    id: `0199dddd-0000-7000-8000-${String(sequence).padStart(12, '0')}`,
    unitId: '0199aaaa-0000-7000-8000-000000000001',
    unitBlock: 'A',
    unitNumber: '101',
    plate: `ABC${1000 + sequence}`,
    model: 'Fiat Argo',
    color: 'Prata',
    version: String(sequence),
    ...overrides,
  };
}

export function vehiclePage(
  items: Vehicle[],
  total = items.length,
  page = 1,
  pageSize = 50,
): VehiclePage {
  return { items, total, page, pageSize };
}
