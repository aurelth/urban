import { MessagesByCode } from '../../core/http/problem-message';

/**
 * Texto da tela para cada código de recusa que o cadastro, a edição e os vínculos do morador podem receber da API. O formulário já
 * confere a maioria antes de enviar; estes textos valem quando a API discorda (ou quando a tela está desatualizada).
 */
export const RESIDENT_FORM_MESSAGES: MessagesByCode = {
  'condominium.person.full_name_invalid': 'Informe o nome e o sobrenome',
  'condominium.cpf.invalid': 'Informe um CPF válido',
  'condominium.cpf.already_registered': 'Este CPF já está cadastrado neste condomínio',
  'condominium.birth_date.invalid': 'Informe uma data de nascimento válida',
  'condominium.resident.under_age': 'O morador precisa ter 18 anos ou mais',
  'condominium.phone.invalid': 'Informe um telefone válido, com DDD',
  'condominium.email.invalid': 'Informe um e-mail válido',
  'condominium.profession.invalid': 'Informe a profissão, com 2 a 80 caracteres',
  'condominium.emergency_contact.name_invalid':
    'Informe o nome e o sobrenome do contato de emergência',
  'condominium.emergency_contact.relationship_invalid':
    'Informe o parentesco do contato de emergência, com 2 a 40 caracteres',
  'condominium.emergency_contact.phone_invalid':
    'Informe um telefone válido, com DDD, para o contato de emergência',
  'condominium.occupancy.kind_invalid': 'Escolha se a pessoa é proprietária ou inquilina',
  'condominium.occupancy.start_date_invalid':
    'Informe uma data de entrada válida, até 1 ano à frente',
  'condominium.occupancy.already_active':
    'Este morador já mora nesta unidade. Veja o vínculo atual na lista',
  'condominium.occupancy.end_date_invalid': 'A data de saída deve ficar entre a entrada e hoje',
  'condominium.occupancy.already_ended': 'Este vínculo já foi encerrado. Veja a lista atualizada',
  'condominium.occupancy.not_found': 'Este vínculo não existe mais. Recarregue a página',
  'unit.not_found': 'Esta unidade não existe mais. Recarregue a página e escolha outra',
  'unit.deactivated': 'Esta unidade foi desativada. Recarregue a página e escolha outra',
  'condominium.version.required': 'Recarregue a página antes de continuar',
  'identity.user.not_manager': 'Só o síndico pode cadastrar moradores',
  'request.invalid': 'Não foi possível ler os dados enviados. Confira o formato e tente de novo',
};
