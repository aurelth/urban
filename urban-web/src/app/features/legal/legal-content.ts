export type LegalDocument = 'terms' | 'privacy';

export interface LegalSection {
  readonly title: string;
  readonly paragraphs: readonly string[];
}

export interface LegalContent {
  readonly title: string;
  readonly sections: readonly LegalSection[];
}

/**
 * Versão do texto abaixo. Ao mudar o texto, mude também a versão vigente na API
 * (`Consent:CurrentVersion`, no appsettings.json da Urban.Api).
 */
export const LEGAL_VERSION = '2026-10-26';

const TERMS: LegalContent = {
  title: 'Termos de uso',
  sections: [
    {
      title: '1. Sobre o Urban',
      paragraphs: [
        'O Urban é um sistema de gestão condominial: ajuda o síndico a cuidar de moradores, finanças e manutenção do condomínio em um só lugar.',
        'Estes termos explicam as regras para usar o Urban. Ao criar sua conta, você declara que leu e aceita este texto.',
      ],
    },
    {
      title: '2. Quem pode usar',
      paragraphs: [
        'O cadastro é para síndicos maiores de 18 anos que respondem pela gestão de um condomínio e têm poderes para isso.',
      ],
    },
    {
      title: '3. Sua conta',
      paragraphs: [
        'Informe dados verdadeiros e mantenha-os atualizados. A senha é pessoal e secreta: não a compartilhe.',
        'Avise-nos assim que perceber qualquer uso indevido da sua conta.',
      ],
    },
    {
      title: '4. Uso permitido',
      paragraphs: [
        'Use o Urban apenas para a gestão do seu condomínio e de acordo com a lei. Não é permitido tentar acessar dados de outros condomínios, burlar medidas de segurança nem sobrecarregar o sistema de propósito.',
      ],
    },
    {
      title: '5. Dados do condomínio e dos moradores',
      paragraphs: [
        'Você decide quais dados de moradores e do condomínio registrar. Registre apenas o necessário e tenha base legal para isso, como prevê a Lei Geral de Proteção de Dados (LGPD).',
        'O Urban guarda e trata esses dados para prestar o serviço, seguindo as suas instruções e a Política de privacidade.',
      ],
    },
    {
      title: '6. Disponibilidade e mudanças',
      paragraphs: [
        'O Urban está em evolução e pode ter instabilidades ou mudanças de funcionalidades. Procuramos avisar antes de mudanças que afetem o seu uso.',
      ],
    },
    {
      title: '7. Suspensão e encerramento',
      paragraphs: [
        'Podemos suspender ou encerrar contas que violem estes termos. Você pode pedir o encerramento da sua conta quando quiser.',
      ],
    },
    {
      title: '8. Limite de responsabilidade',
      paragraphs: ['[A definir com assessoria jurídica antes do lançamento]'],
    },
    {
      title: '9. Mudanças nestes termos',
      paragraphs: [
        'Quando este texto mudar de forma relevante, publicaremos uma nova versão e pediremos um novo aceite. A versão e a data ficam no topo desta página.',
      ],
    },
    {
      title: '10. Contato',
      paragraphs: ['[E-mail de contato a definir]'],
    },
  ],
};

const PRIVACY: LegalContent = {
  title: 'Política de privacidade',
  sections: [
    {
      title: '1. Para quem é esta política',
      paragraphs: [
        'Explica como o Urban trata os dados de quem usa a plataforma. Em relação aos dados de moradores que o síndico cadastra, o síndico decide a finalidade e o Urban os trata para prestar o serviço.',
      ],
    },
    {
      title: '2. Dados que coletamos',
      paragraphs: [
        'Da sua conta: e-mail e senha. A senha é guardada de forma irreversível (hash), e nem a nossa equipe consegue lê-la.',
        'De segurança e uso: data e hora de acessos, endereço IP e informações do navegador.',
        'Do consentimento: a versão dos termos e da política que você aceitou e quando.',
      ],
    },
    {
      title: '3. Para que usamos',
      paragraphs: [
        'Criar e manter sua conta, proteger o sistema contra abusos, enviar e-mails do próprio serviço (confirmação de cadastro e redefinição de senha) e cumprir obrigações legais.',
      ],
    },
    {
      title: '4. Base legal',
      paragraphs: [
        'Usamos seus dados para executar o contrato do serviço, para o nosso interesse legítimo de segurança, para cumprir obrigações legais e, quando for o caso, com o seu consentimento.',
      ],
    },
    {
      title: '5. Com quem compartilhamos',
      paragraphs: [
        'Não vendemos seus dados. Eles passam por fornecedores que nos ajudam a operar o serviço, como a hospedagem dos servidores e o envio de e-mails, sempre para essa finalidade.',
      ],
    },
    {
      title: '6. Por quanto tempo guardamos',
      paragraphs: [
        'Enquanto a conta estiver ativa e pelo tempo exigido por lei. [Prazos a definir com assessoria jurídica]',
      ],
    },
    {
      title: '7. Seus direitos',
      paragraphs: [
        'Pela LGPD, você pode pedir confirmação do tratamento, acesso, correção, anonimização ou eliminação dos dados, portabilidade, informação sobre compartilhamento e a revogação do consentimento.',
      ],
    },
    {
      title: '8. Segurança',
      paragraphs: [
        'A comunicação com o Urban é criptografada (HTTPS) e a sessão usa um cookie protegido, que o JavaScript do navegador não consegue ler.',
      ],
    },
    {
      title: '9. Cookies e armazenamento',
      paragraphs: [
        'Usamos apenas o necessário: um cookie para manter você conectado e o armazenamento local do navegador para lembrar o tema claro ou escuro. Não usamos cookies de publicidade nem de rastreamento.',
      ],
    },
    {
      title: '10. Mudanças e contato',
      paragraphs: [
        'Quando esta política mudar de forma relevante, publicaremos uma nova versão e pediremos um novo aceite.',
        '[Encarregado de dados e e-mail de contato a definir]',
      ],
    },
  ],
};

export const LEGAL_CONTENT: Readonly<Record<LegalDocument, LegalContent>> = {
  terms: TERMS,
  privacy: PRIVACY,
};
