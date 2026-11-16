export const PAYMENTS_PATH = '/financeiro/pagamentos';

export function paymentsApi(month = '2026-11'): string {
  return `/api/finance/months/${month}/payments`;
}

export interface StubCharge {
  chargeId: string;
  unitId: string;
  block: string | null;
  number: string;
  amount: number;
  paid: number;
  balance: number;
  status: 'open' | 'partial' | 'paid' | 'overdue';
  lastReceivedOn: string | null;
}

export interface StubPayment {
  id: string;
  chargeId: string;
  amount: number;
  receivedOn: string;
  method: 'pix' | 'boleto' | 'transfer' | 'cash' | 'other';
  note: string | null;
  registeredAt: string;
  reversedAt: string | null;
  reversalReason: string | null;
}

export const CHARGE_101: StubCharge = {
  chargeId: '0199bbbb-0030-7000-8000-000000000101',
  unitId: '0199bbbb-0005-7000-8000-000000000101',
  block: 'A',
  number: '101',
  amount: 800,
  paid: 0,
  balance: 800,
  status: 'overdue',
  lastReceivedOn: null,
};

export const CHARGE_102: StubCharge = {
  chargeId: '0199bbbb-0030-7000-8000-000000000102',
  unitId: '0199bbbb-0005-7000-8000-000000000102',
  block: 'A',
  number: '102',
  amount: 800,
  paid: 500,
  balance: 300,
  status: 'partial',
  lastReceivedOn: '2026-11-10',
};

export const CHARGE_201: StubCharge = {
  chargeId: '0199bbbb-0030-7000-8000-000000000201',
  unitId: '0199bbbb-0005-7000-8000-000000000201',
  block: null,
  number: '201',
  amount: 800,
  paid: 800,
  balance: 0,
  status: 'paid',
  lastReceivedOn: '2026-11-12',
};

export const PAYMENT_ON_102: StubPayment = {
  id: '0199bbbb-0031-7000-8000-000000000001',
  chargeId: CHARGE_102.chargeId,
  amount: 500,
  receivedOn: '2026-11-10',
  method: 'pix',
  note: null,
  registeredAt: '2026-11-10T15:30:00Z',
  reversedAt: null,
  reversalReason: null,
};

/**
 * As cobranças do mês (GET), com o filtro de situação e a paginação que a API faz. Devolve a lista viva: o registro de
 * pagamento (`stubPaymentRegistration`) a atualiza, então a leitura seguinte já mostra o novo saldo.
 */
export function stubPayments(
  initial: readonly StubCharge[] = [CHARGE_101, CHARGE_102, CHARGE_201],
  month = '2026-11',
): StubCharge[] {
  const charges = initial.map((charge) => ({ ...charge }));
  cy.intercept({ method: 'GET', pathname: paymentsApi(month) }, (request) => {
    const params = new URL(request.url).searchParams;
    const status = params.get('status');
    const page = Number(params.get('page') ?? '1');
    const pageSize = Number(params.get('pageSize') ?? '50');
    const filtered = status ? charges.filter((charge) => charge.status === status) : charges;
    request.reply({
      statusCode: 200,
      body: {
        month,
        dueDate: '2026-12-10',
        charged: charges.reduce((sum, charge) => sum + charge.amount, 0),
        received: charges.reduce((sum, charge) => sum + charge.paid, 0),
        total: filtered.length,
        page,
        pageSize,
        items: filtered.slice((page - 1) * pageSize, page * pageSize),
      },
    });
  }).as('payments');
  return charges;
}

/** O mês ainda não foi fechado: a API recusa a lista com 409. */
export function stubPaymentsNotClosed(month = '2026-11'): void {
  cy.intercept(
    { method: 'GET', pathname: paymentsApi(month) },
    {
      statusCode: 409,
      body: { status: 409, code: 'closing.not_closed', detail: 'Detalhe da API' },
    },
  ).as('payments');
}

/** O registro (POST) de um pagamento: atualiza a cobrança da lista viva e devolve o pagamento criado. */
export function stubPaymentRegistration(charges: StubCharge[]): void {
  cy.intercept('POST', /\/api\/finance\/charges\/[^/]+\/payments$/, (request) => {
    const chargeId = request.url.split('/').at(-2)!;
    const charge = charges.find((candidate) => candidate.chargeId === chargeId)!;
    const body = request.body as { amount: number; receivedOn: string };
    charge.paid += body.amount;
    charge.balance -= body.amount;
    charge.status = charge.balance === 0 ? 'paid' : 'partial';
    charge.lastReceivedOn = body.receivedOn;
    request.reply({
      statusCode: 201,
      body: { ...PAYMENT_ON_102, chargeId, amount: body.amount, receivedOn: body.receivedOn },
    });
  }).as('register');
}

/** O histórico de uma cobrança (GET); o estorno (POST) marca o pagamento da lista viva como estornado. */
export function stubChargePayments(charge: StubCharge, payments: StubPayment[]): void {
  cy.intercept('GET', `/api/finance/charges/${charge.chargeId}/payments`, (request) => {
    const paid = payments
      .filter((payment) => payment.reversedAt === null)
      .reduce((sum, payment) => sum + payment.amount, 0);
    request.reply({
      statusCode: 200,
      body: {
        chargeId: charge.chargeId,
        month: '2026-11',
        dueDate: '2026-12-10',
        amount: charge.amount,
        paid,
        balance: charge.amount - paid,
        status: paid === 0 ? 'open' : paid === charge.amount ? 'paid' : 'partial',
        payments,
      },
    });
  }).as('chargePayments');
  cy.intercept('POST', /\/api\/finance\/payments\/[^/]+\/reverse$/, (request) => {
    const id = request.url.split('/').at(-2)!;
    const payment = payments.find((candidate) => candidate.id === id)!;
    payment.reversedAt = '2026-11-15T15:00:00Z';
    payment.reversalReason = (request.body as { reason: string }).reason;
    request.reply({ statusCode: 204 });
  }).as('reverse');
}
