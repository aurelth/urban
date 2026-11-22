import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { ANNOUNCEMENT_DELETE_MESSAGES, ANNOUNCEMENT_FORM_MESSAGES } from './announcements.messages';

function refused(code: string): ApiProblem {
  return { status: 422, code, detail: 'detalhe da API', retryAfterSeconds: null };
}

const FIELD_CODES = [
  'announcement.title.invalid',
  'announcement.body.invalid',
  'announcement.category.invalid',
  'announcement.scope.invalid',
  'announcement.block.invalid',
  'announcement.unit.required',
  'announcement.block.not_found',
  'announcement.unit.not_found',
  'announcement.page.invalid',
  'operations.version.required',
  'operations.condominium_not_found',
  'identity.user.not_manager',
  'request.invalid',
  'concurrency.conflict',
  'announcement.not_found',
];

describe('mensagens dos comunicados', () => {
  it.each(FIELD_CODES)(
    'o código "%s" tem texto próprio no diálogo de gravar e no de excluir',
    (code) => {
      const generic = 'Algo deu errado. Tente novamente em instantes';

      expect(describeProblem(refused(code), ANNOUNCEMENT_FORM_MESSAGES)).not.toBe(generic);
      expect(describeProblem(refused(code), ANNOUNCEMENT_DELETE_MESSAGES)).not.toBe(generic);
    },
  );

  it('comunicado já apagado tem texto diferente ao gravar e ao excluir', () => {
    expect(
      describeProblem(refused('announcement.not_found'), ANNOUNCEMENT_FORM_MESSAGES),
    ).toContain('não existe mais');
    expect(
      describeProblem(refused('announcement.not_found'), ANNOUNCEMENT_DELETE_MESSAGES),
    ).toContain('já foi apagado');
  });

  it('um código desconhecido cai na mensagem genérica', () => {
    expect(describeProblem(refused('outra.coisa'), ANNOUNCEMENT_FORM_MESSAGES)).toBe(
      'Algo deu errado. Tente novamente em instantes',
    );
  });
});
