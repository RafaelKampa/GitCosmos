# GitCosmos — Contexto e Decisões do Projeto

> Documento de referência para continuidade do desenvolvimento do GitCosmos.
>
> **Status:** fundação inicial da Milestone 1 implementada; validação final de testes e cobertura a executar manualmente pelo usuário.
>
> **Última atualização:** 20/11/2026.

---

## 1. Identificação do projeto

| Campo | Valor |
|---|---|
| Nome | GitCosmos |
| Repositório | https://github.com/RafaelKampa/GitCosmos |
| Diretório local Windows | `C:\Repos\GitCosmos` |
| Visibilidade | Público |
| Modelo de desenvolvimento | Open source |
| Estrutura do GitHub | Um único repositório (monorepo) |
| Plataforma inicial | Windows |
| Execução inicial | Aplicação web totalmente local |
| Status atual | Fundação inicial implementada; validação manual final pendente |

**Descrição em inglês:**

A local-first Git client for managing multiple repositories in a unified workspace.

**Slogan sugerido:**

One workspace. A universe of repositories.

### Decisão sobre o nome

O nome **GitCosmos** foi escolhido após avaliar alternativas como GitOrbit, GitHive, RepoCosmos, EnxameGit e EnxameFlow.

O nome representa a ideia de um universo de repositórios independentes, organizados em um ambiente unificado.

Foi identificado um usuário `@GitCosmos` no GitHub, mas não um produto concorrente diretamente relacionado. A disponibilidade de marca no INPI e de domínios comerciais não foi confirmada.

### Decisão sobre a organização GitHub

O projeto começará no GitHub pessoal do desenvolvedor, sem organização dedicada.

Motivos:

- Facilitar a administração inicial.
- Dar visibilidade ao projeto no portfólio pessoal.
- Evitar complexidade desnecessária.
- Permitir transferência futura para uma organização, caso o projeto cresça.

---

## 2. Motivação e objetivo

O GitCosmos nasceu da necessidade de gerenciar diversos repositórios Git relacionados a um mesmo sistema.

Em arquiteturas de microsserviços, um único produto pode possuir vários repositórios independentes, por exemplo:

- Serviço de autenticação.
- Serviço de pedidos.
- Serviço de produtos.
- Serviço de campanhas.
- Serviço de relatórios.
- Frontend administrativo.
- Frontend do usuário.

Ferramentas tradicionais de interface gráfica Git normalmente trabalham com um repositório por vez.

O objetivo do GitCosmos é permitir que esses repositórios sejam agrupados em **workspaces lógicos**, possibilitando visualização unificada e operações coordenadas.

### Problema principal

Executar manualmente operações repetitivas em vários repositórios, como:

- Fetch.
- Pull.
- Push.
- Checkout de branches.
- Consulta de status.
- Verificação de alterações pendentes.

### Proposta

Uma ferramenta que centraliza essas operações sem alterar a independência dos repositórios.

**Escopo confirmado do MVP:** o GitCosmos também será um cliente Git de uso diário, com as funções básicas esperadas de um programa como o Fork. O usuário deverá conseguir trabalhar em um repositório individual, além de coordenar vários repositórios no mesmo workspace. As operações coletivas são um diferencial sobre essa base funcional.

**O GitCosmos não transforma os repositórios em um monorepo Git.**

Cada repositório continuará possuindo:

- Histórico próprio.
- Branches próprias.
- Remotes próprios.
- Credenciais próprias.
- Diretório local próprio.

O workspace é apenas um agrupamento lógico gerenciado pelo GitCosmos.

---

## 3. Público-alvo

O público inicial são desenvolvedores que trabalham com vários repositórios relacionados.

Exemplos:

- Desenvolvedores full stack.
- Equipes que utilizam microsserviços.
- Desenvolvedores que trabalham com múltiplos frontends e backends.
- Engenheiros de software.
- Arquitetos de software.
- Profissionais que mantêm sistemas distribuídos em diversos repositórios.

O projeto também possui um objetivo de portfólio: demonstrar competências em engenharia de software, arquitetura, integração entre tecnologias e desenvolvimento de ferramentas para desenvolvedores.

---

## 4. Princípios arquiteturais

### 4.1. Local-first

**Decisão confirmada.**

A primeira versão será executada inteiramente no computador do usuário.

Não haverá dependência de:

- Hospedagem Azure.
- Servidor centralizado.
- Banco de dados remoto.
- Autenticação em um serviço próprio do GitCosmos.
- Infraestrutura cloud para funcionamento da aplicação.

A conexão com a internet será necessária somente para operações que dependam de remotes Git ou outros serviços externos.

### 4.2. Monólito modular

**Decisão confirmada.**

O backend será um monólito modular, e não uma arquitetura de microsserviços.

A separação lógica entre camadas deve permitir evolução, testes e substituição de implementações sem introduzir complexidade operacional desnecessária.

**Decisão confirmada: Clean Architecture no backend**, reproduzindo a arquitetura, a organização de pastas e o padrão de código do projeto `C:\Repos\Latam\Blux.LATAM.Auth`. O monólito modular descreve o modelo de execução; Clean Architecture define a separação de responsabilidades e dependências internas. O detalhamento obrigatório está na seção 13.

**Frontend componentizado:** utilizar a separação arquitetural de `C:\Repos\Latam\Blux.LATAM.Admin.Frontend` como referência, adaptada ao Vue 3. A organização de pastas é um requisito de arquitetura e deverá ser respeitada desde a fundação.

### 4.3. Monorepo do código-fonte

**Decisão confirmada.**

O frontend e o backend do GitCosmos ficarão no mesmo repositório GitHub.

Isso facilita:

- Versionamento.
- Releases.
- Build.
- Integração contínua.
- Documentação.
- Distribuição da aplicação.

Essa decisão não deve ser confundida com a funcionalidade do produto, que gerencia múltiplos repositórios independentes.

### 4.4. Independência dos repositórios gerenciados

**Decisão confirmada.**

O GitCosmos nunca deve assumir que os repositórios de um workspace compartilham o mesmo histórico, branch ou remote.

Cada operação precisa considerar as particularidades de cada repositório.

---

## 5. Stack tecnológica

### 5.1. Backend

**Tecnologias definidas:**

- .NET SDK 10, com target framework `net10.0` nos projetos backend.
- C#.
- ASP.NET Core Web API.
- Entity Framework Core.
- SQLite.
- SignalR.
- Git CLI.
- `System.Diagnostics.Process` para execução dos comandos Git.
- FluentValidation para validação dos requests.
- OpenAPI nativo e Scalar para documentação da API, seguindo a referência Auth.

O backend será responsável por:

- Gerenciar workspaces.
- Persistir configurações.
- Identificar repositórios Git locais.
- Consultar informações Git.
- Executar operações individuais e coordenadas.
- Controlar concorrência.
- Registrar resultados.
- Comunicar progresso ao frontend.

**Decisão confirmada do usuário:** utilizar .NET SDK 10. Fixar uma versão concreta da linha 10 em `global.json` durante a fundação e usar a mesma configuração no desenvolvimento e na CI. A versão principal já está definida; a seleção do patch será feita na configuração do ambiente.

### 5.2. Frontend

**Tecnologias definidas:**

- Vue.js 3.
- TypeScript.
- Vite.
- Pinia.
- PrimeVue.

Vue Router será utilizado para organizar as rotas e os layouts das features, adaptando a separação do Admin Frontend. A estrutura componentizada está definida na seção 13.

O frontend será responsável por:

- Interface de gerenciamento dos workspaces.
- Dashboard dos repositórios.
- Seleção dos repositórios envolvidos nas operações.
- Visualização do estado Git.
- Acompanhamento de operações.
- Exibição de erros e resultados.

### 5.3. Banco de dados

**Decisão confirmada: SQLite.**

O banco será um arquivo local.

O SQLite armazenará dados de configuração e gerenciamento do GitCosmos, como:

- Workspaces.
- Repositórios cadastrados.
- Caminhos locais.
- Configurações da aplicação; preferências de tema e idioma ficam no navegador (localStorage).
- Configurações de branches.
- Histórico de operações.
- Resultados das operações.

O SQLite **não armazenará o conteúdo dos repositórios nem substituirá o banco interno do Git**.

Os commits, arquivos, branches e históricos continuam nos próprios repositórios.

PostgreSQL foi considerado, mas descartado para o MVP por exigir infraestrutura desnecessária.

Uma futura edição com colaboração centralizada poderá avaliar outro banco de dados. A migração entre provedores do Entity Framework Core exigirá cuidados com esquema, migrations e dados.

