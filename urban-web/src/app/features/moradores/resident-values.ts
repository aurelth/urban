import { unitLabel } from '../units/unit-values';
import { OccupancyKind, Resident, ResidentOccupancy } from '../../core/residents/residents.models';

const MOBILE_DIGITS = 11;
const LANDLINE_DIGITS = 10;
const AREA_CODE_LENGTH = 2;
const LANDLINE_SPLIT = 6;
const MOBILE_SPLIT = 7;

const KIND_LABELS: Readonly<Record<OccupancyKind, string>> = {
  owner: 'Proprietário',
  tenant: 'Inquilino',
  dependent: 'Dependente',
};

/** Mora hoje quem tem ao menos um vínculo em andamento; quem só tem vínculos encerrados é ex-morador. */
export function isCurrentResident(resident: Pick<Resident, 'occupancies'>): boolean {
  return resident.occupancies.some((occupancy) => occupancy.endDate === null);
}

/** "Bloco A · 101", ou só o número quando a unidade não tem bloco. */
export function occupancyUnit(occupancy: ResidentOccupancy): string {
  return unitLabel({ block: occupancy.unitBlock, number: occupancy.unitNumber });
}

export function kindLabel(kind: OccupancyKind): string {
  return KIND_LABELS[kind];
}

/** "desde 01/03/2024" para quem mora, "de 01/03/2024 a 31/10/2026" para quem já saiu. */
export function occupancyPeriod(occupancy: ResidentOccupancy): string {
  const start = formatDay(occupancy.startDate);
  return occupancy.endDate === null
    ? `desde ${start}`
    : `de ${start} a ${formatDay(occupancy.endDate)}`;
}

/** "47998765432" vira "(47) 99876-5432" e "4733334444" vira "(47) 3333-4444"; outro texto volta como veio. */
export function formatPhone(digits: string): string {
  if (!/^\d+$/.test(digits)) {
    return digits;
  }
  if (digits.length === MOBILE_DIGITS) {
    return `(${digits.slice(0, AREA_CODE_LENGTH)}) ${digits.slice(AREA_CODE_LENGTH, MOBILE_SPLIT)}-${digits.slice(MOBILE_SPLIT)}`;
  }
  if (digits.length === LANDLINE_DIGITS) {
    return `(${digits.slice(0, AREA_CODE_LENGTH)}) ${digits.slice(AREA_CODE_LENGTH, LANDLINE_SPLIT)}-${digits.slice(LANDLINE_SPLIT)}`;
  }
  return digits;
}

/** "2026-11-10" vira "10/11/2026". */
export function formatDay(date: string): string {
  const [year, month, day] = date.split('-');
  return `${day}/${month}/${year}`;
}
