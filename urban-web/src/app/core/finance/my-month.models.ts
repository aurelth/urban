import { ChargeStatus, PaymentMethod } from './payments.models';

/** O que a pessoa tem a ver com as cobranças: de uma ou mais unidades, só dependente (sem fatura) ou nenhuma. */
export type MyMonthAccess = 'unit' | 'dependent' | 'none';

/** Só como a pessoa mora, sem ler mês nenhum: é o que o menu pergunta para decidir se mostra "Minha fatura" ao síndico. */
export interface MyAccess {
  readonly access: MyMonthAccess;
}

/** Um pagamento da cobrança como o morador o vê; o recibo dele é baixado pelo `paymentId`. */
export interface MyPayment {
  readonly paymentId: string;
  readonly amount: number;
  /** AAAA-MM-DD. */
  readonly receivedOn: string;
  readonly method: PaymentMethod;
  readonly receiptNumber: number;
  /** O número como se fala dele, "REC-000123". */
  readonly receiptCode: string;
  /** Estornado: deixa de contar no que foi pago, e o recibo sai marcado como cancelado. */
  readonly reversed: boolean;
}

/** A cobrança do mês de uma unidade, como o morador a vê. */
export interface MyCharge {
  readonly chargeId: string;
  /** AAAA-MM-DD. */
  readonly dueDate: string;
  readonly amount: number;
  readonly paid: number;
  readonly balance: number;
  readonly status: ChargeStatus;
  /** AAAA-MM-DD do último pagamento, se houve. */
  readonly lastReceivedOn: string | null;
  /** Do mais antigo para o mais novo, estornados inclusive. */
  readonly payments: readonly MyPayment[];
}

/** Uma unidade da pessoa; `charge` é nulo com o mês aberto ou se a unidade não tem cobrança no mês. */
export interface MyUnit {
  readonly unitId: string;
  readonly block: string | null;
  readonly number: string;
  readonly charge: MyCharge | null;
}

export interface MyExpenseGroup {
  readonly groupId: string;
  readonly groupName: string;
  readonly total: number;
}

/** As despesas do condomínio no mês, sem fornecedor nem observação. */
export interface MyExpenses {
  readonly total: number;
  readonly groups: readonly MyExpenseGroup[];
}

/** O painel financeiro do morador num mês. `expenses` só vem com o mês fechado. */
export interface MyMonth {
  readonly month: string;
  /** AAAA-MM-DD do dia, em Brasília, a que a situação da cobrança se refere. */
  readonly asOf: string;
  readonly access: MyMonthAccess;
  readonly closed: boolean;
  readonly units: readonly MyUnit[];
  readonly expenses: MyExpenses | null;
}