---

## 6. Execução e distribuição

### 6.1. Plataforma inicial

**Windows.**

### 6.2. Modelo de execução

O backend ASP.NET Core será iniciado localmente.

Ele deverá:

1. Inicializar os serviços necessários.
2. Abrir uma porta HTTP local.
3. Servir o frontend Vue compilado.
4. Expor os endpoints da API.
5. Disponibilizar a comunicação SignalR.

A interface será acessada pelo navegador em um endereço semelhante a:

`http://localhost:<porta>`

A aplicação não dependerá de um servidor remoto.

### 6.3. Git instalado no computador

O GitCosmos utilizará inicialmente o Git CLI já instalado na máquina do usuário.

A ferramenta deverá verificar se o Git está disponível e informar adequadamente caso não esteja.

### 6.4. Autenticação Git

O GitCosmos deverá aproveitar os mecanismos de autenticação já configurados no ambiente do usuário, como Git Credential Manager e SSH.

Não deverá armazenar senhas ou tokens Git diretamente no SQLite.

### 6.5. Empacotamento

A distribuição inicial deverá contemplar um pacote Windows com runtime .NET incluído (*self-contained*), evitando exigir a instalação separada do runtime pelo usuário.

A estratégia exata de instalador, executável e inicialização automática do navegador ainda será definida.

### 6.6. Desenvolvimento nativo e execução integrada

**Decisão confirmada após avaliar Docker:** manter o desenvolvimento diretamente no Windows, sem Dockerfile ou Compose como requisito. Isso preserva o acesso direto aos repositórios locais, ao Git e à autenticação já configurada no ambiente do usuário.

Durante desenvolvimento, executar backend com `dotnet watch` em `127.0.0.1:5210` e frontend com `npm run dev` (Node/Vite) em `127.0.0.1:5173`. O Vite encaminha `/api` para o backend e permite atualização da interface ao editar os arquivos.

Na execução integrada, `scripts/Build.ps1` compila o Vue em arquivos estáticos e os copia para `GitCosmos.API/wwwroot`; a API pode então servir backend e frontend na porta `5210`. Vue roda no navegador; Node é necessário para desenvolvimento/build, mas não precisa permanecer em execução para servir a interface compilada. `dotnet run` sozinho não recompila o frontend. Os comandos completos estão no README.

---

### 6.7. Debug do frontend no VS Code

Abrir a pasta C:\Repos\GitCosmos\src\frontend no VS Code. As configurações locais em src/frontend/.vscode/launch.json permitem Run → Start Debugging (F5) com Chrome ou Edge. Os perfis principais executam a tarefa GitCosmos: start Vite, definida em tasks.json, que chama npm run dev, espera a disponibilidade em 127.0.0.1:5173 e então abre o navegador com source maps e webRoot do frontend. No Windows, a tarefa usa npm.cmd.

Perfis Vite already running permitem depurar sem iniciar outro servidor quando npm run dev já está ativo. Manter o backend executando separadamente pelo Rider ou dotnet watch na porta 5210. As configurações não instalam dependências nem executam testes. A tarefa Vite permanece ativa após parar a sessão do navegador; encerrá-la pelo terminal dedicado com Ctrl+C ou Terminal → Terminate Task. A validação real de breakpoints/F5 no editor será feita manualmente pelo usuário; a criação da configuração não comprova essa execução.

## 7. Modelo de workspaces

### 7.1. Conceito

Um workspace é um agrupamento lógico de repositórios Git independentes.

Exemplo:

**Workspace: Sistema de Microsserviços**

| Repositório | Diretório local |
|---|---|
| Authentication | `C:\Repos\Sistema\Auth` |
| Orders | `C:\Repos\Sistema\Orders` |
| Products | `C:\Repos\Sistema\Products` |
| Campaigns | `C:\Repos\Sistema\Campaigns` |
| Admin Frontend | `C:\Repos\Sistema\Admin` |
| Customer Frontend | `C:\Repos\Sistema\Web` |

O usuário poderá selecionar um workspace para visualizar e operar seus repositórios.

### 7.2. Operações de gerenciamento

O MVP deverá permitir:

- Criar workspaces.
- Renomear workspaces.
- Excluir workspaces.
- Adicionar repositórios Git locais existentes.
- Remover repositórios de um workspace.
- Validar se o caminho informado corresponde a um repositório Git válido.
- Persistir as configurações no SQLite.

Excluir um workspace ou remover um repositório de um workspace **não deve excluir automaticamente os arquivos do repositório no disco**.

### 7.3. Repositórios em múltiplos workspaces

A arquitetura deverá permitir que um mesmo repositório local participe de mais de um workspace.

É importante evitar que operações simultâneas realizadas em workspaces diferentes modifiquem o mesmo repositório ao mesmo tempo.

### 7.4. Armazenamento local

Inicialmente, os workspaces existirão somente no computador onde foram criados.

Não haverá sincronização automática entre computadores.

---

## 8. Funcionalidades do MVP

O MVP deverá contemplar tanto as funções básicas de um cliente Git gráfico quanto o gerenciamento de múltiplos repositórios. Fork é uma referência de experiência e abrangência funcional básica; não implica reproduzir integralmente todas as suas funcionalidades ou interface.

As funções individuais detalhadas abaixo são requisitos do MVP, inclusive clonagem, visualização de diferenças e consulta de commits, anteriormente previstas como evolução futura.

### 8.1. Dashboard de repositórios

O GitCosmos deverá apresentar uma visão unificada dos repositórios pertencentes ao workspace selecionado.

Informações previstas:

- Nome do repositório.
- Caminho local.
- Branch atual.
- Remote principal.
- Existência de alterações não commitadas.
- Status do working tree.
- Commits à frente do remote (*ahead*).
- Commits atrás do remote (*behind*).
- Estado da última operação executada.

A interface deve facilitar a identificação de repositórios que necessitam de atenção.

### 8.2. Fetch All

Executar `git fetch` nos repositórios selecionados.

Deverá:

- Permitir seleção de repositórios.
- Exibir progresso individual.
- Registrar sucesso ou falha por repositório.
- Não interromper necessariamente toda a operação quando um repositório falhar.

### 8.3. Pull All

Executar pull nos repositórios selecionados.

**Decisão de segurança:** utilizar `--ff-only` como comportamento padrão.

Isso evita merges automáticos inesperados.

Se não for possível realizar fast-forward, o GitCosmos deverá informar o problema e não forçar a atualização.

### 8.4. Push All

Executar push nos repositórios selecionados.

Deverá respeitar:

- Branch atual.
- Remote configurado.
- Permissões existentes.
- Autenticação configurada no Git.

Não executar `--force` automaticamente.

### 8.5. Checkout All

Permitir selecionar uma branch desejada e tentar realizar checkout em cada repositório selecionado.

**Decisão importante:** uma branch pode existir em alguns repositórios e não existir em outros.

Nesse caso:

- Repositórios compatíveis podem concluir a operação.
- Repositórios incompatíveis devem retornar falha individual.
- O sistema não deve criar automaticamente branches ausentes.
- O usuário deve receber um resumo claro.

### 8.6. Seleção de repositórios

As operações coletivas não precisam necessariamente envolver todos os repositórios do workspace.

O usuário poderá selecionar quais participarão de cada operação.

### 8.7. Abertura, criação e clonagem de repositórios

- Abrir repositórios locais existentes.
- Inicializar um repositório Git em um diretório local.
- Clonar um repositório remoto para um diretório escolhido pelo usuário.
- Adicionar o repositório aberto, criado ou clonado a um workspace.
- Validar caminhos e impedir sobrescrita indevida de arquivos existentes.

### 8.8. Alterações locais, staging e commits

- Listar arquivos modificados, adicionados, removidos e não rastreados.
- Visualizar diferenças do working tree e do staging, com identificação de arquivos binários.
- Adicionar e remover arquivos do staging, individualmente ou em conjunto.
- Selecionar trechos de alterações textuais para staging e unstage.
- Criar commits com mensagem e visualizar seu resultado.
- Alterar o último commit (amend), com indicação das consequências quando já publicado.
- Descartar alterações locais por arquivo ou conjunto selecionado, mediante ação explícita e confirmação de perda de dados.
- Consultar e editar regras de ignore no `.gitignore`.

### 8.9. Histórico, diferenças e autoria

