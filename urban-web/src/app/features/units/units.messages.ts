import { MessagesByCode } from '../../core/http/problem-message';
import { AREA_MESSAGE, FRACTION_MESSAGE } from './unit-batch.rows';

/** Texto da tela para cada código de recusa de campo que as unidades podem receber da API. */
const FIELD_MESSAGES: MessagesByCode = {
  'unit.number.invalid': 'Informe o número da unidade, com até 20 caracteres',
  'unit.block.invalid': 'O bloco pode ter até 30 caracteres',
  'unit.type.invalid': 'Escolha se a unidade é apartamento ou casa',
  'unit.ideal_fraction.invalid': FRACTION_MESSAGE,
  'unit.area.invalid': AREA_MESSAGE,
  'unit.identifier.already_exists': 'Já existe uma unidade com este bloco e número',
  'unit.ideal_fraction.sum_exceeded':
    'A soma das frações ideais das unidades ativas não pode passar de 100%',
  'unit.version.required': 'Recarregue a lista antes de continuar',
  'unit.page.invalid': 'Página inválida. Recarregue a lista',
  'unit.batch.size_invalid': 'O cadastro em lote aceita de 1 a 500 unidades',
  'condominium.apportionment_method.invalid': 'Escolha fração ideal, valor igual ou área',
  'identity.user.not_manager': 'Só o síndico pode cadastrar unidades',
  'request.invalid': 'Não foi possível ler os dados enviados. Confira o formato e tente de novo',
};

/** Cadastro e edição (dentro do diálogo): quando a unidade mudou, a pessoa fecha a janela e confere a lista. */
export const UNIT_FORM_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict':
    'Esta unidade foi alterada em outro lugar. Feche esta janela e confira a lista, que já foi atualizada',
  'unit.not_found':
    'Esta unidade não existe mais. Feche esta janela e confira a lista, que já foi atualizada',
  'unit.deactivated':
    'Esta unidade foi desativada em outro lugar. Feche esta janela e confira a lista, que já foi atualizada',
};

/** Desativar e reativar (na própria lista, que já foi atualizada quando o aviso aparece). */
export const UNIT_ACTION_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict': 'Esta unidade foi alterada em outro lugar. A lista já foi atualizada',
  'unit.not_found': 'Esta unidade não existe mais. A lista já foi atualizada',
  'unit.deactivated': 'Esta unidade foi desativada em outro lugar. A lista já foi atualizada',
  'unit.already_deactivated': 'Esta unidade já estava desativada. A lista já foi atualizada',
  'unit.already_active': 'Esta unidade já estava ativa. A lista já foi atualizada',
};

export const METHOD_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict':
    'O método de rateio foi alterado em outro lugar. Confira a escolha atual e tente de novo',
};

/** Cadastro em lote: o motivo de cada linha e o aviso geral. */
export const BATCH_MESSAGES: MessagesByCode = FIELD_MESSAGES;

export const BATCH_REJECTED_MESSAGE =
  'Nenhuma unidade foi cadastrada. Corrija as linhas marcadas e tente de novo';
