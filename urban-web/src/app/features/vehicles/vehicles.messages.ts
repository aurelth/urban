import { MessagesByCode } from '../../core/http/problem-message';

/** Texto da tela para cada código de recusa de campo que os veículos podem receber da API. */
const FIELD_MESSAGES: MessagesByCode = {
  'vehicle.plate.invalid': 'Confira a placa: use o formato ABC1234 ou ABC1D23',
  'vehicle.plate.already_registered': 'Já existe um veículo com esta placa neste condomínio',
  'vehicle.model.invalid': 'Informe o modelo, com 2 a 60 caracteres',
  'vehicle.color.invalid': 'Informe a cor, com 2 a 30 caracteres',
  'vehicle.unit_required': 'Escolha a unidade do veículo',
  'vehicle.version.required': 'Recarregue a lista antes de continuar',
  'vehicle.page.invalid': 'Página inválida. Recarregue a lista',
  'vehicle.condominium_required': 'Cadastre o condomínio antes de cadastrar veículos',
  'identity.user.not_manager': 'Só o síndico pode cadastrar veículos',
  'request.invalid': 'Não foi possível ler os dados enviados. Confira o formato e tente de novo',
};

/** Cadastro e edição (dentro do diálogo): quando algo mudou por trás, a pessoa fecha a janela e confere a lista. */
export const VEHICLE_FORM_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict':
    'Este veículo foi alterado em outro lugar. Feche esta janela e confira a lista, que já foi atualizada',
  'vehicle.not_found':
    'Este veículo não existe mais. Feche esta janela e confira a lista, que já foi atualizada',
  'unit.not_found': 'Esta unidade não existe mais. Feche esta janela e escolha outra unidade',
  'unit.deactivated': 'Esta unidade foi desativada. Feche esta janela e escolha outra unidade',
};

/** Exclusão (na própria lista, que já foi atualizada quando o aviso aparece). */
export const VEHICLE_ACTION_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'vehicle.not_found': 'Este veículo não existe mais. A lista já foi atualizada',
};