- Exibir histórico de commits com grafo de branches e identificação de tags e remotes.
- Consultar autor, data, mensagem, SHA e arquivos alterados de um commit.
- Visualizar diferenças de commits, arquivos e comparações entre referências Git.
- Pesquisar e filtrar o histórico por mensagem, autor, referência e caminho.
- Consultar histórico de um arquivo e autoria por linha (blame) em arquivos textuais.

### 8.10. Branches, tags e remotes

- Listar, criar, renomear e excluir branches locais.
- Fazer checkout de branches locais e criar uma branch local a partir de uma branch remota.
- Configurar o upstream de uma branch.
- Listar, criar e excluir tags locais, incluindo tags anotadas.
- Listar, adicionar, editar e remover remotes.
- Executar fetch, pull e push em um único repositório, com seleção apropriada de remote e branch.
- Publicar branches e tags e excluir branches ou tags remotas por ação explícita.
- Aplicar as proteções de pull com `--ff-only` por padrão e ausência de force push automático também às operações individuais.

### 8.11. Stash e integração de alterações

- Criar e listar stashes, consultar seu conteúdo, aplicar, executar pop e excluir entradas.
- Executar merge e rebase de branches no repositório selecionado.
- Aplicar commits selecionados por cherry-pick.
- Reverter commits com revert e oferecer reset nos modos soft, mixed e hard, com explicação do efeito de cada modo.
- Identificar operações de merge, rebase e cherry-pick em andamento e permitir continuar ou abortar quando aplicável.
- Identificar arquivos em conflito, apresentar diferenças e permitir editar o resultado ou abri-lo em ferramenta externa.
- Permitir marcar conflitos como resolvidos e concluir a operação correspondente.

### 8.12. Limites e proteção das funções individuais

As funções desta seção deverão ser acessíveis pela interface; a execução manual de comandos em um terminal não substitui sua implementação no MVP.

As operações individuais não se tornam automaticamente operações coletivas. O escopo coletivo inicial continua sendo fetch, pull, push e checkout, conforme as seções 8.2 a 8.6.

Operações que descartam dados, removem referências remotas ou reescrevem histórico deverão exigir escolha explícita, pré-validação e confirmação adequada ao impacto. O GitCosmos não deverá executar reset hard, descarte, exclusão forçada ou reescrita de histórico como tentativa automática de resolver uma falha.

---

## 9. Orquestração e consistência das operações Git

### 9.1. Ausência de transação distribuída

**Decisão arquitetural fundamental.**

O Git não oferece uma transação atômica entre múltiplos repositórios independentes.

Portanto, uma operação coletiva pode terminar com:

- Alguns repositórios atualizados.
- Outros não atualizados.
- Alguns com erro de autenticação.
- Outros com conflitos ou branches incompatíveis.

O GitCosmos não deve prometer comportamento de tudo ou nada.

### 9.2. Pré-validação

Antes de executar operações que modificam o estado dos repositórios, o GitCosmos deverá verificar condições relevantes, como:

- Repositório acessível.
- Diretório Git válido.
- Branch atual.
- Existência da branch solicitada.
- Alterações locais pendentes.
- Configuração de remotes.
- Possíveis impedimentos conhecidos.

As validações específicas dependem do tipo de operação.

### 9.3. Resultados individuais

Cada repositório deverá possuir um estado de execução próprio, como:

- Pendente.
- Validando.
- Executando.
- Concluído.
- Falhou.
- Cancelado.
- Ignorado.

Os estados exatos poderão ser refinados durante a implementação.

### 9.4. Falhas parciais

Uma falha individual não deverá apagar o histórico de resultados dos demais repositórios.

O usuário deverá identificar quais operações funcionaram e quais falharam.

### 9.5. Reexecução

A arquitetura deverá permitir repetir operações somente nos repositórios que falharam, quando isso for seguro.

### 9.6. Rollback

Não implementar rollback automático de operações Git distribuídas como se fossem uma única transação.

Especialmente após um push remoto, desfazer alterações pode afetar outros desenvolvedores.

Qualquer operação destrutiva deverá exigir uma ação explícita do usuário.

---

## 10. Concorrência

### 10.1. Operações no mesmo repositório

Operações que alteram o estado de um mesmo repositório deverão ser serializadas.

Isso evita executar simultaneamente comandos incompatíveis sobre o mesmo diretório Git.

### 10.2. Operações em repositórios diferentes

Poderão ser executadas em paralelo, respeitando um limite de concorrência configurável ou definido pela aplicação.

O objetivo é equilibrar desempenho, estabilidade e consumo de recursos.

### 10.3. Cancelamento

O sistema deverá oferecer mecanismos de cancelamento seguro de operações em andamento.

O cancelamento não deverá assumir que comandos Git já concluídos podem ser revertidos.

---

## 11. Comunicação em tempo real

**Tecnologia definida: SignalR.**

O backend utilizará SignalR para comunicar ao frontend:

- Início de operações.
- Mudança de status.
- Progresso por repositório.
- Conclusão.
- Falhas.
- Cancelamentos.

O frontend deverá apresentar os resultados de maneira compreensível.

A comunicação em tempo real não substitui a persistência do histórico das operações.

---

## 12. Segurança local

Embora a aplicação seja local, ela executará comandos com acesso aos arquivos e repositórios do computador.

Portanto, segurança continua sendo uma preocupação importante.

Diretrizes definidas:

- Escutar preferencialmente apenas no endereço de loopback (`127.0.0.1`).
- Não expor a API na rede local por padrão.
- Validar origem das requisições.
- Considerar proteção de autenticação/autorização para endpoints sensíveis.
- Não armazenar credenciais Git no SQLite.
- Evitar montagem insegura de comandos por concatenação de strings.
- Validar caminhos de repositórios.
- Controlar quais operações Git poderão ser executadas.
- Não permitir execução arbitrária de comandos do sistema por meio da API.

A estratégia específica de proteção da API local será detalhada durante a implementação.

---

## 13. Clean Architecture, organização de pastas e padrões obrigatórios

### 13.1. Referências analisadas e regra de adoção

Análise realizada em 20/11/2026 sobre os arquivos locais:

- Backend: `C:\Repos\Latam\Blux.LATAM.Auth`.
- Frontend: `C:\Repos\Latam\Blux.LATAM.Admin.Frontend`.

**Decisão do usuário:** o backend deverá seguir exatamente a arquitetura e o padrão de código do Auth, incluindo controllers, services, use cases, repositórios, `CustomResponse`, exceptions e middlewares globais. O frontend deverá ser componentizado e reaproveitar a organização arquitetural do Admin, adaptando os mecanismos Angular para Vue.

São referências concretas do backend: `Controllers/RoleController.cs`, `Controllers/PermissionController.cs`, `Application/Common/CustomResponse.cs`, `Application/Services/RoleService.cs`, `Application/Interfaces/Services/IRoleService.cs`, `Application/UseCases/Roles/`, `Application/Validators/`, `Application/Common/Exceptions/`, `Application/Common/Localization/`, `Infrastructure/Repositories/Repository.cs`, `Infrastructure/Data/AuthDbContext.cs`, os arquivos `DependencyInjection.cs`, `API/Middlewares/` e `API/Program.cs`.

No frontend foram analisados `core/api/api-base-service.ts`, `core/api/api-error.ts`, `core/supplier-type/supplier-type-api.service.ts`, `core/<dominio>/models/requests|responses`, `shared/models`, `features/roles/role-dialog`, `layout/admin-layout` e `app.routes.ts`.

Os padrões devem ser reproduzidos com namespaces e funcionalidades do GitCosmos. SQLite, Git CLI, Vue e execução local continuam sendo decisões confirmadas. Dependências e regras específicas de identidade Blux (PostgreSQL, campanhas, JWT de participantes, e-mail e serviços cloud) não passam a ser requisitos do GitCosmos por fazerem parte das referências.

### 13.2. Estrutura obrigatória do monorepo

A organização abaixo substitui a proposta genérica anterior. Manter a capitalização `API` usada no Auth e organizar DTOs, validações e casos de uso por domínio. As pastas funcionais serão criadas conforme a implementação correspondente.

