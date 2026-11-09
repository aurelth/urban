import { MessagesByCode } from '../../core/http/problem-message';

const FIELD_MESSAGES: MessagesByCode = {
  'expense_group.name.invalid': 'Informe o nome do grupo, com até 60 caracteres',
  'expense_group.name.already_exists': 'Já existe um grupo com este nome',
  'expense.description.invalid': 'Informe a descrição, com até 120 caracteres',
  'expense.supplier.invalid': 'O fornecedor pode ter até 120 caracteres',
  'expense.amount.invalid':
    'Informe um valor maior que zero e menor que R$ 10.000.000,00, com até 2 casas decimais',
  'expense.installment_count.invalid': 'O número de parcelas deve ser de 2 a 120',
  'expense.current_installment.invalid':
    'A parcela atual deve estar entre 1 e o número de parcelas',
  'expense.group_required': 'Escolha o grupo da despesa',
  'expense.end_before_start': 'O mês final não pode ser anterior ao mês inicial',
  'expense.change_before_start':
    'O novo valor não pode valer a partir de um mês anterior ao início da despesa',
  'expense.due_day.invalid': 'Informe o dia do vencimento, de 1 a 31',
  'expense.kind.invalid': 'Escolha se a despesa é única, fixa mensal, variável mensal ou parcelada',
  'expense.not_fixed': 'Só despesas fixas mensais têm reajuste de valor',
  'expense.not_installment': 'Só despesas parceladas têm parcelas',
  'expense.end_not_allowed': 'Só despesas fixas e variáveis podem ser encerradas',
  'consumption.bands_invalid':
    'As faixas precisam ser de 1 a 6, com limites crescentes, a última sem limite',
  'consumption.gas_invalid': 'Informe os kg por m³ e o preço do kg, ambos maiores que zero',
  'consumption.sewage_percent_invalid':
    'O percentual do esgoto vai de 0 a 100, com até 2 casas decimais',
  'consumption.reserve_percent_invalid':
    'O percentual do fundo de reserva vai de 0 a 100, com até 2 casas decimais',
  'month.invalid': 'Informe um mês válido',
  'month.too_far_ahead': 'Só é possível abrir meses até 5 anos à frente',
  'expense_entry.amount_required': 'Informe o valor do lançamento antes de marcá-lo como pago',
  'expense_entry.status.invalid': 'Escolha pendente, pago ou cancelado',
  'closing.due_date_invalid':
    'Informe uma data de vencimento válida, a partir do primeiro dia do mês',
  'closing.month_not_open': 'Este mês ainda não tem lançamentos para fechar',
  'closing.amounts_pending':
    'Informe o valor de todos os lançamentos variáveis antes de fechar o mês',
  'closing.nothing_to_charge': 'O mês não tem nada a cobrar: todos os lançamentos estão cancelados',
  'closing.total_invalid':
    'O total do mês deve ter no máximo 2 casas decimais e não passar de R$ 9.999.999,99',
  'closing.no_weights': 'Cadastre as unidades do condomínio antes de fechar o mês',
  'closing.readings_pending':
    'Falta a leitura de água ou gás de alguma unidade. Informe as leituras do mês antes de fechar',
  'closing.month_is_closed': 'O mês está fechado. Reabra o mês para mudar os lançamentos',
  'condominium.apportionment_not_ready':
    'Complete os dados das unidades, na tela de unidades, para o método de rateio escolhido',
  'finance.version.required': 'Recarregue a lista antes de continuar',
  'finance.condominium_not_found': 'Cadastre o condomínio antes de usar o financeiro',
  'identity.user.not_manager': 'Só o síndico pode usar o financeiro',
  'request.invalid': 'Não foi possível ler os dados enviados. Confira o formato e tente de novo',
};

/** Dentro de um diálogo: quando algo mudou em outro lugar, a pessoa fecha a janela e confere a tela, que já foi atualizada. */
export const FINANCE_FORM_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict':
    'Este item foi alterado em outro lugar. Feche esta janela e confira a tela, que já foi atualizada',
  'expense.not_found':
    'Esta despesa não existe mais. Feche esta janela e confira a tela, que já foi atualizada',
  'expense.installment.not_found':
    'Esta parcela não existe mais. Feche esta janela e confira a tela, que já foi atualizada',
  'expense_group.not_found':
    'Este grupo não existe mais. Feche esta janela e confira a tela, que já foi atualizada',
  'expense_entry.not_found':
    'Este lançamento não existe mais. Feche esta janela e confira a tela, que já foi atualizada',
  'consumption.tariff_not_found':
    'Esta tarifa não existe mais. Feche esta janela e confira a tela, que já foi atualizada',
  'closing.already_closed':
    'Este mês já está fechado. Feche esta janela e confira a tela, que já foi atualizada',
  'closing.not_closed':
    'Este mês não está fechado. Feche esta janela e confira a tela, que já foi atualizada',
};

/** Ações na própria tela, que já foi atualizada quando o aviso aparece. */
export const FINANCE_ACTION_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict': 'Este item foi alterado em outro lugar. A tela já foi atualizada',
  'expense.not_found': 'Esta despesa não existe mais. A tela já foi atualizada',
  'expense_group.not_found': 'Este grupo não existe mais. A tela já foi atualizada',
  'expense_entry.not_found': 'Este lançamento não existe mais. A tela já foi atualizada',
  'consumption.tariff_not_found': 'Esta tarifa não existe mais. A tela já foi atualizada',
  'closing.already_closed': 'Este mês já está fechado. A tela já foi atualizada',
  'closing.not_closed': 'Este mês não está fechado. A tela já foi atualizada',
};
