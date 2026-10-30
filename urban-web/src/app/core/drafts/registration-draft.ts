import { Injectable } from '@angular/core';

/** Endereço como o formulário o guarda: com as máscaras que a pessoa digitou. */
export interface DraftAddress {
  readonly postalCode: string;
  readonly street: string;
  readonly number: string;
  readonly complement: string;
  readonly neighborhood: string;
  readonly city: string;
  readonly state: string;
}

/**
 * O que o rascunho do cadastro do condomínio guarda. De propósito, SÓ campos que não identificam a pessoa de forma
 * sensível: nunca CPF, data de nascimento nem senha. Quem digita esses três de novo perde pouco; quem tiver o
 * navegador emprestado não os lê.
 */
export interface RegistrationDraftData {
  readonly manager: {
    readonly fullName: string;
    readonly displayName: string;
    readonly phone: string;
    readonly address: DraftAddress;
  };
  readonly condominium: {
    readonly name: string;
    readonly cnpj: string;
    readonly address: DraftAddress;
  };
  readonly sameAddress: boolean;
}

const KEY_PREFIX = 'urban.registration-draft.';
const VALIDITY_MS = 7 * 24 * 60 * 60 * 1000;
const ADDRESS_FIELDS = [
  'postalCode',
  'street',
  'number',
  'complement',
  'neighborhood',
  'city',
  'state',
] as const;

/**
 * Rascunho do cadastro interrompido, guardado no navegador (localStorage), um por usuário. Vale por 7 dias e some ao sair.
 * Tudo aqui é "melhor esforço": armazenamento cheio ou bloqueado nunca quebra a tela, só deixa de guardar. O que vem do
 * armazenamento é tratado como entrada não confiável: só é aceito o que tem exatamente o formato esperado, e campos a mais
 * (que alguém tenha gravado à mão) são descartados na leitura.
 */
@Injectable({ providedIn: 'root' })
export class RegistrationDraft {
  save(userId: string, data: RegistrationDraftData, now: number = Date.now()): void {
    try {
      window.localStorage.setItem(keyOf(userId), JSON.stringify({ savedAt: now, data }));
    } catch {
      // Cheio ou bloqueado: sem rascunho, a pessoa só perde a comodidade.
    }
  }

  load(userId: string, now: number = Date.now()): RegistrationDraftData | null {
    const key = keyOf(userId);
    let raw: string | null;
    try {
      raw = window.localStorage.getItem(key);
    } catch {
      return null;
    }
    if (raw === null) {
      return null;
    }

    const data = readStored(raw, now);
    if (data === null) {
      this.remove(key);
    }
    return data;
  }

  clear(userId: string): void {
    this.remove(keyOf(userId));
  }

  /** Apaga o rascunho de todos os usuários deste navegador, sem tocar no resto do armazenamento. */
  clearAll(): void {
    try {
      const storage = window.localStorage;
      const keys: string[] = [];
      for (let index = 0; index < storage.length; index++) {
        const key = storage.key(index);
        if (key?.startsWith(KEY_PREFIX)) {
          keys.push(key);
        }
      }
      keys.forEach((key) => this.remove(key));
    } catch {
      // Bloqueado: não há o que apagar que a tela consiga alcançar.
    }
  }

  private remove(key: string): void {
    try {
      window.localStorage.removeItem(key);
    } catch {
      // Bloqueado: o rascunho vence sozinho em 7 dias.
    }
  }
}

function keyOf(userId: string): string {
  return `${KEY_PREFIX}${userId}`;
}

function readStored(raw: string, now: number): RegistrationDraftData | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return null;
  }
  if (!isRecord(parsed)) {
    return null;
  }

  const savedAt = parsed['savedAt'];
  if (typeof savedAt !== 'number' || !Number.isFinite(savedAt) || savedAt > now) {
    return null;
  }
  if (now - savedAt >= VALIDITY_MS) {
    return null;
  }

  return toDraft(parsed['data']);
}

function toDraft(value: unknown): RegistrationDraftData | null {
  if (!isRecord(value) || typeof value['sameAddress'] !== 'boolean') {
    return null;
  }
  const manager = value['manager'];
  const condominium = value['condominium'];
  if (!isRecord(manager) || !isRecord(condominium)) {
    return null;
  }

  const managerAddress = toAddress(manager['address']);
  const condominiumAddress = toAddress(condominium['address']);
  const fullName = manager['fullName'];
  const displayName = manager['displayName'];
  const phone = manager['phone'];
  const name = condominium['name'];
  const cnpj = condominium['cnpj'];
  if (
    managerAddress === null ||
    condominiumAddress === null ||
    typeof fullName !== 'string' ||
    typeof displayName !== 'string' ||
    typeof phone !== 'string' ||
    typeof name !== 'string' ||
    typeof cnpj !== 'string'
  ) {
    return null;
  }

  return {
    manager: { fullName, displayName, phone, address: managerAddress },
    condominium: { name, cnpj, address: condominiumAddress },
    sameAddress: value['sameAddress'],
  };
}

function toAddress(value: unknown): DraftAddress | null {
  if (!isRecord(value)) {
    return null;
  }
  const picked: Record<string, string> = {};
  for (const field of ADDRESS_FIELDS) {
    const text = value[field];
    if (typeof text !== 'string') {
      return null;
    }
    picked[field] = text;
  }
  return picked as unknown as DraftAddress;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