```text
gitcosmos/
├── src/
│   ├── backend/
│   │   ├── GitCosmos.API/
│   │   │   ├── Controllers/
│   │   │   ├── Middlewares/
│   │   │   ├── Hubs/
│   │   │   ├── Extensions/
│   │   │   ├── Services/
│   │   │   └── Program.cs
│   │   ├── GitCosmos.Application/
│   │   │   ├── Common/
│   │   │   │   ├── Exceptions/
│   │   │   │   ├── Localization/
│   │   │   │   ├── CustomResponse.cs
│   │   │   │   └── PagedResult.cs
│   │   │   ├── DTOs/
│   │   │   │   ├── Requests/<Domain>/
│   │   │   │   └── Responses/<Domain>/
│   │   │   ├── Interfaces/
│   │   │   │   ├── Services/
│   │   │   │   └── Git/
│   │   │   ├── Services/
│   │   │   ├── UseCases/<Domain>/
│   │   │   ├── Validators/<Domain>/
│   │   │   └── DependencyInjection.cs
│   │   ├── GitCosmos.Domain/
│   │   │   ├── Common/
│   │   │   ├── Entities/
│   │   │   ├── Enums/
│   │   │   ├── ReadModels/
│   │   │   └── Interfaces/Repositories/
│   │   └── GitCosmos.Infrastructure/
│   │       ├── Data/
│   │       ├── Migrations/Ef/
│   │       ├── Repositories/
│   │       ├── Git/
│   │       ├── FileSystem/
│   │       └── DependencyInjection.cs
│   │
│   └── frontend/
│       ├── src/
│       │   ├── app/                    # bootstrap, router e configuração
│       │   ├── core/
│       │   │   ├── api/                # cliente HTTP base, ApiError e opções
│       │   │   ├── i18n/translations/  # pt-BR, es e en
│       │   │   ├── realtime/          # conexão e eventos SignalR
│       │   │   └── <domain>/
│       │   │       ├── <domain>-api.service.ts
│       │   │       ├── models/requests/
│       │   │       ├── models/responses/
│       │   │       └── stores/        # estado compartilhado Pinia
│       │   ├── features/<feature>/
│       │   │   ├── <name>-page/
│       │   │   ├── <name>-dialog/
│       │   │   ├── components/        # componentes exclusivos da feature
│       │   │   └── composables/       # lógica exclusiva da feature
│       │   ├── layout/                # shell e navegação
│       │   └── shared/
│       │       ├── components/
│       │       ├── composables/
│       │       ├── models/
│       │       ├── utils/
│       │       ├── validators/
│       │       ├── constants/
│       │       └── styles/
│       └── ...
│
├── tests/
│   ├── GitCosmos.Application.Tests/
│   └── GitCosmos.IntegrationTests/
│
├── docs/
│   ├── architecture/
│   └── decisions/
│
├── scripts/
│
└── README.md
```

### 13.3. Dependências e responsabilidades do backend

Fluxo obrigatório:

```text
Controller -> IService -> Service -> UseCase -> IRepository -> Repository -> EF Core
                                         -> IGit...     -> Git CLI
```

Referências entre projetos, como nos `.csproj` do Auth:

- `Domain`: sem referência aos outros projetos.
- `Application` referencia `Domain`.
- `Infrastructure` referencia `Domain` e `Application`.
- `API` referencia `Application` e `Infrastructure` para composição.
- As implementações de infraestrutura não podem ser chamadas diretamente pelos controllers ou pelos casos de uso; usar contratos nas camadas internas.

**GitCosmos.API**

- Endpoints HTTP.
- Hubs SignalR.
- Configuração e inicialização da aplicação.
- Composição de dependências.
- Middlewares globais, documentação OpenAPI/Scalar e serviços de inicialização.

**GitCosmos.Application**

- Casos de uso.
- Orquestração de operações.
- Contratos e interfaces.
- Regras de fluxo da aplicação.
- `Interfaces/Services`, `Services`, `UseCases`, DTOs de request/response, validators e respostas padronizadas.
- Exceptions de aplicação e localização em `Common`.

**GitCosmos.Domain**

- Entidades.
- Regras centrais de domínio.
- Conceitos de workspace, repositório e operações.
- Contratos de repositório em `Interfaces/Repositories`, enums, read models e paginação em `Common`.

**GitCosmos.Infrastructure**

- EF Core.
- SQLite.
- Execução de comandos Git.
- Acesso ao sistema de arquivos.
- Implementações de contratos de infraestrutura.
- `GitCosmosDbContext` em `Data`, migrations em `Migrations/Ef` e repositórios em `Repositories`.

Reproduzir o padrão de `BaseEntity` (identidade, timestamps UTC, ativação e desativação) e de `Repository<TEntity>`/repositórios especializados onde aplicável à persistência. Adaptar os tipos e mapeamentos PostgreSQL para SQLite. A remoção de um vínculo de workspace não deve remover arquivos Git.

Registrar services em `Application/DependencyInjection.cs` com `AddApplication()` e infraestrutura em `Infrastructure/DependencyInjection.cs` com `AddInfrastructure(...)`, compostos no `Program.cs`. Não introduzir MediatR ou outro fluxo que substitua a cadeia adotada.

### 13.4. Services, use cases e convenções de código

- Interface `I<Domain>Service` em `Interfaces/Services`; implementação `<Domain>Service` em `Services`.
- Controller recebe a interface por injeção usando primary constructor, como no Auth.
- Service delega aos use cases. Nos exemplos concretos de Role e Permission, ele instancia use cases em campos `private readonly` com as dependências recebidas e retorna diretamente suas tasks.
- Os use cases de Role já devolvem `CustomResponse<T>`; o service não adiciona um segundo envelope. O Auth também contém use cases registrados por DI. Reproduzir o fluxo concreto mais próximo e documentar a escolha, sem afirmar que todas as classes da referência usam uma única forma de instanciação.
- Regras, validação e coordenação ficam nos use cases, fora dos controllers e dos repositórios.
- FluentValidation em `Validators/<Domain>`, com o padrão `ValidateAndThrowApp` lançando `ValidationAppException`.
- DTOs separados em `DTOs/Requests/<Domain>` e `DTOs/Responses/<Domain>`; usar records e projeções de entidade para response como `RoleResponse.Projection` quando aplicável.
- Namespaces por arquivo, identificadores em inglês, código assíncrono com sufixo `Async`, nullable habilitado e dependências explícitas. Classes simples e coesas, sem abstrações alternativas ao modelo de referência.
- Receber e encaminhar `CancellationToken` das actions aos services e use cases e às operações canceláveis de Git e persistência. Algumas assinaturas antigas de repositórios do Auth não o recebem; isso não elimina o requisito de cancelamento do GitCosmos.

### 13.5. Contrato CustomResponse e paginação

Adotar o mesmo `CustomResponse<T>` de `Blux.LATAM.Auth.Application/Common/CustomResponse.cs`, no namespace `GitCosmos.Application.Common`, mantendo propriedades, construtores e factories:

| Propriedade C# | JSON | Tipo |
|---|---|---|
| `Success` | `success` | `bool` |
| `Errors` | `errors` | `List<string>`, inicializada vazia |
| `Messages` | `messages` | `List<string>`, inicializada vazia |
| `Result` | `result` | `T?` |
| `TotalRows` | `totalRows` | `int?` |

- Construtor vazio e construtor `(bool success, List<string> errors, T? result, int? totalRows = null, List<string>? messages = null)`.
- Factories `Ok(result, totalRows)`, `Ok(result, message, totalRows)`, `Fail(List<string>, result)`, `Fail(string)`, `OkList(items, totalRows)`, `FailList(List<string>)` e `FailList(string)` com os mesmos comportamentos da referência.
- `OkList` e `FailList` retornam `CustomResponse<List<T>>`; falhas de lista usam lista vazia.
- `TotalRows` é nullable; validar o JSON real do serializer adotado. A referência anota a omissão de nulo com Newtonsoft, mas usa `AddControllers()` e `WriteAsJsonAsync`; o atributo isolado não garante omissão com System.Text.Json.
- Usar `PaginationRequest` em `Domain/Common` (página inicial 0, tamanho padrão 10, mínimo 1 e máximo 100).
- Usar `PagedResult<T>` em `Application/Common`: `Items`, `Page`, `PageSize`, `TotalRows` e `TotalPages` calculado. Retorno paginado: `CustomResponse<PagedResult<T>>`, com `TotalRows` do envelope conforme o exemplo Auth.
- Consultas de lista sem resultados retornam HTTP 200, sucesso, lista vazia e mensagem localizada quando pertinente; não usar 404 para lista vazia.
- Preservar o envelope em cada resposta de negócio JSON; respostas de protocolo, arquivos e eventos SignalR possuem contratos próprios explícitos.

### 13.6. Modelo exato dos controllers

Seguir `[ApiController]`, `[Route("api/[controller]")]`, herança de `ControllerBase`, primary constructor com `IService`, actions `async Task<IActionResult>`, binding explícito e `CancellationToken`. Usar atributo de verbo separado de `[Route("NomeDaOperacao")]`, com rotas de ação como no Auth.

