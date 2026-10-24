# Urban: memória do projeto

Leia este arquivo inteiro antes de criar ou alterar qualquer coisa neste repositório. Se algo aqui conflitar com o que o Aurel pedir na conversa, vale o pedido dele; avise e proponha atualizar este arquivo.

## O que é

Urban (https://urban-pro.io) é um sistema de gestão condominial que substitui as planilhas da síndica do condomínio do Aurel e é uma plataforma para vários condomínios (multi-condomínio desde o MVP). Ela será a beta-tester do MVP. Roadmap em 3 fases (77 módulos, ver "Escopo e roadmap"), com recursos de IA na fase 2. O primeiro módulo é Identity (identidade e autenticação).

## Escopo e roadmap (77 módulos em 3 fases)

A síndica é a beta-tester. A Fase 1 (MVP) tem que permitir abandonar as planilhas no dia 1, e cada fase termina com release em produção. O plano original tinha 66 módulos (documento Urban-03). Em 22/10/2026 o Aurel decidiu mover 4 módulos da Fase 3 para a Fase 1 (modo escuro, backup, consentimento LGPD e log de auditoria) e acrescentar 9 novos: importação das planilhas na Fase 1, 4 na Fase 2 e 4 na Fase 3. Total naquela data: Fase 1 com 25, Fase 2 com 27 e Fase 3 com 23. Em 23/10/2026 ele pediu que os síndicos possam se cadastrar sozinhos (multi-condomínio desde o MVP), fatura em PDF com envio automático, chamados e reclamações dos moradores e um painel da plataforma para ele: multi-condomínio sai da Fase 3, registro de ocorrências sai da Fase 2, e fatura em PDF e painel da plataforma entram como módulos novos. Total atual: Fase 1 com 29, Fase 2 com 26 e Fase 3 com 22 (77 módulos).

Papéis do MVP: `SuperAdmin` (o Aurel, dono da plataforma), `Manager` (síndico) e `Resident` (morador). Detalhes na seção "Papéis, condomínios e acesso". A inadimplência é visível só ao `Manager`.

**Fase 1: MVP (29 módulos)**
- Infraestrutura e autenticação: autenticação (Identity + JWT, access token de 15 min, refresh token de 7 dias com rotação, logout invalida no Redis); perfis e permissões (`SuperAdmin`, `Manager` e `Resident`); gestão de usuários (cadastro do síndico, convite de moradores, suspensão e exclusão, redefinição de senha com token de 1 h); API Gateway (YARP).
- Condomínio: cadastro do condomínio; cadastro de unidades; cadastro de moradores; histórico de ocupação; dependentes; veículos.
- Financeiro: lançamento de despesas (com comprovante); categorias de despesa; competência mensal; geração de cobranças (rateio por fração ideal); fatura em PDF com envio automático por e-mail; registro manual de pagamentos; dashboard financeiro (navegação entre meses e versão do morador); controle de inadimplência; fornecedores (básico).
- Operações: agendamento de manutenção (alertas D-3 e D-1); comunicados internos (categorias manutenção, importante e geral; SignalR e e-mail); registro de ocorrências (chamados de conserto e reclamações de barulho; quem reclama não aparece para o reclamado).
- Plataforma e compliance: importação das planilhas (CSV e Excel); backup automático diário; LGPD (consentimento com data e versão); log de auditoria imutável; modo escuro (tema claro e escuro à escolha do usuário); multi-condomínio (isolamento por tenant); painel da plataforma (SuperAdmin: condomínios e síndicos, suspender, reativar e excluir).

A Fase 1 sai em duas releases. A 1a leva o que a síndica precisa para largar as planilhas: identidade, cadastro do síndico, condomínio, unidades, moradores, despesas, cobranças, fatura em PDF, comunicados e o isolamento por tenant. A 1b leva ocorrências, painel da plataforma, importação das planilhas, backup e log de auditoria.

**Fase 2: Essencial, automatizar o manual e IA (26 módulos)**
- Financeiro avançado: boleto e PIX automático (Asaas ou Pagar.me); conciliação automática por webhook; multa e juros automáticos; relatório de inadimplência (aging); exportação de relatórios (PDF e Excel); rateio configurável; acordos e parcelamento de dívidas; prestação de contas (balancete mensal e anual).
- Operações avançadas: ordem de serviço; fornecedores (completo); reserva de áreas comuns; cancelamento e fila de espera; contratos (básico); documentos e vencimentos (convenção, regimento, AVCB e seguro, com alertas de validade); encomendas e visitantes (pré-autorização por QR code).
- Comunicação avançada: notificações multicanal; WhatsApp Business API; mural digital.
- Condomínio avançado: animais de estimação; checklist de vistoria; painel do morador.
- Inteligência artificial (Claude API): assistente para a síndica; previsão de inadimplência; comunicados gerados por IA; análise preditiva de manutenção; relatório mensal por IA.

**Fase 3: Avançado, condomínio 100% digital (22 módulos)**
- Governança: votações e enquetes; assembleia digital; ata automática; painel do conselho; procuração digital.
- Financeiro completo: fundo de reserva; previsão orçamentária anual; conciliação bancária (OFX e CSV); DRE simplificado; nota fiscal eletrônica; consumo individual (água e gás); contas a pagar com aprovação do conselho.
- Operações completas: plano de manutenção preventiva (ABNT NBR 5674); contratos (completo); controle de acesso da portaria; assinatura digital de documentos (ICP-Brasil); infrações e multas; gestão de funcionários; mudanças e obras.
- Segurança e compliance: LGPD (portabilidade e exclusão); 2FA (TOTP, obrigatório para o `SuperAdmin` e o `Manager`).
- Plataforma: app mobile (PWA).

Documentos de planejamento, fora do repositório (pasta `Urban` da Área de Trabalho do Aurel): Urban-01 a Urban-07 (arquitetura, mensageria, módulos, stack, estrutura, CI/CD e modelo de dados). Estão em português e parcialmente desatualizados (PrimeNG virou Angular Material; nomes de módulos, schemas e tabelas em português viram inglês). Em caso de conflito vale este arquivo.

## Papéis, condomínios e acesso

Decidido com o Aurel em 22 e 23/10/2026.

- Papéis no código (e na interface): `SuperAdmin` (Super administrador, o Aurel, dono da plataforma, sem condomínio), `Manager` (Síndico) e `Resident` (Morador).
- `Manager` pode tudo o que `Resident` pode: as políticas da área do morador aceitam os dois papéis. O que aparece nessa área (faturas, chamados) vem da ocupação da pessoa, não do papel. A fatura é por unidade, então a unidade do síndico entra no rateio e na emissão como qualquer outra, sem regra especial. Síndico profissional pode não morar no prédio: a unidade no cadastro é opcional.
- Multi-condomínio (tenant) desde o MVP: todo usuário, exceto o `SuperAdmin`, pertence a um condomínio. `condominium_id` fica em toda tabela de negócio e no JWT, e um filtro global do EF Core aplica o tenant atual. Cache, jobs do Hangfire e eventos carregam o tenant. Cada módulo de negócio tem teste de isolamento: o usuário do condomínio A não enxerga nada do B.
- Cadastro do síndico (por conta própria, sem convite): (1) Identity: nome, e-mail, senha e aceite dos termos e da política de privacidade (alimenta o consentimento LGPD); a conta nasce `pending_verification`; (2) confirmação por e-mail torna a conta `active`, com acesso imediato; (3) depois do login, o módulo Condominium pede nome, CNPJ e endereço do condomínio (CNPJ já cadastrado dá conflito: pedir convite ao síndico atual); (4) unidade do síndico, opcional. O endpoint de cadastro tem limite de requisições. O `SuperAdmin` também pode criar síndicos.
- Moradores são cadastrados pelo síndico, que dispara um convite com link para definir a senha (token de 1 h, com reenvio).
- Estados da conta: `pending_verification`, `active`, `deactivated` (feito pelo síndico, que também reativa) e `suspended` (feito pelo `SuperAdmin`, só ele reativa). Excluir é anonimizar o login: e-mail trocado, hash da senha apagado e `deleted_at` preenchido; o `id` permanece para a auditoria e o e-mail fica livre. Mudanças de estado gravam `status_changed_at`, `status_changed_by` e `status_reason`.
- Quem age sobre quem é uma função pura no Domain, `AccessPolicy.CanManage(actor, target)`, que considera o tenant. `SuperAdmin`: cria síndicos e suspende, reativa e exclui síndicos e moradores. `Manager`: cria, desativa, reativa e exclui moradores do próprio condomínio. `Resident`: nenhum. Ninguém age sobre si mesmo. O `SuperAdmin` nasce só no bootstrap (primeiro start, com link para definir a senha), nunca pela API.
- Excluir ou suspender o síndico age só na conta dele: o condomínio e seus dados financeiros ficam, e os moradores continuam acessando. Status de condomínio e troca de síndico (`ChangeManager`) são módulos futuros, ainda sem fase.
- Login: e-mail inexistente e senha errada dão o mesmo erro. O estado da conta só aparece depois da senha correta (`email_not_verified`, `account_deactivated`, `account_suspended`). Cinco falhas bloqueiam a conta por 15 min (423). Senha com no mínimo 10 caracteres, com pelo menos 1 letra e 1 dígito.
- Tokens: access token de 15 min, guardado só em memória no frontend; refresh token de 7 dias em cookie `HttpOnly; Secure; SameSite=Strict` com path `/api/auth`, com rotação e detecção de reuso (reuso revoga todos os tokens do usuário). Logout e suspensão revogam pelo Redis (`jti` do token e instante de revogação por usuário).
- O `SuperAdmin` não acessa dados de negócio dos condomínios (menor privilégio); as rotas de finanças e moradores exigem `Manager` ou `Resident`. Suporte por "assumir sessão", com auditoria, é módulo futuro.
- Painel da plataforma: módulo `Platform`, só leitura, que lista cada condomínio com o síndico associado a partir de contratos de Identity e Condominium. As ações de suspender, reativar e excluir ficam nele.

## Como trabalhar com o Aurel

- Idioma: português do Brasil na conversa, na interface, nos commits, PRs e documentação. Documentos gerados se chamam `Urban-…`.
- Ele quer aprender enquanto constrói: explique e justifique cada escolha (tecnologia, arquitetura, como front, back, infra, testes e CI/CD se comunicam), com detalhe suficiente para ele ensinar outras pessoas.
- Guie passo a passo, em texto simples no chat (sem widget de cards), com comandos exatos para o PowerShell que ele já está usando. Tudo o que der para fazer pelo terminal é feito pelo terminal, sem abrir outra janela. Passos só de navegador (Hostinger, SonarQube Cloud) são guiados com capturas de tela.
- Dê o resultado esperado de cada comando (por exemplo, número de linhas de um `git diff --stat`) e peça para ele colar a saída quando algo divergir.
- Nunca peça nem aceite chaves, tokens ou senhas no chat. Segredos entram só por `gh secret set` ou pelo ambiente do GitHub.
- Não mexa nos outros apps do VPS (Orizon e FinanceFlow) sem pedido explícito.
- Fatias verticais: cada funcionalidade é entregue por inteiro, com o backend primeiro (contrato e testes, documentado no Scalar) e a tela logo em seguida, contra a API real. Nunca um módulo inteiro de backend e só depois todo o frontend.

## Princípios de engenharia

SOLID, Clean Code, padrões de projeto, CQRS, Clean Architecture, desempenho, escala, reuso e facilidade de manutenção e de troca de tecnologia. Prefira soluções simples e justificadas a soluções genéricas demais.

## Qualidade do código: sem duplicação, reuso e extensibilidade

O Aurel pediu que estas regras sejam seguidas à risca em todo PR.

- Um lugar para cada coisa: o que mais de um módulo usa mora em `Urban.SharedKernel` (`Entity`, `AggregateRoot`, `ValueObject`, `Result<T>` e `Error`, `IClock`, `ICurrentUser`, `ITenantContext`, `PagedQuery` e `PagedResult<T>`). Antes de criar um tipo ou helper, procure se já existe.
- Preocupações transversais são escritas e testadas uma vez: behaviors do MediatR (validação, log, transação nos comandos, cache nas consultas), filtro global de tenant, tradução de `Result` para HTTP (ProblemDetails), paginação e auditoria. Handlers e endpoints não repetem esse código.
- Regra dos três: na terceira repetição, extraia. Em Domain e Application a duplicação nunca passa de duas ocorrências; o teto de 3% do Sonar não é meta.
- Organização por funcionalidade dentro de cada camada: `Features/<Feature>/` com comando ou consulta, handler e validator juntos. Funcionalidade nova é pasta nova, sem mexer no que já existe (aberto para extensão, fechado para modificação). Handlers e validators são registrados por varredura do assembly, cada módulo expõe um único `Add<Module>Module(...)`, os endpoints ficam em `Map<Module>Endpoints(...)` e as políticas de autorização usam constantes.
- Endpoints finos: só traduzem HTTP para comando ou consulta e de volta; nenhuma regra de negócio neles.
- Testes também sem duplicação: builders e fakes compartilhados (`FakeClock`, builders de entidades), uma única fábrica da API e containers do Testcontainers iniciados uma vez por coleção.
- Desempenho por padrão: as metas de "Qualidade da interface e desempenho" valem em cada consulta e tela (paginação, projeção, índices, sem N+1, cache). Otimização se decide medindo, não supondo.
- Frontend: componentes reutilizáveis em `shared/ui`, um único cliente HTTP com interceptors (token, erros), stores por feature, nenhum componente copiado entre features e estilos só pelos tokens do tema.
- Reuso sim, abstração genérica demais não (ver "Princípios de engenharia").
- Checklist no corpo de todo PR de código: existe algo parecido que dava para reaproveitar? Houve duplicação? Alguma consulta sem paginação ou com N+1? A funcionalidade exigiu mexer em código antigo? O comportamento novo nasceu de um teste?

## Desenvolvimento orientado a testes (TDD)

Decidido em 22/10/2026: todo comportamento novo nasce de um teste que falha.

- Ciclo: vermelho (escreve o teste e vê falhar pelo motivo certo), verde (o mínimo de código para passar) e refatorar (com os testes verdes).
- Backend: Domain e Application começam por testes unitários (xUnit, Moq, FluentAssertions; handlers e validators sem banco). Endpoints e persistência começam por testes de integração (WebApplicationFactory e Testcontainers com PostgreSQL, Redis e RabbitMQ reais). Uma regra de arquitetura nova começa por um teste do NetArchTest.
- Frontend: Jest primeiro para stores (NgRx Signals), serviços, guards, pipes e componentes; fluxos completos no Cypress, escrevendo o cenário antes da tela.
- Bug: primeiro um teste que reproduz o defeito e falha; depois a correção.
- Ao guiar o Aurel, mostre sempre o teste falhando (com a saída esperada) antes de escrever o código que o faz passar.
- No PR todos os testes passam; o vermelho acontece localmente. Cobertura de 80% no código novo é piso, não objetivo: o teste vale pelo comportamento que protege.
- O que o TDD não cobre: aparência (revisão visual nos dois temas e nos tamanhos 360, 768 e 1280 px) e infraestrutura (validada rodando de verdade).

## Stack (lista oficial)

- Backend: .NET 10 / C# 14, monolito modular, EF Core 10 + PostgreSQL 18, MediatR 12.5.0 + FluentValidation (CQRS), JWT (sem ASP.NET Identity), BCrypt.Net, Mapperly, Hangfire, SignalR, Redis 8, RabbitMQ + MassTransit 8.5.10, Garage (S3), YARP (gateway), Scalar (documentação da API), Brevo (e-mail).
- Observabilidade: Serilog + Seq/Loki, OpenTelemetry, Jaeger, Prometheus, Grafana.
- Frontend: Angular 22 + TypeScript 6, Angular Material 22 (Material 3), NgRx Signals, Tailwind 4, SCSS, GSAP, ícones Material Symbols locais.
- Testes: xUnit + Moq + FluentAssertions 7.2.2, NetArchTest, Testcontainers, Jest 30, Cypress 16.
- Entrega: GitHub Actions, GHCR, SonarQube Cloud, Dependabot, Semantic Release, Docker, VPS Hostinger com nginx e certbot.
- Congelados de propósito (licença): MediatR 12.5.0, MassTransit 8.5.10, FluentAssertions 7.2.2. O Dependabot ignora major dessas e de Moq. Não atualize por conta própria.
- Não use PrimeNG (foi trocado pelo Angular Material) nem Kafka (foi trocado por RabbitMQ).

## Arquitetura

- Solução `Urban.slnx`, versões de pacotes centralizadas em `Directory.Packages.props`, SDK fixado em `global.json`.
- Projetos de entrada: `Urban.Api`, `Urban.Gateway`, `Urban.Workers`, mais `Urban.SharedKernel`.
- Módulos em `src/Modules/<Module>/Urban.<Module>.{Domain,Application,Infrastructure}`: Identity, Condominium, Finance, Operations e, quando começar o painel da plataforma, Platform (só leitura, sem tabelas próprias).
- Regras de dependência (verificadas por NetArchTest em `tests/Urban.ArchTests`): Domain não depende de nada; Application depende só de Domain; Infrastructure depende de Application; módulos não referenciam as camadas internas uns dos outros e se comunicam por contratos e eventos de integração (RabbitMQ).
- CQRS: comandos e consultas separados (MediatR), validação com FluentValidation em pipeline, regras de negócio no Domain.
- Testes: `tests/Urban.UnitTests`, `tests/Urban.IntegrationTests` (WebApplicationFactory), `tests/Urban.ArchTests`; E2E do frontend em `urban-web/cypress`.
- Identity: o EF Core mapeia direto o `User` do Domain, sem ASP.NET Identity (`IdentityUser`/`UserManager`). Motivo: o Domain não depende de framework, e o que o ASP.NET Identity traria (hash de senha, bloqueio por tentativas, token de redefinição) é pouco e fica em código nosso, pequeno e testado. BCrypt, bloqueio e JWT ficam na Infrastructure, atrás de portas definidas na Application (`IUserAccounts`, `IPasswordHasher`, `IRefreshTokenRepository`, `ITokenIssuer`, `ISessionRevocations`, `ISessionRevocationChecker`, `IEmailSender`, `IClock`). Domain e Application não referenciam EF Core, Npgsql, BCrypt, `Microsoft.IdentityModel` nem StackExchange.Redis (NetArchTest).
- Identity, senha e bloqueio: o hash BCrypt, a contagem de falhas e o fim do bloqueio ficam em `identity.user_credential`, tabela própria separada de `user` (o `User` não carrega segredo e a tabela que muda a cada login é pequena). A regra (5 falhas bloqueiam por 15 minutos, falha durante o bloqueio não o prorroga) é do Domain (`UserCredential`); `UserAccounts` só lê e grava. A coluna `xmin` do PostgreSQL é o token de concorrência: duas tentativas simultâneas não perdem a contagem (até 3 conferências por chamada). O BCrypt usa `EnhancedHashPassword` com SHA-384 (senhas acima de 72 bytes não são truncadas) e custo configurável em `PasswordHashing:WorkFactor` (padrão 12). E-mail inexistente gasta o mesmo tempo de uma conferência (`VerifyDummy`). Compromisso aceito: conta bloqueada responde `locked_out` (423) antes de conferir a senha, o que revela que o e-mail existe depois de 5 tentativas; em troca o atacante não aprende nada sobre a senha durante o bloqueio.
- Identity, revogação de sessão: o Redis guarda duas chaves, ambas com TTL para nada se acumular. `identity:revoked-token:{jti}` recusa um token de acesso até a hora em que ele expiraria (logout); `identity:revoked-user:{id}` guarda o instante (em segundos) até o qual todo token do usuário é recusado, com TTL igual à vida do token de acesso (suspensão, reuso de refresh token). Um token é recusado se foi emitido até esse instante, inclusive (na dúvida, recusa), e uma revogação mais antiga nunca desfaz uma mais nova (script Lua que só grava se o valor for maior). A validação faz um único `MGET` por requisição. Redis fora do ar: a API recusa as requisições autenticadas (falha fechada: preferimos indisponibilidade a aceitar um token revogado).
- Identity, API de autenticação: `POST /api/auth/login` e `POST /api/auth/refresh` (MediatR). O token de acesso vai no corpo; o de renovação, só no cookie `__Secure-urban-refresh` (HttpOnly, Secure, SameSite=Strict, Path=/api/auth), e as respostas levam `Cache-Control: no-store`. Falhas viram `application/problem+json` (RFC 9457) com o campo estável `code`; a tabela código→status está em `Urban.Api/Http/ErrorHttp.cs` (401 credenciais ou sessão inválidas, 423 bloqueio, 403 estado da conta só depois da senha) e um teste varre todos os `Error` do módulo para garantir que nenhum fique sem status.
- Configuração da API (variáveis de ambiente com `__`): `ConnectionStrings:Postgres`, `ConnectionStrings:Redis`, `Jwt:SigningKey` (base64, mínimo 32 bytes; vem do secret `JWT_SIGNING_KEY`), `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenLifetime`, `PasswordHashing:WorkFactor` e `Database:MigrateOnStartup` (ligado no deploy; o EF Core usa bloqueio no banco). Configuração ausente ou inválida derruba a partida, não o primeiro login.
- Documentação do contrato: OpenAPI em `/openapi/v1.json` e Scalar em `/scalar/v1`, fora do ambiente Production. Mapeamento de DTOs com Mapperly em `Urban.Api/Mapping`: campo de origem sem destino quebra o build (RMG020) até alguém decidir com `MapperIgnoreSource`.
- Identity, rotas protegidas: o JwtBearer valida o token de acesso (`AccessTokenValidation`): só HS256, emissor, público e validade, sem tolerância de relógio, porque a chave de revogação no Redis expira junto com o token. Cada requisição autenticada consulta `ISessionRevocationChecker` (um único `MGET`); token revogado ou ilegível vira 401 `identity.session.unauthenticated` com `WWW-Authenticate: Bearer`, e rotas públicas ignoram um `Authorization` inválido. `RedisException` vira `SessionStoreUnavailableException` na Infrastructure e a API responde 503 `identity.session.store_unavailable` (falha fechada: nunca aceita token sem conseguir checar a revogação); login e renovação seguem funcionando com o Redis fora do ar. `POST /api/auth/logout` exige o token, revoga o de acesso e o de renovação do cookie, apaga o cookie e sempre responde 204 para quem está autenticado.

## Modelo de dados

- PostgreSQL 18 com um schema por módulo (`identity`, `condominium`, `finance`, `operations`) e o do `hangfire`; tabelas e colunas em `snake_case` e em inglês.
- Chaves primárias `uuid` geradas no .NET com `Guid.CreateVersion7()`; dinheiro em `numeric(14,2)`; datas em `timestamptz` (UTC).
- Migrations: ferramenta local `dotnet ef` (manifesto `dotnet-tools.json` na raiz, restaurado com `dotnet tool restore`). Cada módulo guarda as suas em `Persistence/Migrations` da Infrastructure e o histórico em `<schema>.__ef_migrations_history`. Para criar: `dotnet ef migrations add <Nome> --project <Infrastructure do módulo> --startup-project src/Urban.Api --output-dir Persistence/Migrations`. Migrations são código gerado (fora do Sonar e das regras de estilo); nunca edite uma já publicada, crie outra.
- Enums são gravados como texto (o nome do valor), nunca como número; a forma `snake_case` dos estados (`pending_verification`) é só a da API.
- Referência entre módulos só por ID, sem chave estrangeira física; dentro do módulo, chaves estrangeiras normais.
- Cada módulo que publica eventos tem suas tabelas `outbox_message`, `outbox_state` e `inbox_state` (MassTransit EF Core Outbox).
- Pessoa e usuário são conceitos separados: nem toda pessoa tem acesso ao sistema (`person` 1 : 0..1 `user`, ligados por ID entre módulos).
- O histórico de ocupação da unidade fica em `occupancy` (data de início e fim por vínculo); nunca apague o registro de quem saiu.
- Multi-condomínio: toda tabela de negócio leva `condominium_id` (tenant), aplicado por filtro global do EF Core. `identity.user` leva `condominium_id` (nulo só para o `SuperAdmin`), `status`, `status_changed_at`, `status_changed_by`, `status_reason` e `deleted_at`.

## Convenções de código

- C#: tipos explícitos em vez de `var` quando o tipo não é óbvio (regra IDE0008 do Sonar), `await app.RunAsync()`, nullable habilitado, funções puras extraídas para poderem ser testadas.
- Cobertura de código novo mínima de 80% (gate do Sonar). Arquivos sem lógica (`Program.cs`, `AssemblyReference.cs`, `*.routes.ts`) já estão excluídos da cobertura.
- Angular: zoneless, componentes OnPush e standalone, estado com NgRx Signals. Tailwind só para o layout dos próprios elementos; a aparência dos componentes do Material vem dos tokens do tema, nunca de CSS por cima.
- Textos da interface em português do Brasil, em sentença (só a primeira letra maiúscula), verbo no começo dos botões, sem ponto final em rótulos e títulos.
- Identificadores do código em inglês (classes, métodos, tabelas, endpoints, módulos e pastas do frontend). Interface, documentação, commits e PRs seguem em português do Brasil. Termos do negócio sem tradução óbvia entram no glossário abaixo.
- Nunca desative o sanitizador do Angular sem validar a entrada e documentar o motivo (ver `core/theme/icons.ts`).
- Comentários: o mínimo possível. Só quando explicam um porquê que o código não mostra (restrição externa, armadilha, decisão não óbvia). Nome claro e função pequena substituem comentário; sem resumo XML que repita o nome do membro. A documentação do contrato HTTP fica nos metadados dos endpoints (Scalar), não em comentários.
- Mapeamento entre objetos (entidade para DTO, comando para entidade): Mapperly, que gera o código na compilação, sem reflexão. Não use AutoMapper (licença comercial nas versões novas, o mesmo motivo do MediatR congelado). Mapeadores ficam na camada que conhece os dois tipos, são `partial` e têm teste; nas consultas de leitura prefira `Select` direto para o DTO. O pacote entra junto com o primeiro DTO.

## Glossário: negócio e código

- Condomínio = `Condominium`; unidade = `Unit`; morador = `Resident`; proprietário = `Owner`; síndico = `Manager`; super administrador (dono da plataforma) = `SuperAdmin`.
- Cobrança = `Charge`; fatura (PDF da cobrança) = `Invoice`; ocorrência (chamado, reclamação) = `Incident`; comunicado = `Announcement`; condomínio como inquilino do sistema = tenant.
- Termos novos entram aqui no início de cada módulo, antes de virar código.

## Interface (identidade visual)

Estilo Apple: limpo, minimalista, muito espaço em branco, bordas finas, um único acento. Paleta "Índigo suave":

| Papel | Claro |
| --- | --- |
| Fundo | `#F6F6FA` |
| Superfície (cards) | `#FFFFFF` |
| Texto | `#1C1C1E` |
| Texto secundário | `#65657A` |
| Destaque (cor-semente do Material 3) | `#5856D6`, texto sobre ele `#FFFFFF` |
| Linhas e bordas | `#E6E6EF` |
| Sucesso / pago | `#248A3D` |
| Alerta / pendente | `#B25000` |
| Erro / atrasado | `#D70015` |

O modo escuro é gerado pelo Material 3 a partir da cor-semente (destaque mais claro, fundos grafite). Estados nunca dependem só de cor: use também texto ou ícone.

## Qualidade da interface e desempenho

A interface tem que ser extremamente bonita e rápida. Isso é critério de aceite de cada tela, não acabamento deixado para o fim. As metas numéricas abaixo foram propostas em 22/10/2026 e podem ser ajustadas pelo Aurel.

- Beleza: espaçamento em escala de 4 px, hierarquia tipográfica clara, estados vazio, carregando (skeleton, sem spinner solto) e erro sempre desenhados, microanimações sutis de 150 a 300 ms só com `transform` e `opacity` (tokens `--urban-duration-*`), respeitando `prefers-reduced-motion`.
- Dois temas, claro e escuro, escolhidos pelo usuário (claro, escuro ou do sistema); cada tela é conferida nos dois e nos tamanhos 360, 768 e 1280 px, com a base pensada para celular.
- Acessibilidade: contraste mínimo WCAG AA, foco visível, uso completo por teclado, estados nunca só por cor.
- Metas de velocidade (Lighthouse, celular, rede 4G): LCP até 2,5 s, INP até 200 ms, CLS até 0,1; bundle inicial até 200 kB comprimido, com `budgets` no `angular.json` para o build falhar se estourar.
- Frontend rápido: rotas carregadas sob demanda por feature, `@defer` em blocos pesados (gráficos), `NgOptimizedImage`, listas longas com paginação no servidor e virtual scroll, sem polling (tempo real por SignalR), e nenhuma biblioteca nova sem checar o peso.
- API rápida: consultas com p95 até 200 ms; paginação obrigatória, `AsNoTracking` e projeção direta para DTO nas consultas, sem N+1, índices desenhados junto com a tabela, cache no Redis para dashboards, compressão no nginx.
- Verificação: `budgets` no CI desde já; Lighthouse CI entra no CI quando as primeiras telas reais existirem.

## Git e CI

- `main` (produção) e `develop` (integração) protegidas. Branches de trabalho saem da `develop` e seguem `^(feature|fix|chore|docs|hotfix)/.+`. Nunca use `ci/…`.
- Commits e títulos de PR em Conventional Commits: `^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-z0-9-]+\))?!?: .{1,100}$` (escopo em minúsculas).
- PR de trabalho para `develop`: squash e apagar a branch. `develop` para `main`: merge commit, sem apagar a `develop`.
- Checks obrigatórios: `pr-title`, `branch-policy`, `build-test`, `web-build-test` e `web-e2e`. O Sonar também roda, mas não é obrigatório. Só faça o merge com todos verdes e confira que a lista está completa.
- Em CI use `npm ci --ignore-scripts` e binários locais (`./node_modules/.bin/...`) em vez de `npx`.
- Commits e PRs não levam rodapés de co-autoria, links de sessão nem menção a ferramentas de IA.

## Entrega e infraestrutura

- Push na `develop` publica imagens `sha-<7>` no GHCR e faz deploy em https://staging.urban-pro.io. Push na `main` roda o Semantic Release (tags `vX.Y.Z`, começando em 0.x) e, com versão nova, faz deploy em https://urban-pro.io depois de aprovação manual.
- Código de deploy em `infra/deploy/` (compose, `deploy.sh` com health check e rollback, modelos de nginx). Portas do Urban no VPS: 5200 (produção) e 5210 (staging), só em 127.0.0.1.
- Dockerfiles em `infra/`; compose local em `infra/docker-compose.yml` com perfis `tools` e `observability`.
- Segredos do deploy (`POSTGRES_PASSWORD`, `REDIS_PASSWORD`, `JWT_SIGNING_KEY`) ficam nos secrets do Environment do GitHub (`staging`, `production`), entram por `gh secret set --env`, viajam por ssh (entrada padrão) até `secrets.env` no VPS e o `deploy.sh` os junta ao `.env`. Postgres e Redis do deploy não publicam portas; os dados ficam nos volumes `postgres-data` e `redis-data`, então nunca use `docker compose down -v` nesses ambientes.
- Endpoints de apoio: `GET /health` e `GET /api/version`.

## PowerShell 5.1 (terminal do Aurel)

- Funções `New-RepoFile` e `Complete-Pr` ficam em `~\urban-helpers.ps1`, carregado pelo perfil.
- Não coloque `else` em linha separada no prompt; cole blocos em uma linha. Não use placeholders como `NN` em comandos: use o número real. Aspas duplas dentro de argumentos de programas nativos quebram; use filtros `jq` sem aspas ou JSON gravado em arquivo com `--input`.
- `Get-Content` precisa de `-Encoding UTF8` para acentos. Use `curl.exe`. Mensagens de commit passam por pipe em ASCII.
- `gh pr checks --watch` logo depois de um push pode mostrar a rodada anterior ou uma lista incompleta: espere e repita.
