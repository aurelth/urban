import { MessagesByCode } from '../../../core/http/problem-message';

/** Texto da tela para cada código que o cadastro do condomínio pode receber da API. */
export const REGISTER_CONDOMINIUM_MESSAGES: MessagesByCode = {
  'condominium.cpf.invalid': 'Informe um CPF válido',
  'condominium.cnpj.invalid': 'Informe um CNPJ válido',
  'condominium.cnpj.already_registered':
    'Este CNPJ já está cadastrado. Peça um convite ao síndico atual',
  'condominium.postal_code.invalid': 'Informe um CEP válido',
  'condominium.phone.invalid': 'Informe um telefone válido, com DDD',
  'condominium.phone.mobile_required': 'Informe um celular com DDD',
  'condominium.person.full_name_invalid': 'Informe o nome e o sobrenome',
  'condominium.birth_date.invalid': 'Informe uma data de nascimento válida',
  'condominium.birth_date.under_age': 'O síndico precisa ter 18 anos ou mais',
  'condominium.name.invalid': 'Informe o nome do condomínio, com 2 a 120 caracteres',
  'condominium.address.street_invalid': 'Informe a rua',
  'condominium.address.number_invalid': 'Informe o número (use S/N quando não houver)',
  'condominium.address.complement_invalid': 'O complemento deve ter até 60 caracteres',
  'condominium.address.neighborhood_invalid': 'Informe o bairro',
  'condominium.address.city_invalid': 'Informe a cidade',
  'condominium.address.state_invalid': 'Informe a UF',
  'identity.display_name.invalid': 'Informe como prefere ser chamado, com 2 a 40 letras',
  'identity.user.not_manager': 'Só o síndico pode cadastrar o condomínio',
  'request.invalid': 'Não foi possível ler os dados enviados. Confira o formato e tente de novo',
};

/** O síndico já tem condomínio (a tela estava desatualizada): basta renovar a sessão e seguir. */
export const MANAGER_ALREADY_REGISTERED = 'condominium.manager.already_registered';
