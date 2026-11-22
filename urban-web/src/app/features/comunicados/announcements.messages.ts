import { MessagesByCode } from '../../core/http/problem-message';
import {
  ANNOUNCEMENT_BODY_MAX,
  ANNOUNCEMENT_TITLE_MAX,
} from '../../core/announcements/announcements.models';

/** Texto da tela para cada código de recusa que os comunicados podem receber da API. */
const FIELD_MESSAGES: MessagesByCode = {
  'announcement.title.invalid': `Informe o título, em uma linha, com até ${ANNOUNCEMENT_TITLE_MAX} caracteres`,
  'announcement.body.invalid': `Informe o texto, com até ${ANNOUNCEMENT_BODY_MAX} caracteres`,
  'announcement.category.invalid': 'Escolha a categoria do comunicado',
  'announcement.scope.invalid': 'Escolha para quem vai o comunicado',
  'announcement.block.invalid': 'Escolha o bloco',
  'announcement.unit.required': 'Escolha a unidade',
  'announcement.block.not_found': 'Este bloco não tem unidades cadastradas',
  'announcement.unit.not_found': 'Esta unidade não existe neste condomínio',
  'announcement.page.invalid': 'Página inválida. Recarregue a lista',
  'operations.version.required': 'Recarregue a lista antes de continuar',
  'operations.condominium_not_found': 'Cadastre o condomínio antes de enviar comunicados',
  'identity.user.not_manager': 'Só o síndico pode enviar comunicados',
  'request.invalid': 'Não foi possível ler os dados enviados. Confira o formato e tente de novo',
};

/** Criar e editar (dentro do diálogo): quando o comunicado mudou, a pessoa fecha a janela e confere a lista. */
export const ANNOUNCEMENT_FORM_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict':
    'Este comunicado foi alterado em outro lugar. Feche esta janela e confira a lista, que já foi atualizada',
  'announcement.not_found':
    'Este comunicado não existe mais. Feche esta janela e confira a lista, que já foi atualizada',
};

/** Apagar (dentro do diálogo de confirmação). */
export const ANNOUNCEMENT_DELETE_MESSAGES: MessagesByCode = {
  ...FIELD_MESSAGES,
  'concurrency.conflict':
    'Este comunicado foi alterado em outro lugar. Feche esta janela e confira a lista, que já foi atualizada',
  'announcement.not_found':
    'Este comunicado já foi apagado. Feche esta janela e confira a lista, que já foi atualizada',
};