Exemplo normativo adaptado de `RoleController.Create`:

```csharp
[ApiController]
[Route("api/[controller]")]
public class WorkspaceController(IWorkspaceService workspaceService) : ControllerBase
{
    [HttpPost]
    [Route("CreateWorkspace")]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workspaceService.CreateAsync(request, cancellationToken);
        if (!response.Success)
            return BadRequest(response);
        return Ok(response);
    }
}
```

- Escritas seguem a verificação `!response.Success` e o retorno adequado, como `BadRequest(response)`, seguido de `Ok(response)` no sucesso.
- Leituras de Role verificam `Success` e retornam `NotFound(response)` quando falso; leituras como `PermissionController.GetAllPermissions` retornam diretamente `Ok(response)`. Documentar por endpoint qual variante concreta se aplica, preservando HTTP 200 para consultas vazias bem-sucedidas.
- Não introduzir Minimal APIs para substituir os controllers de negócio, `BaseController` com comportamento diferente, retorno cru de entidades, `CreatedAtAction` como novo padrão ou `try/catch` repetido nas actions.
- Exceções são traduzidas globalmente conforme a seção 13.7; autorização local do GitCosmos deverá ser aplicada sem copiar roles e permissões do domínio Blux.
- Documentar endpoints com OpenAPI nativo e Scalar e operation IDs únicos.

**Decisão confirmada do usuário: Scalar é obrigatório para a documentação do backend desde o MVP.** Utilizar `Microsoft.AspNetCore.OpenApi` para gerar o documento em `/openapi/v1.json` e `Scalar.AspNetCore` para disponibilizar a interface em `/scalar/v1`, seguindo o modelo do Auth. Documentar rotas, parâmetros, DTOs, `CustomResponse`, paginação, status HTTP e contratos globais de erro. Não substituir o Scalar por Swagger UI, Swashbuckle ou NSwag.

### 13.7. Exceptions e middlewares globais

**Decisão confirmada:** reutilizar o modelo completo de exceptions de aplicação e tratamento global do Auth, adaptando os namespaces e os detalhes de domínio/provedor.

Inventário efetivamente encontrado em `Application/Common/Exceptions`:

| Classe | HTTP | `errorCode` |
|---|---|---|
| `AppException` (abstrata) | Definido pelo construtor | Definido pelo construtor |
| `ValidationAppException` | 400 | `validation_error` |
| `AuthenticationFailedAppException` | 401 | `authentication_failed` |
| `AccessDeniedAppException` | 403 | `access_denied` |
| `NotFoundAppException` | 404 | `not_found` |
| `ConflictAppException` | 409 | `conflict` |
| `OperationFailedAppException` | 500 | `operation_failed` |
| `ExternalServiceAppException` | Recebido no construtor | Recebido no construtor |
| `FirstAccessLinkAppException` | 410 | Recebido no construtor |

`AppException` expõe `StatusCode`, `ErrorCode` e `IReadOnlyCollection<string> Errors`, com construtores para uma mensagem ou coleção e fallback quando a coleção está vazia. As subclasses preservam seus códigos e sobrecargas. `FirstAccessLinkAppException` é específica do primeiro acesso do Auth: seu modelo foi identificado, mas não cria uma funcionalidade de primeiro acesso no GitCosmos. Reutilizar as classes gerais; especializações do domínio Git devem herdar da mesma base quando necessárias.

Não foram encontradas `BusinessRuleAppException` nem `IntegrationAppException` nessa pasta da referência; não apresentá-las como classes existentes ou substituir o modelo analisado por elas.

Middlewares globais obrigatórios em `API/Middlewares`, preservando esta ordem relativa do Auth:

```csharp
app.UseMiddleware<RequestLanguageMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
// Proteções locais e demais componentes do pipeline.
app.MapControllers();
```

- `RequestLanguageMiddleware`: lê `Accept-Language`, define `RequestLanguage.Current` e escreve `Content-Language`.
- `RequestLoggingMiddleware`: mede duração, cria escopo de logs com `TraceId`, `CorrelationId` e `JourneyId`, registra método, rota, status HTTP e código de erro. Reutilizar o tratamento dos headers `X-Correlation-ID` e `X-Journey-ID`, incluindo validação, fallback e devolução nos headers. Logs locais não devem depender de Application Insights ou expor credenciais, conteúdo de arquivos ou parâmetros sensíveis.
- `ExceptionHandlingMiddleware`: captura exceções, respeita `StatusCode`, `ErrorCode` e `Errors` de `AppException`, registra o código em `HttpContext.Items` para o middleware de logs, retorna JSON e registra falhas de servidor com trace. O mapping PostgreSQL de unicidade deverá ser adaptado para as restrições SQLite do GitCosmos.
- Preservar os mappings de `BadHttpRequestException` para 400/`bad_request`, `UnauthorizedAccessException` para 401/`unauthorized` e falhas não previstas para 500/`internal_server_error`, com mensagens localizadas e sem stack trace na resposta.
- Configurar `ApiBehaviorOptions.InvalidModelStateResponseFactory` para retornar erros localizados no mesmo formato global; não deixar erros de binding caírem no formato padrão de ProblemDetails.

O JSON global de erro é distinto do envelope de negócio `CustomResponse<T>`, exatamente como na referência:

```json
{
  "success": false,
  "errors": ["Mensagem localizada conforme o idioma solicitado."],
  "errorCode": "validation_error",
  "traceId": "identificador-da-requisicao"
}
```

O frontend deverá aceitar esse corpo sem exigir `result`, `messages` ou `totalRows` em respostas de exceção. Não converter indiscriminadamente todas as falhas em HTTP 200 ou 500.

Também foram identificados `MirrorAccessReadOnlyMiddleware` (bloqueio de escrita em acesso espelho) e `AdministrativeAuditMiddleware` (auditoria administrativa por metadados, justificativa e usuário). São específicos de regras Blux. O GitCosmos preservará o padrão de middlewares globais e seu histórico de operações, sem importar claims, campanhas ou headers administrativos como novos requisitos de produto. Se surgir regra equivalente, adaptar a responsabilidade ao domínio Git.

Falhas individuais de uma operação coletiva continuam sendo resultados por repositório, sem apagar resultados anteriores por meio de uma exceção global. O middleware HTTP não captura automaticamente falhas de jobs ou hubs SignalR; esses limites deverão tratar exceptions e localizar seus contratos mantendo códigos e correlação equivalentes.

### 13.8. Frontend componentizado e adaptação Angular para Vue

- `core`: serviços de infraestrutura da aplicação, cliente HTTP, i18n, SignalR e serviços/modelos por domínio de API.
- `features`: páginas, dialogs, componentes e composables de cada funcionalidade, por exemplo workspaces, repositories, history, changes e operations.
- `layout`: estrutura de navegação, shell e composição das páginas.
- `shared`: componentes reutilizáveis entre features, contratos genéricos, utilitários, validadores e estilos compartilhados.
- Não concentrar a interface em páginas extensas ou pastas globais indiferenciadas de `components`, `services` e `stores`. Cada arquivo deve ter domínio e responsabilidade claros.
- Componentes Vue SFC (`.vue`) com Composition API, `<script setup lang="ts">`, props e emits tipados e estilos locais quando pertinentes. Usar componentes PrimeVue; não importar Angular Material.
- `ref` e `computed` substituem o estado local Angular com signals; Pinia guarda estado compartilhado por domínio, e composables encapsulam lógica reutilizável. Não replicar o mesmo estado em stores e páginas.
- Adaptar os serviços Angular para serviços TypeScript e promises, com um cliente HTTP base centralizado em `core/api`. Não exigir Angular HttpClient, decorators, DI Angular ou RxJS.
- Toda chamada HTTP de negócio passa por `core/<domain>/<domain>-api.service.ts`; componentes e páginas não montam endpoints nem fazem chamadas HTTP diretas.
- Requests e responses em `core/<domain>/models/requests|responses`, com nomes de arquivo kebab-case e campos readonly espelhando os DTOs e o JSON camelCase do backend.
- `CustomResponse<T>`, `PagedResult<T>`, `PaginationRequest` e o contrato de erro global ficam em `shared/models`. Incluir `totalPages` no modelo paginado Vue para espelhar o backend; o model Angular analisado não expõe esse campo calculado.
- Reproduzir as responsabilidades de `ApiBaseService` e `ApiError`: URL e query params, headers, validação de `success`, timeout configurável, cancelamento, estado de carregamento e normalização de erros com `status`, origem e `traceId`.
- Ler mensagens de `errors[]` devolvidas pelo backend. Usar fallback traduzido apenas quando não houver mensagem disponível, como falha de rede ou timeout. Não substituir um erro localizado válido por texto genérico.
- Rotas organizadas por feature, com carregamento sob demanda e layouts via Vue Router. Limpar subscriptions SignalR ao sair do escopo e evitar vazamentos.
- Toda tela deve tratar carregamento, sucesso, falha e estado vazio. Dialogs têm dados e resultados tipados; formulários validam para UX sem substituir as regras do backend.
- Testes Vitest próximos aos componentes, composables e serviços correspondentes; formatação consistente com Prettier e `.editorconfig`, sem `any` para contornar contratos.

