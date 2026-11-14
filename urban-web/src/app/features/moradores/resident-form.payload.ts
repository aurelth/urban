import {
  EmergencyContact,
  OccupancyKind,
  Resident,
  ResidentPayload,
  UpdateResidentPayload,
} from '../../core/residents/residents.models';
import { cleanText } from '../../shared/forms/address.payload';
import { applyMask, unmask } from '../../shared/forms/masks';

/**
 * O que o formulário guarda, com os textos como aparecem nos campos (máscaras e data em dd/mm/aaaa). O CPF só existe aqui
 * quando a pessoa o digita: o número salvo nunca chega à tela, só a versão mascarada, que fica fora do formulário. Unidade,
 * tipo de vínculo e entrada só valem no cadastro.
 */
export interface ResidentFormValue {
  fullName: string;
  cpf: string;
  birthDate: string;
  phone: string;
  email: string;
  profession: string;
  contactName: string;
  contactRelationship: string;
  contactPhone: string;
  unitId: string;
  kind: OccupancyKind | '';
  startDate: string;
}

export const EMPTY_RESIDENT_FORM: ResidentFormValue = {
  fullName: '',
  cpf: '',
  birthDate: '',
  phone: '',
  email: '',
  profession: '',
  contactName: '',
  contactRelationship: '',
  contactPhone: '',
  unitId: '',
  kind: '',
  startDate: '',
};

/** Dos dados da API para os campos. O que o morador não tem (e-mail, profissão, contato) fica em branco. */
export function toResidentFormValue(resident: Resident): ResidentFormValue {
  return {
    ...EMPTY_RESIDENT_FORM,
    fullName: resident.fullName,
    birthDate: toDisplayDate(resident.birthDate),
    phone: applyMask('phone', resident.phone),
    email: resident.email ?? '',
    profession: resident.profession ?? '',
    contactName: resident.emergencyContact?.name ?? '',
    contactRelationship: resident.emergencyContact?.relationship ?? '',
    contactPhone: applyMask('phone', resident.emergencyContact?.phone ?? ''),
  };
}

/** Corpo do cadastro: só dígitos, a data em AAAA-MM-DD e os textos sem espaços sobrando. */
export function toCreatePayload(value: ResidentFormValue): ResidentPayload {
  return {
    ...personalPayload(value),
    cpf: unmask('cpf', value.cpf),
    unitId: value.unitId,
    kind: value.kind as OccupancyKind,
    startDate: unmask('date', value.startDate),
  };
}

/** Corpo da edição. Sem CPF novo a chave nem existe e a API mantém o atual. */
export function toUpdatePayload(value: ResidentFormValue, version: string): UpdateResidentPayload {
  const cpf = unmask('cpf', value.cpf);
  return { version, ...personalPayload(value), ...(cpf === '' ? {} : { cpf }) };
}

/** Mudou algo? Compara o que iria para a API, então espaço e máscara não contam. */
export function residentChanged(original: ResidentFormValue, current: ResidentFormValue): boolean {
  return (
    JSON.stringify(toUpdatePayload(original, '')) !== JSON.stringify(toUpdatePayload(current, ''))
  );
}

function personalPayload(
  value: ResidentFormValue,
): Omit<ResidentPayload, 'cpf' | 'unitId' | 'kind' | 'startDate'> {
  return {
    fullName: cleanText(value.fullName),
    birthDate: unmask('date', value.birthDate),
    phone: unmask('phone', value.phone),
    email: cleanText(value.email) || null,
    profession: cleanText(value.profession) || null,
    emergencyContact: contactPayload(value),
  };
}

/** Sem nenhum dos três campos o contato vai nulo (o dependente menor de idade); com algum, vai como está, para a API apontar o que falta. */
function contactPayload(value: ResidentFormValue): EmergencyContact | null {
  const contact = {
    name: cleanText(value.contactName),
    relationship: cleanText(value.contactRelationship),
    phone: unmask('phone', value.contactPhone),
  };
  return contact.name === '' && contact.relationship === '' && contact.phone === ''
    ? null
    : contact;
}

function toDisplayDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-');
  return year && month && day ? `${day}/${month}/${year}` : '';
}
