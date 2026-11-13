import { CurrentUser } from './auth.models';

/** Separadores que, no começo do e-mail, dividem nome e sobrenome: "maria.silva", "joao_pedro-lima". */
const EMAIL_WORD_SEPARATORS = /[._-]+/;

/** Primeira letra de uma palavra, em maiúscula (pt-BR) e sem partir um caractere em dois. */
function firstLetter(word: string): string {
  return Array.from(word)[0].toLocaleUpperCase('pt-BR');
}

/** Iniciais da primeira e da última palavra ("Ana Maria Souza" vira "AS"); uma palavra só dá uma letra. */
function initialsOfWords(words: readonly string[]): string {
  if (words.length === 0) {
    return '';
  }
  const first = firstLetter(words[0]);
  return words.length === 1 ? first : first + firstLetter(words[words.length - 1]);
}

function wordsOf(text: string, separator: RegExp): string[] {
  return text.split(separator).filter((word) => word.length > 0);
}

/**
 * As letras que aparecem no avatar: as do nome escolhido e, enquanto a pessoa não escolheu um, as do começo do
 * e-mail. "?" só aparece se não houver nada de onde tirar uma letra.
 */
export function initialsOf(user: Pick<CurrentUser, 'displayName' | 'email'>): string {
  const fromName = initialsOfWords(wordsOf(user.displayName ?? '', /\s+/));
  if (fromName) {
    return fromName;
  }
  const localPart = user.email.split('@')[0] ?? '';
  return initialsOfWords(wordsOf(localPart, EMAIL_WORD_SEPARATORS)) || '?';
}