### 13.9. Internacionalização obrigatória no MVP

**Decisão confirmada do usuário:** backend e frontend deverão fornecer português brasileiro, espanhol e inglês, inclusive mensagens de erro retornadas pela API.

Identificadores do GitCosmos: `pt-BR`, `es` e `en`. A referência Auth normaliza espanhol como `es-PY`; no GitCosmos, `ES`, `es`, `es-PY` e outras variantes espanholas deverão resolver para o catálogo `es`. Normalizar também variantes de português para `pt-BR` e inglês para `en`. O idioma padrão e fallback será `pt-BR`, como no Auth.

**Backend:**

- Manter `RequestLanguage` e `LocalizedText.Get(key, args)` em `Application/Common/Localization`, com catálogos completos nos três idiomas e contexto por requisição, seguindo o modelo `AsyncLocal` do Auth.
- Receber idioma pelo header `Accept-Language` e informar o idioma efetivo por `Content-Language` no middleware.
- Localizar mensagens de validação FluentValidation, model binding, exceções conhecidas, erro inesperado, sucesso, consultas vazias e mensagens destinadas ao usuário nos resultados de operações Git.
- Códigos de erro, nomes JSON, nomes de rotas e estados de protocolo permanecem estáveis; traduzir o texto de apresentação, não o identificador do contrato.
- Capturar o idioma ao iniciar uma operação assíncrona e preservá-lo explicitamente no contexto da execução e de seus eventos SignalR. Não depender apenas do contexto HTTP para localizar processamento em segundo plano.
- Preservar códigos Git e saídas técnicas como diagnóstico separado quando necessário, sem apresentar stderr bruto como única mensagem de erro ao usuário e sem expor dados sensíveis.

**Frontend:**

- Catálogos por idioma e área em `core/i18n/translations/pt-BR`, `es` e `en`.
- Disponibilizar seleção de idioma, persistir a preferência local e mantê-la no estado compartilhado.
- Enviar `Accept-Language` em todas as chamadas à API pelo cliente HTTP centralizado e usar o mesmo idioma para iniciar operações e conexões/assinaturas em tempo real, conforme seus contratos.
- Traduzir menus, botões, títulos, labels, placeholders, validações, confirmações destrutivas, mensagens, estados vazios e feedback de progresso. Formatar datas e números conforme o idioma selecionado.
- Exibir mensagens já localizadas pelo backend, sem retraduzi-las por comparação de strings. Fallbacks locais usam chaves i18n; códigos estáveis podem selecionar mensagens de fallback quando necessário.
- Não deixar textos de interface fixos em componentes. Toda nova chave deverá existir nos três catálogos; não considerar uma funcionalidade concluída com tradução faltante.

**Validação obrigatória:** testar os três idiomas, normalização e fallback, isolamento entre requisições concorrentes, validações e exceptions, formato dos contratos de erro, envio de headers pelo frontend e mensagens das operações assíncronas/SignalR. A referência local orienta o padrão; os testes deverão demonstrar os contratos efetivos do GitCosmos.

---

## 14. Abstração para execução do Git

A integração com Git deverá ficar isolada atrás de interfaces.

Objetivos:

- Facilitar testes automatizados.
- Evitar dependência direta do Git CLI nos casos de uso.
- Centralizar tratamento de erros.
- Controlar execução de processos.
- Permitir evolução futura da implementação.

A arquitetura deverá separar a responsabilidade de executar um comando Git da responsabilidade de coordenar operações sobre múltiplos repositórios.

---

## 15. Testes automatizados

### Cobertura mínima obrigatória

**Decisão confirmada do usuário:** todo código gerado, adicionado ou alterado deverá ser acompanhado de testes unitários e manter **no mínimo 95% de cobertura** no backend e no frontend.

- Medir cobertura de linhas e branches, com limiar mínimo de 95% para cada métrica, separadamente no backend e no frontend. A cobertura de uma parte não compensa a da outra.
- Verificar também a cobertura do código novo ou alterado, evitando que a cobertura acumulada esconda funcionalidades sem testes.
- Testes devem validar comportamento, regras, cenários de sucesso, falhas e casos de borda; não apenas executar linhas para atingir o percentual.
- Testes de integração e ponta a ponta complementam os testes unitários e não substituem o limiar de cobertura unitária.
- Não excluir código de negócio, controllers, services, use cases, middlewares, componentes ou serviços frontend da medição para atingir artificialmente a meta. Exclusões técnicas de arquivos gerados por ferramentas deverão ser explícitas e justificadas.
- Toda entrega de código deverá informar os testes executados e a cobertura medida. Uma alteração não estará concluída enquanto os testes falharem ou a cobertura ficar abaixo do mínimo.
- **Preferência operacional confirmada:** o usuário executará os testes manualmente. O agente não deverá iniciar testes automaticamente; sempre que uma alteração exigir testes, deverá criar/atualizar os testes necessários e fornecer ao final os comandos PowerShell completos para executar testes unitários e verificar a cobertura, incluindo `Set-Location` com o caminho absoluto correto. Builds e inspeções podem ser feitos pelo agente. Quando a validação manual estiver pendente, informar isso explicitamente, sem declarar a cobertura final aprovada. A exigência de 95% e a validação na CI permanecem obrigatórias.

### Backend

Utilizar xUnit, Moq e Coverlet para testes unitários e medição da cobertura.

Cobertura prioritária:

- Regras de workspace.
- Validação de caminhos.
- Orquestração de operações.
- Tratamento de falhas parciais.
- Controle de concorrência.
- Cancelamento.
- Persistência de configurações.

### Integração Git

Criar repositórios Git temporários durante os testes.

Testar cenários como:

- Repositório válido.
- Diretório inválido.
- Branch inexistente.
- Working tree com alterações.
- Pull sem possibilidade de fast-forward.
- Remote indisponível.
- Falha individual durante operação coletiva.
- Staging e unstage de arquivos e trechos, criação de commits e amend.
- Clonagem e gerenciamento de branches, tags, remotes e upstream.
- Histórico, diferenças e autoria de arquivos.
- Stash, merge, rebase e cherry-pick, incluindo conflitos, continuação e abortamento.
- Revert e modos de reset, incluindo as proteções contra perda de dados.

### Frontend

Utilizar Vitest para testes de componentes e lógica relevante.

### Integração contínua

Configurar GitHub Actions para executar builds e testes automaticamente.

Gerar relatórios de cobertura unitária do backend e frontend e bloquear a CI quando qualquer um deles ficar abaixo de 95% de linhas ou branches. Configurar essa verificação desde a Milestone 1.

---

## 16. Roadmap do MVP

O MVP foi ampliado para incluir as funções básicas de um cliente Git gráfico, além das operações coletivas. A estimativa anterior de **8 a 12 semanas**, considerando **6 a 10 horas semanais**, correspondia ao escopo anterior e precisa ser refeita.

As milestones abaixo indicam a sequência de implementação. Prazos serão reestimados após o detalhamento das tarefas do novo escopo; não há compromisso de entrega baseado na estimativa anterior.

### Milestone 1 — Fundação

- Criar repositório GitHub.
- Clonar repositório localmente.
- Criar solução .NET.
- Configurar .NET SDK 10 em `global.json` e target framework `net10.0` nos projetos backend.
- Criar projetos das camadas backend.
- Criar aplicação Vue.js.
- Configurar TypeScript, Vite, Pinia e PrimeVue.
- Configurar SQLite e EF Core.
- Aplicar Clean Architecture e a organização obrigatória da seção 13, reproduzindo o padrão Auth.
- Estabelecer `CustomResponse`, paginação, controllers, services, use cases e contratos de repositório no modelo definido.
- Implementar a base de exceptions e middlewares globais de idioma, logs e erros, incluindo erros de model binding.
- Configurar frontend componentizado com `core`, `features`, `layout` e `shared`, cliente de API tipado e Vue Router.
- Configurar i18n pt-BR, es e en no backend e frontend, incluindo preferência de idioma e headers.
- Estabelecer comunicação inicial entre frontend e backend.
- Criar estrutura básica de testes.
- Configurar a coleta de cobertura unitária e os limites de 95% de linhas e branches no backend e frontend.
- Configurar CI inicial.
- Configurar OpenAPI nativo e Scalar, com documento em `/openapi/v1.json` e interface em `/scalar/v1`.

