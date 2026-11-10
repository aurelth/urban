import { ApiProblem } from '../../core/http/api-problem';
import { formatPercent, unitLabel } from '../units/unit-values';

const GENERIC =
  'Complete os dados das unidades, na tela de unidades, para o método de rateio escolhido';

/** Quantas unidades a frase nomeia; passando disso, ela dá só a contagem e as primeiras. */
const NAMED_UNITS = 3;

interface UnitRef {
  readonly block: string | null;
  readonly number: string;
}

/** Como a frase chama o dado que falta, no singular e no plural, e o "preencha" que concorda com ele. */
interface MissingWording {
  readonly one: string;
  readonly many: string;
  readonly fillOne: string;
  readonly fillMany: string;
}

const MISSING: Readonly<Record<string, MissingWording>> = {
  fractions_missing: {
    one: 'a fração ideal',
    many: 'as frações ideais',
    fillOne: 'Preencha-a',
    fillMany: 'Preencha-as',
  },
  areas_missing: {
    one: 'a área',
    many: 'as áreas',
    fillOne: 'Preencha-a',
    fillMany: 'Preencha-as',
  },
};

/**
 * Monta a frase do erro "os dados das unidades não bastam para o rateio" a partir do que a API mandou em `data`: diz o que
 * falta (a soma atual das frações, ou quais unidades estão sem fração ou sem área) e onde resolver. Sem dados que se entendam,
 * cai no texto geral.
 */
export function describeApportionmentShortfall(problem: ApiProblem): string {
  const data = problem.data ?? {};
  const reason = String(data['reason']);

  if (reason === 'no_active_units') {
    return 'Não há unidades ativas para dividir o mês. Cadastre as unidades na tela de unidades';
  }
  if (reason === 'fractions_total') {
    const total = data['fractionTotal'];
    return typeof total === 'number'
      ? `A soma das frações ideais das unidades ativas é ${formatPercent(total)} e precisa chegar a 100%. Ajuste as frações na tela de unidades`
      : GENERIC;
  }
  if (Object.hasOwn(MISSING, reason)) {
    return describeMissing(MISSING[reason], data['units'], data['unitCount']);
  }
  return GENERIC;
}

function describeMissing(wording: MissingWording, units: unknown, unitCount: unknown): string {
  const named = Array.isArray(units) ? units.filter(isUnitRef) : [];
  if (named.length === 0) {
    return GENERIC;
  }

  const count = typeof unitCount === 'number' ? Math.max(unitCount, named.length) : named.length;
  const labels = joinLabels(named.slice(0, NAMED_UNITS).map(unitLabel));
  if (count === 1) {
    return `Falta ${wording.one} da unidade ${labels}. ${wording.fillOne} na tela de unidades`;
  }
  if (count <= NAMED_UNITS) {
    return `Faltam ${wording.many} das unidades ${labels}. ${wording.fillMany} na tela de unidades`;
  }
  return `Faltam ${wording.many} de ${count} unidades, entre elas ${labels}. ${wording.fillMany} na tela de unidades`;
}

function joinLabels(labels: readonly string[]): string {
  return labels.length === 1
    ? labels[0]
    : `${labels.slice(0, -1).join(', ')} e ${labels[labels.length - 1]}`;
}

function isUnitRef(value: unknown): value is UnitRef {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as { number?: unknown }).number === 'string'
  );
}
