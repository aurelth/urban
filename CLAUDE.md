# Urban: memória do projeto

Leia este arquivo inteiro antes de criar ou alterar qualquer coisa neste repositório. Se algo aqui conflitar com o que o Aurel pedir na conversa, vale o pedido dele; avise e proponha atualizar este arquivo.

## O que é

Urban (https://urban-pro.io) é um sistema de gestão condominial que substitui as planilhas da síndica do condomínio do Aurel. Ela será a beta-tester do MVP. Roadmap em 3 fases (75 módulos, ver "Escopo e roadmap"), com recursos de IA na fase 2. O primeiro módulo é Identity (identidade e autenticação).

## Escopo e roadmap (75 módulos em 3 fases)

A síndica é a beta-tester. A Fase 1 (MVP) tem que permitir abandonar as planilhas no dia 1, e cada fase termina com release em produção. O plano original tinha 66 módulos (documento Urban-03). Em 22/10/2026 o Aurel decidiu mover 4 módulos da Fase 3 para a Fase 1 (modo escuro, backup, consentimento LGPD e log de auditoria) e acrescentar 9 novos: importação das planilhas na Fase 1, 4 na Fase 2 e 4 na Fase 3. Total: Fase 1 com 25, Fase 2 com 27 e Fase 3 com 23.

Papéis do MVP: Admin (a síndica, acesso total) e Morador (só os próprios dados, comunicados, reservas e a versão simplificada do dashboard). A inadimplência é visível só ao Admin.

**Fase 1: MVP (25 módulos)**
- Infraestrutura e autenticação: autenticação (Identity + JWT, access token de 15 min, refresh token de 7 dias com rotação, logout invalida no Redis); perfis e permissões; gestão de usuários (redefinição de senha com token de 1 h); API Gateway (YARP).
- Condomínio: cadastro do condomínio; cadastro de unidades; cadastro de moradores; histórico de ocupação; dependentes; veículos.
- Financeiro: lançamento de despesas (com comprovante); categorias de despesa; competência mensal; geração de cobranças (rateio por fração ideal); registro manual de pagamentos; dashboard financeiro (navegação entre meses e versão do morador); controle de inadimplência; fornecedores (básico).
- Operações: agendamento de manutenção (alertas D-3 e D-1); comunicados internos (SignalR e e-mail).
- Plataforma e compliance: importação das planilhas (CSV e Excel); backup automático diário; LGPD (consentimento com data e versão); log de auditoria imutável; modo escuro (tema claro e escuro à escolha do usuário).

**Fase 2: Essencial, automatizar o manual e IA (27 módulos)**
- Financeiro avançado: boleto e PIX automático (Asaas ou Pagar.me); conciliação automática por webhook; multa e juros automáticos; relatório de inadimplência (aging); exportação de relatórios (PDF e Excel); rateio configurável; acordos e parcelamento de dívidas; prestação de contas (balancete mensal e anual).
- Operações avançadas: registro de ocorrências; ordem de serviço; fornecedores (completo); reserva de áreas comuns; cancelamento e fila de espera; contratos (básico); documentos e vencimentos (convenção, regimento, AVCB e seguro, com alertas de validade); encomendas e visitantes (pré-autorização por QR code).
- Comunicação avançada: notificações multicanal; WhatsApp Business API; mural digital.
- Condomínio avançado: animais de estimação; checklist de vistoria; painel do morador.
- Inteligência artificial (Claude API): assistente para a síndica; previsão de inadimplência; comunicados gerados por IA; análise preditiva de manutenção; relatório mensal por IA.

**Fase 3: Avançado, condomínio 100% digital (23 módulos)**
- Governança: votações e enquetes; assembleia digital; ata automática; painel do conselho; procuração digital.
- Financeiro completo: fundo de reserva; previsão orçamentária anual; conciliação bancária (OFX e CSV); DRE simplificado; nota fiscal eletrônica; consumo individual (água e gás); contas a pagar com aprovação do conselho.
- Operações completas: plano de manutenção preventiva (ABNT NBR 5674); contratos (completo); controle de acesso da portaria; assinatura digital de documentos (ICP-Brasil); infrações e multas; gestão de funcionários; mudanças e obras.
- Segurança e compliance: LGPD (portabilidade e exclusão); 2FA (TOTP, obrigatório para o Admin).
- Plataforma: app mobile (PWA); multi-condomínio (isolamento por tenant; as tabelas de negócio já levam `condominium_id`).

Documentos de planejamento, fora do repositório (pasta `Urban` da Área de Trabalho do Aurel): Urban-01 a Urban-07 (arquitetura, mensageria, módulos, stack, estrutura, CI/CD e modelo de dados). Estão em português e parcialmente desatualizados (PrimeNG virou Angular Material; nomes de módulos, schemas e tabelas em português viram inglês). Em caso de conflito vale este arquivo.

## Como trabalhar com o Aurel

- Idioma: português do Brasil na conversa, na interface, nos commits, PRs e documentação. Documentos gerados se chamam `Urban-…`.
- Ele quer aprender enquanto constrói: explique e justifique cada escolha (tecnologia, arquitetura, como front, back, infra, testes e CI/CD se comunicam), com detalhe suficiente para ele ensinar outras pessoas.
- Guie passo a passo, em texto simples no chat (sem widget de cards), com comandos exatos para o PowerShell que ele já está usando. Tudo o que der para fazer pelo terminal é feito pelo terminal, sem abrir outra janela. Passos só de navegador (Hostinger, SonarQube Cloud) são guiados com capturas de tela.
- Dê o resultado esperado de cada comando (por exemplo, número de linhas de um `git diff --stat`) e peça para ele colar a saída quando algo divergir.
- Nunca peça nem aceite chaves, tokens ou senhas no chat. Segredos entram só por `gh secret set` ou pelo ambiente do GitHub.
- Não mexa nos outros apps do VPS (Orizon e FinanceFlow) sem pedido explícito.

## Princípios de engenharia

SOLID, Clean Code, padrões de projeto, CQRS, Clean Architecture, desempenho, escala, reuso e facilidade de manutenção e de troca de tecnologia. Prefira soluções simples e justificadas a soluções genéricas demais.

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

- Backend: .NET 10 / C# 14, monolito modular, EF Core 10 + PostgreSQL 18, MediatR 12.5.0 + FluentValidation (CQRS), ASP.NET Identity + JWT, BCrypt.Net, Hangfire, SignalR, Redis 8, RabbitMQ + MassTransit 8.5.10, Garage (S3), YARP (gateway), Scalar (documentação da API), Brevo (e-mail).
- Observabilidade: Serilog + Seq/Loki, OpenTelemetry, Jaeger, Prometheus, Grafana.
- Frontend: Angular 22 + TypeScript 6, Angular Material 22 (Material 3), NgRx Signals, Tailwind 4, SCSS, GSAP, ícones Material Symbols locais.
- Testes: xUnit + Moq + FluentAssertions 7.2.2, NetArchTest, Testcontainers, Jest 30, Cypress 16.
- Entrega: GitHub Actions, GHCR, SonarQube Cloud, Dependabot, Semantic Release, Docker, VPS Hostinger com nginx e certbot.
- Congelados de propósito (licença): MediatR 12.5.0, MassTransit 8.5.10, FluentAssertions 7.2.2. O Dependabot ignora major dessas e de Moq. Não atualize por conta própria.
- Não use PrimeNG (foi trocado pelo Angular Material) nem Kafka (foi trocado por RabbitMQ).

## Arquitetura

- Solução `Urban.slnx`, versões de pacotes centralizadas em `Directory.Packages.props`, SDK fixado em `global.json`.
- Projetos de entrada: `Urban.Api`, `Urban.Gateway`, `Urban.Workers`, mais `Urban.SharedKernel`.
- Módulos em `src/Modules/<Module>/Urban.<Module>.{Domain,Application,Infrastructure}`: Identity, Condominium, Finance, Operations.
- Regras de dependência (verificadas por NetArchTest em `tests/Urban.ArchTests`): Domain não depende de nada; Application depende só de Domain; Infrastructure depende de Application; módulos não referenciam as camadas internas uns dos outros e se comunicam por contratos e eventos de integração (RabbitMQ).
- CQRS: comandos e consultas separados (MediatR), validação com FluentValidation em pipeline, regras de negócio no Domain.
- Testes: `tests/Urban.UnitTests`, `tests/Urban.IntegrationTests` (WebApplicationFactory), `tests/Urban.ArchTests`; E2E do frontend em `urban-web/cypress`.

## Modelo de dados

- PostgreSQL 18 com um schema por módulo (`identity`, `condominium`, `finance`, `operations`) e o do `hangfire`; tabelas e colunas em `snake_case` e em inglês.
- Chaves primárias `uuid` geradas no .NET com `Guid.CreateVersion7()`; dinheiro em `numeric(14,2)`; datas em `timestamptz` (UTC).
- Referência entre módulos só por ID, sem chave estrangeira física; dentro do módulo, chaves estrangeiras normais.
- Cada módulo que publica eventos tem suas tabelas `outbox_message`, `outbox_state` e `inbox_state` (MassTransit EF Core Outbox).
- Pessoa e usuário são conceitos separados: nem toda pessoa tem acesso ao sistema (`person` 1 : 0..1 `user`, ligados por ID entre módulos).
- O histórico de ocupação da unidade fica em `occupancy` (data de início e fim por vínculo); nunca apague o registro de quem saiu.

## Convenções de código

- C#: tipos explícitos em vez de `var` quando o tipo não é óbvio (regra IDE0008 do Sonar), `await app.RunAsync()`, nullable habilitado, funções puras extraídas para poderem ser testadas.
- Cobertura de código novo mínima de 80% (gate do Sonar). Arquivos sem lógica (`Program.cs`, `AssemblyReference.cs`, `*.routes.ts`) já estão excluídos da cobertura.
- Angular: zoneless, componentes OnPush e standalone, estado com NgRx Signals. Tailwind só para o layout dos próprios elementos; a aparência dos componentes do Material vem dos tokens do tema, nunca de CSS por cima.
- Textos da interface em português do Brasil, em sentença (só a primeira letra maiúscula), verbo no começo dos botões, sem ponto final em rótulos e títulos.
- Identificadores do código em inglês (classes, métodos, tabelas, endpoints, módulos e pastas do frontend). Interface, documentação, commits e PRs seguem em português do Brasil. Termos do negócio sem tradução óbvia entram no glossário abaixo.
- Nunca desative o sanitizador do Angular sem validar a entrada e documentar o motivo (ver `core/theme/icons.ts`).

## Glossário: negócio e código

- Condomínio = `Condominium`; unidade = `Unit`; morador = `Resident`; proprietário = `Owner`; síndico = `Manager`.
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
- Endpoints de apoio: `GET /health` e `GET /api/version`.

## PowerShell 5.1 (terminal do Aurel)

- Funções `New-RepoFile` e `Complete-Pr` ficam em `~\urban-helpers.ps1`, carregado pelo perfil.
- Não coloque `else` em linha separada no prompt; cole blocos em uma linha. Não use placeholders como `NN` em comandos: use o número real. Aspas duplas dentro de argumentos de programas nativos quebram; use filtros `jq` sem aspas ou JSON gravado em arquivo com `--input`.
- `Get-Content` precisa de `-Encoding UTF8` para acentos. Use `curl.exe`. Mensagens de commit passam por pipe em ASCII.
- `gh pr checks --watch` logo depois de um push pode mostrar a rodada anterior ou uma lista incompleta: espere e repita.