**Estado atual (20/11/2026):** solução .NET e quatro camadas criadas, frontend Vue componentizado, contratos de resposta/paginação, exceptions, middlewares, três idiomas, Scalar e consulta inicial de estado implementados. SQLite e EF Core estão registrados; entidades, contratos de repositório e migrations serão implementados com os workspaces na Milestone 2. SignalR está registrado; hubs de operações serão adicionados com os fluxos correspondentes. Scripts de build, testes/cobertura e workflow CI foram criados. A validação final de testes e cobertura será executada manualmente pelo usuário; não considerar a milestone definitivamente aprovada antes dessa evidência. Consultar `README.md` e `docs/decisions/0001-foundation.md`.

**Próxima direção acordada:** iniciar a Milestone 2 com o desenho enxuto de `Workspace`, `GitRepository` e `WorkspaceRepository`, definindo relacionamentos, unicidade de caminhos/vínculos e remoção sem exclusão de arquivos antes da migration. Implementar criação/listagem ponta a ponta e então completar CRUD e vínculos. O schema e esses fluxos ainda não foram implementados.

### Milestone 2 — Workspaces

- CRUD de workspaces.
- Adicionar repositórios locais.
- Remover repositórios de workspaces.
- Validar caminhos.
- Persistir configurações no SQLite.

### Milestone 3 — Dashboard, histórico e diferenças

- Detectar branch atual.
- Consultar alterações locais.
- Consultar remote.
- Identificar commits ahead/behind.
- Criar dashboard unificado.
- Exibir histórico com grafo, detalhes de commits e diferenças.
- Implementar pesquisa, histórico de arquivos e blame.

### Milestone 4 — Fluxo básico de um repositório

- Abertura, inicialização e clonagem de repositórios.
- Alterações locais, staging e unstage de arquivos e trechos.
- Commits, amend, descarte protegido e `.gitignore`.
- Gerenciamento de branches, tags, remotes e upstream.
- Fetch, pull e push individuais.

### Milestone 5 — Integração e recuperação

- Stash.
- Merge e rebase.
- Cherry-pick e revert.
- Reset soft, mixed e hard, com as proteções correspondentes.
- Fluxo de resolução de conflitos, continuação e abortamento.

### Milestone 6 — Operações coletivas

- Fetch All.
- Pull All.
- Push All.
- Checkout All.
- Seleção de repositórios.
- Pré-validações.
- Tratamento de falhas parciais.

### Milestone 7 — Execução e observabilidade

Os controles essenciais de concorrência, pré-validação e segurança deverão acompanhar cada operação desde sua implementação; esta milestone consolida a experiência de acompanhamento e a validação integrada.

- SignalR.
- Progresso em tempo real.
- Logs.
- Cancelamento seguro.
- Controle de concorrência.
- Testes de falha.

### Milestone 8 — Distribuição

- Empacotamento Windows.
- Build self-contained.
- README completo.
- Diagramas de arquitetura.
- Documentação de decisões.
- Publicação da versão `v0.1.0`.

---

## 17. Funcionalidades futuras

As funcionalidades abaixo foram discutidas como possibilidades de evolução, **não fazem parte obrigatória do MVP**.

### Versão v0.2 — Compartilhamento de workspaces

- Exportação de configurações para JSON.
- Importação de workspaces via JSON.
- Compartilhamento de configurações entre desenvolvedores.
- Perfis de branches.

Na exportação, não devem ser compartilhados caminhos locais específicos de uma máquina como se fossem universais.

O arquivo poderá utilizar URLs remotas dos repositórios e outras informações portáveis.

A importação poderá orientar o usuário a clonar os repositórios em diretórios locais.

### Versão v0.3 — Recursos Git avançados

- Snapshots de conjuntos de repositórios.
- Registro de SHAs por repositório.
- Restauração de estados, com proteções adequadas.

Clonagem, diferenças e consulta de commits integram o MVP. Esta versão futura concentra os recursos de snapshots e restauração de conjuntos de repositórios.

### Versão v1.0

- Melhorias de experiência do usuário.
- Estabilidade.
- Polimento da interface.
- Distribuição mais madura.
- Documentação consolidada.
- Evolução dos recursos já existentes de internacionalização, mantendo os três idiomas obrigatórios desde o MVP.

O escopo exato será definido conforme o progresso das versões anteriores.

### Futuro de longo prazo

Poderão ser avaliados:

- Sincronização entre dispositivos.
- Colaboração entre membros de uma equipe.
- Workspaces compartilhados.
- Serviços cloud opcionais.
- Armazenamento centralizado.

Essas possibilidades não devem introduzir dependência de nuvem no MVP.

---

## 18. Interface e identidade visual

O conceito visual do GitCosmos pode explorar:

- Universo.
- Planetas.
- Órbitas.
- Constelações.
- Conjuntos de repositórios.

A metáfora principal é um workspace como um universo composto por diversos repositórios independentes.

**Marca e temas implementados na fundação:**

- A logo fornecida pelo usuário em `src/frontend/src/shared/assets/branding/Logo_GitCosmos.png` é utilizada no cabeçalho com transparência preservada, sem fundo branco ou moldura CSS. A versão SVG original permanece disponível, sem ser selecionada pela interface.
- Favicon e ícone para dispositivos Apple utilizam `src/frontend/public/GitCosmos-favicon-transparent.png`, com transparência nos cantos. O SVG original é preservado, sem referência no HTML atual.
- Alternância entre temas claro e escuro no cabeçalho, com textos em pt-BR, es e en e preferência persistida em `localStorage` (`gitcosmos.theme`). Cosmic Dark é o tema principal na primeira visita e para preferência inválida; uma escolha light/dark já salva é respeitada. Tema e idioma ficam no navegador, em gitcosmos.theme e gitcosmos.language.
- Store em `core/theme/theme.store.ts`, aplicada no bootstrap antes de montar os componentes. `data-theme` e classe `.dark` na raiz sincronizam a interface com o PrimeVue.
- Todas as cores customizadas devem ser variáveis globais em `src/frontend/src/shared/styles/tokens.css`. O arquivo reproduz os tokens semânticos --gc-* dos design systems Cosmic Dark e Cosmic Light, com aliases --color-* para a interface e o PrimeVue. Cosmic Dark é a base e cosmic-light sobrescreve os valores; hover e escalas auxiliares derivam dessas paletas. Não espalhar cores fixas pelos componentes.
- `core/theme/primevue-preset.ts` conecta os componentes PrimeVue às mesmas variáveis globais. As cores e a transparência dos arquivos de marca são preservadas; a logo fica diretamente sobre o fundo do cabeçalho em ambos os temas. A opção inicial por uma base clara foi removida conforme solicitação do usuário.
- Títulos H1 usam #EA580C nos dois temas, conforme os design systems, por meio do token global --gc-heading-h1 ligado a --gc-brand.
- Testes da store, do seletor e do bootstrap preparados; execução e aprovação da cobertura permanecem manuais pelo usuário.

**Referências visuais obrigatórias:** toda alteração visual deve consultar e respeitar .agents/GitCosmos-Cosmic-Light-Design-System.md e .agents/GitCosmos-Cosmic-Dark-Design-System.md. As paletas e papéis semânticos destes documentos estão adotados no frontend; a preferência do usuário é persistida no navegador. Continuam pendentes a fonte oficial, o detalhamento final dos componentes e o layout definitivo do dashboard. Os exemplos dos documentos não representam funcionalidades implementadas.

PrimeVue foi escolhido como biblioteca de componentes para acelerar o desenvolvimento da interface.

---

## 19. GitHub e desenvolvimento open source

### Decisões

- Repositório público.
- Conta pessoal do desenvolvedor.
- Frontend e backend no mesmo repositório.
- Uso de GitHub Actions.
- Organização do desenvolvimento por issues e milestones.
- Documentação de decisões arquiteturais.

### Licença

A licença MIT foi sugerida como uma opção permissiva.

O arquivo `LICENSE` existente no repositório confirma a licença **MIT**, com copyright de Rafael Gilberto Kampa (2026).

### Documentação

O README deverá explicar:

- Problema resolvido.
- Proposta do GitCosmos.
- Funcionalidades.
- Tecnologias.
- Arquitetura.
- Instalação.
- Execução.
- Contribuição.
- Roadmap.

A documentação arquitetural poderá incluir ADRs (*Architecture Decision Records*) na pasta `docs/decisions`.

---

## 20. Objetivo profissional e divulgação

O GitCosmos também será utilizado como projeto público de portfólio.

O desenvolvimento deverá evidenciar competências como:

- Engenharia de software.
- Arquitetura de software.
- C# e ASP.NET Core.
- Vue.js e TypeScript.
- Persistência local.
- Processamento assíncrono.
- Integração com processos do sistema operacional.
- Concorrência.
- Tratamento de falhas.
- Testes automatizados.
- CI/CD.
- Documentação técnica.

A divulgação no LinkedIn poderá abordar:

- O problema que motivou a ferramenta.
- As decisões arquiteturais.
- Os desafios da orquestração de múltiplos repositórios.
- As limitações transacionais do Git.
- Os testes realizados.
- A evolução das versões.
- Demonstrações práticas.

O foco deve estar tanto no produto quanto no raciocínio técnico empregado em seu desenvolvimento.

---

## 21. Decisões confirmadas — resumo

1. O nome do projeto é **GitCosmos**.
2. O repositório é `https://github.com/RafaelKampa/GitCosmos`.
3. O diretório local informado é `C:\Repos\GitCosmos`.
4. O projeto será open source.
5. O repositório ficará inicialmente na conta pessoal do GitHub.
6. Frontend e backend ficarão no mesmo repositório.
7. A aplicação será local-first.
8. A plataforma inicial será Windows.
9. A interface será web, servida localmente pelo backend.
10. O backend utilizará ASP.NET Core.
11. O frontend utilizará Vue.js 3 e TypeScript.
12. O banco de dados será SQLite.
13. O acesso aos dados utilizará Entity Framework Core.
14. O progresso em tempo real utilizará SignalR.
15. A integração Git utilizará inicialmente o Git CLI instalado.
16. A arquitetura será um monólito modular.
17. O GitCosmos gerenciará workspaces de repositórios independentes.
18. O MVP incluirá as funções básicas de um cliente Git gráfico (com referência no Fork), visualização unificada e operações Git coletivas, conforme o escopo detalhado na seção 8.
19. Pull utilizará `--ff-only` como comportamento padrão.
20. Falhas parciais serão tratadas individualmente.
21. Não haverá rollback automático de operações distribuídas.
22. Operações mutáveis no mesmo repositório deverão ser serializadas.
23. O MVP não dependerá de Azure nem de servidor central.
24. A primeira versão pública planejada é `v0.1.0`.
25. Clonagem, staging, commits, histórico, diferenças, branches, tags, remotes, stash, merge, rebase, cherry-pick, revert, reset e resolução de conflitos integram o MVP.
26. A estimativa de prazo anterior deverá ser refeita para o escopo ampliado do MVP.
27. O backend seguirá Clean Architecture, a estrutura de pastas e o padrão de código do `Blux.LATAM.Auth`, conforme a análise da seção 13.
28. O fluxo backend será Controller → IService → Service → UseCase → IRepository → Repository → EF Core, com contratos próprios para Git CLI.
29. Controllers e `CustomResponse<T>` seguirão o modelo concreto do Auth, incluindo paginação, checagens de `Success` e rotas explícitas por ação.
30. Exceptions de aplicação e middlewares globais de idioma, logs e tratamento de erros seguirão o Auth, com adaptações para SQLite e domínio Git.
31. O frontend Vue será componentizado e organizado em `core`, `features`, `layout` e `shared`, adaptando a arquitetura do Admin Frontend.
32. Português brasileiro (`pt-BR`), espanhol (`es`) e inglês (`en`) são obrigatórios no backend e frontend desde o MVP, incluindo validações, erros, mensagens de sucesso e progresso.
33. A organização de pastas da seção 13 é obrigatória desde a fundação.
34. Todo código gerado, adicionado ou alterado deverá ter testes unitários e cobertura mínima de 95% de linhas e branches, no backend e no frontend, com validação obrigatória na CI conforme a seção 15.
35. A documentação do backend utilizará obrigatoriamente OpenAPI nativo e Scalar desde o MVP, conforme a seção 13.6.
36. O backend utilizará .NET SDK 10 e target framework `net10.0`, com versão do SDK fixada em `global.json`.
37. O usuário executará os testes manualmente; o agente fornecerá os comandos PowerShell ao final e informará quando a validação estiver pendente.
38. A skill local `.agents/skills/gitcosmos-development/SKILL.md` consolida os padrões do projeto e deverá orientar desenvolvimento, revisão e documentação. Todo comando de teste fornecido ao usuário deverá incluir `Set-Location` com o caminho absoluto correto.

39. Toda entrega deverá incluir comandos PowerShell de testes manuais aplicáveis e de execução do projeto, com Set-Location e caminho absoluto, além de uma sugestão de mensagem de commit em inglês. Em alterações documentais, testes são opcionais; nenhuma sugestão autoriza executar commit/push.

---

## 22. Pendências técnicas

As decisões abaixo deverão ser tomadas durante a implementação:

- Atualizações futuras de patch do .NET SDK 10; a fundação fixou 10.0.300 em `global.json`, com `latestPatch`.
- Atualizações futuras do Node.js; a fundação registrou 24.21.0 em `.nvmrc`.
- Detalhes complementares de formatação e tooling, respeitando as convenções de código e pastas já definidas na seção 13.
- Estratégia de migrations do SQLite.
- Modelo de entidades e relacionamentos.
- DTOs e contratos específicos dos endpoints, respeitando o padrão já definido de controllers, `CustomResponse`, paginação e erros globais.
- Estratégia de proteção dos endpoints locais.
- Política de portas HTTP.
- Estrutura dos eventos SignalR.
- Limite de concorrência padrão.
- Estratégia de cancelamento de processos.
- Tratamento de autenticação interativa do Git.
- Política de retenção de logs.
- Mapeamento das restrições SQLite para exceptions no middleware global, preservando o contrato de erros definido.
- Estratégia de instalador Windows.
- Formato exato de importação/exportação JSON.
- Layout e identidade visual.
- Reestimativa de esforço e prazo das milestones considerando as funções básicas do cliente Git incluídas no MVP.

Esses pontos não devem ser tratados como decisões já aprovadas.

---

## 23. Instruções para continuidade do desenvolvimento

O próximo ambiente de desenvolvimento deverá:

1. Verificar se possui acesso ao diretório `C:\Repos\GitCosmos`.
2. Inspecionar o estado atual do repositório antes de modificar arquivos.
3. Respeitar a stack e as decisões arquiteturais documentadas.
4. Concluir a validação manual da fundação da Milestone 1 e então avançar para os workspaces da Milestone 2.
5. Evitar implementar funcionalidades futuras antes da estrutura básica.
6. Manter frontend e backend no mesmo repositório.
7. Priorizar soluções simples, testáveis e adequadas à execução local.
8. Documentar novas decisões relevantes.
9. Não assumir que propostas futuras são requisitos já confirmados.
10. Não executar commits ou pushes sem alinhamento com o usuário.
11. Respeitar a Clean Architecture e o padrão concreto do Auth registrados na seção 13, consultando as referências antes de implementar fluxos equivalentes.
12. Manter a componentização e a organização de pastas frontend adaptadas do Admin para Vue; evitar chamadas HTTP e regras de infraestrutura nas páginas.
13. Entregar cada fluxo com pt-BR, es e en no backend e frontend, incluindo erros, validações e feedback assíncrono.
14. Entregar todo código gerado, adicionado ou alterado com testes unitários e evidência de cobertura mínima de 95%, respeitando os critérios e bloqueios da seção 15.
15. Não iniciar testes automaticamente. Fornecer os comandos PowerShell com `Set-Location` para execução manual pelo usuário e registrar validações pendentes.
16. Utilizar a skill local `.agents/skills/gitcosmos-development/SKILL.md` e manter suas instruções e referências alinhadas às novas decisões do projeto.

17. Ao final das entregas, fornecer também os comandos PowerShell para executar o projeto (incluindo Set-Location) e sempre sugerir uma mensagem de commit em inglês, sem executar o commit.

**Objetivo imediato:** validar manualmente os testes e a cobertura da fundação inicial e então implementar os workspaces da Milestone 2.
