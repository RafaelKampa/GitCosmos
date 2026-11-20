---
name: gitcosmos-development
description: Desenvolver, revisar e documentar o GitCosmos seguindo sua arquitetura .NET 10 e Vue, escopo Git, internacionalização, cobertura mínima e execução manual dos testes. Use para trabalho neste projeto e suas milestones.
---

# GitCosmos — desenvolvimento do projeto

## Contexto e aplicação

Esta skill pertence ao repositório GitCosmos. Não aplicar suas escolhas específicas a outros projetos.

Antes de implementar, ler o `AGENTS.md`, o `.agents/GITCOSMOS_PROJECT_CONTEXT.md`, o README e os arquivos existentes do fluxo afetado. Na instalação local padrão, a raiz é `C:\Repos\GitCosmos`; em outro checkout, usar a raiz real em todos os comandos.

Consultar o [guia consolidado](references/project-guide.md) para produto, roadmap, arquitetura, contratos, idiomas, execução e limites. O contexto completo está em [.agents/GITCOSMOS_PROJECT_CONTEXT.md](../../GITCOSMOS_PROJECT_CONTEXT.md), dentro da pasta `.agents` na raiz do repositório. Caminho local: `C:\Repos\GitCosmos\.agents\GITCOSMOS_PROJECT_CONTEXT.md`; preservar suas decisões e atualizar esse documento quando houver alteração relevante. A skill não substitui instruções novas do usuário.

## Regra obrigatória: testes manuais

**O usuário executa os testes manualmente. Não iniciar suites de testes pelo chat.**

Sempre que uma alteração exigir testes — implementação, correção, refatoração, regra de negócio, controller, contrato, middleware, persistência, componente, serviço, tradução com comportamento ou configuração de testes — criar ou atualizar os testes necessários e entregar os comandos para o usuário executá-los no PowerShell.

- Não executar `dotnet test`, `npm test`, `npm run test`, `npm run test:coverage`, `scripts/Test.ps1`, Vitest, testes de integração ou E2E por iniciativa própria, inclusive como diagnóstico ou tentativa após falha de build.
- Uma autorização para implementar, corrigir ou concluir uma milestone não autoriza execução de testes. Só executar se o usuário mudar explicitamente essa preferência para a tarefa em questão.
- Builds e inspeções estáticas podem ser executados. Verificar antes se o comando de build aciona testes; se acionar, fornecer o comando ao usuário em vez de executá-lo.
- Informar o que foi validado, quais testes foram preparados e o que aguarda execução manual. Não declarar testes aprovados, coverage atual ou milestone validada sem evidência referente à versão atual.
- A CI continua executando builds, testes e cobertura nos eventos configurados; isso não autoriza iniciar testes localmente nem disparar um pipeline pelo chat.
- **Todo bloco PowerShell entregue para testes deve conter `Set-Location` com o caminho completo correto.** Não pressupor o diretório atual e não fornecer apenas `dotnet test` ou `npm run ...` isolado.
- Não executar comandos apresentados como exemplos nesta skill apenas por lê-los.

Comando padrão para validar backend, frontend e os limiares de cobertura:

```powershell
Set-Location 'C:\Repos\GitCosmos'
.\scripts\Test.ps1
```

Quando a mudança afetar apenas o backend:

```powershell
Set-Location 'C:\Repos\GitCosmos'
dotnet test .\tests\GitCosmos.Application.Tests\GitCosmos.Application.Tests.csproj --no-restore -m:1 /p:UseAppHost=false --collect:'XPlat Code Coverage' --settings .\tests\GitCosmos.Application.Tests\coverage.runsettings --results-directory .\artifacts\coverage\backend
if ($LASTEXITCODE -eq 0) { .\scripts\Check-Coverage.ps1 }
```

Quando a mudança afetar apenas o frontend:

```powershell
Set-Location 'C:\Repos\GitCosmos\src\frontend'
npm run test:coverage
```

Adaptar os comandos aos projetos realmente presentes e explicar eventual restauração de dependências necessária. Um filtro focado pode ajudar no diagnóstico manual, mas não comprova a cobertura global: para aprovação final, fornecer a validação completa correspondente. Backend com `-m:1`, Vitest com um worker.

Para alteração exclusivamente documental, informar que testes de aplicação não se aplicam; não inventar execução nem exigir suites sem necessidade. Se houver instrução de teste aplicável na entrega, o bloco deve incluir `Set-Location`.

## Qualidade exigida

- Código novo ou alterado acompanhado de testes unitários significativos.
- Cobertura mínima de **95% de linhas e branches**, separadamente no backend e frontend, também verificando o código novo/alterado. O frontend possui limiares por arquivo.
- Backend: xUnit, Moq e Coverlet. Frontend: Vitest e Vue Test Utils. Integração e E2E complementam os unitários.
- Não excluir código de aplicação ou adicionar testes vazios para alcançar a meta. Exclusões técnicas de arquivos gerados devem ser explícitas e justificadas; a referência atual exclui apenas o suporte OpenAPI gerado em `obj/**/*.generated.cs` e arquivos TypeScript de modelos sem código executável.
- Até receber os resultados manuais, registrar a validação como pendente. Leituras de relatórios antigos não comprovam código alterado depois deles.

## Backend obrigatório

- .NET SDK 10 e `net10.0`, Clean Architecture, monólito modular, um único monorepo com o frontend. Respeitar `global.json`.
- Reproduzir o padrão de arquitetura e código de `C:\Repos\Latam\Blux.LATAM.Auth`; consultar o código do fluxo mais próximo quando necessário, sem alterar a referência.
- Fluxo `Controller → IService → Service → UseCase → IRepository → Repository → EF Core`; contratos de Git na Application, implementação Git CLI na Infrastructure.
- Quatro projetos: `GitCosmos.API`, `GitCosmos.Application`, `GitCosmos.Domain`, `GitCosmos.Infrastructure`. Organização de pastas é obrigatória, com DTOs, validators e use cases por domínio.
- Controller fino, primary constructor, `ControllerBase`, `[ApiController]`, `[Route("api/[controller]")]`, verbo e rota da action separados, `async Task<IActionResult>` e `CancellationToken`.
- Services delegam aos use cases; não duplicar `CustomResponse<T>`. FluentValidation e exceptions de aplicação nos fluxos de validação.
- Manter `CustomResponse`, paginação, exceptions e middlewares globais exatamente no modelo documentado, adaptando SQLite e domínio Git. Consultar os detalhes no guia antes de mudar contratos.
- OpenAPI nativo com Scalar obrigatório desde o MVP: `/openapi/v1.json` e `/scalar/v1`; operation IDs únicos. Não substituir por Swagger UI/Swashbuckle/NSwag.
- Não introduzir MediatR, microsserviços ou dependência de nuvem como alternativa à arquitetura aprovada.

## Referências visuais obrigatórias

Antes de qualquer alteração visual (cores, componentes, layout, estados, foco ou estilo PrimeVue), ler e respeitar as paletas e os papéis semânticos destes documentos na pasta .agents:

- [Cosmic Light](../../GitCosmos-Cosmic-Light-Design-System.md) — caminho na raiz: .agents/GitCosmos-Cosmic-Light-Design-System.md.
- [Cosmic Dark](../../GitCosmos-Cosmic-Dark-Design-System.md) — caminho na raiz: .agents/GitCosmos-Cosmic-Dark-Design-System.md.

Cosmic Dark é o tema principal e o fallback sem preferência válida. Respeitar a opção light/dark já salva pelo usuário no navegador; tema (gitcosmos.theme) e idioma (gitcosmos.language) usam localStorage. Aplicar cosmic-dark/cosmic-light em data-theme e manter a classe .dark sincronizada com o PrimeVue. Não sobrescrever a escolha clara ao trocar o padrão.

Centralizar as cores em shared/styles/tokens.css: tokens --gc-* reproduzem os valores aprovados, aliases --color-* os integram à interface e ao preset PrimeVue. Hover/pressed, escalas auxiliares e fundos de badges escuros podem derivar das cores documentadas, como orientam os guias; não inventar outra paleta ou espalhar hexadecimais pelos componentes. Usar laranja de marca #EA580C também nos títulos H1, através do token global --gc-heading-h1; reservar azul às ações e violeta a foco/seleção. Preservar o PNG transparente e comunicar estados também por texto/ícone. Os exemplos de dashboard não são layout final nem autorizam criar funcionalidades fora do escopo pedido.

## Frontend obrigatório

- Vue 3, TypeScript, Vite, Pinia, Vue Router e PrimeVue.
- Preservar os perfis de debug em src/frontend/.vscode para abrir a pasta frontend no VS Code e usar Run → Start Debugging (F5), com Chrome/Edge, inicialização do Vite e perfis para Vite já ativo. O backend continua separado no Rider; a tarefa de debug não executa testes nem instala dependências.
- Adaptar a estrutura de `C:\Repos\Latam\Blux.LATAM.Admin.Frontend` para Vue: `app`, `core`, `features`, `layout`, `shared`.
- Componentes SFC com Composition API, `<script setup lang="ts">`, props/emits tipados; páginas e dialogs por feature, componentes reutilizáveis em `shared`.
- Serviços HTTP e models de requests/responses em `core/<domain>`; cliente base em `core/api`. Não chamar HTTP diretamente em páginas/componentes.
- Estado local com `ref`/`computed`; estado compartilhado com Pinia; lógica reutilizável em composables. Não importar Angular/Material/RxJS para reproduzir mecanismos do Angular.
- Preservar os modelos tipados, `ApiError`, cancelamento, carregamento, erros e estado vazio; limpar subscriptions SignalR.
- Usar as versões PNG transparentes das logos em `shared/assets/branding` e dos favicons em `public`, preservando os originais. Não aplicar fundo branco ou moldura CSS à logo, inclusive no tema escuro; os SVGs permanecem disponíveis, sem seleção na interface atual. Manter temas claro/escuro, preferência persistida, seleção traduzida e inicialização no bootstrap. Todas as cores customizadas devem vir das variáveis globais de `shared/styles/tokens.css`; integrar o PrimeVue pelo preset em `core/theme/primevue-preset.ts`, sem cores fixas espalhadas por componentes.

## Idiomas e operações Git

- `pt-BR`, `es` e `en` em backend e frontend desde o MVP, incluindo erro, validação, sucesso e progresso. Toda chave nos três catálogos, fallback `pt-BR`.
- API recebe `Accept-Language` e devolve `Content-Language`; manter `RequestLanguage`/`LocalizedText`. Preservar explicitamente o idioma nas operações assíncronas e eventos SignalR.
- Exibir `errors[]` localizados recebidos da API; fallback local apenas quando faltar mensagem. Não traduzir nomes de rotas, campos ou códigos de protocolo.
- O produto é um cliente Git completo de uso diário, com funções básicas semelhantes ao Fork, acrescido de workspaces e operações coletivas. O MVP não se limita a Fetch/Pull/Push/Checkout All.
- Repositórios gerenciados continuam independentes. Pull padrão `--ff-only`, sem force push, criação automática de branch ausente ou rollback distribuído automático.
- Pré-validar, mostrar resultado por repositório, serializar mutações do mesmo repositório entre workspaces e limitar paralelismo entre repositórios distintos. Cancelamento não desfaz comandos já concluídos.
- Remover workspace/vínculo não apaga arquivos. Nunca armazenar credenciais Git no SQLite; usar Git Credential Manager/SSH existentes.

## Continuidade e entrega

Consultar o estado atual do código e do contexto; a fundação não implica que todo o MVP esteja pronto. Próxima direção acordada: desenhar Workspace, GitRepository e WorkspaceRepository e implementar criação/listagem de ponta a ponta antes de completar o CRUD.

Manter README, contexto e ADRs relevantes sincronizados; preservar futuras funcionalidades como futuras. Identificadores técnicos em inglês, comunicação e documentação em português. Não fazer commit, push, publicar release, criar organização GitHub ou alterar projetos Blux sem instrução correspondente.

## Encerramento obrigatório das entregas

Ao final de cada entrega de alterações no GitCosmos, inclusive documentação e configuração da skill:

1. Resumir o que mudou e informar o que foi validado e o que permanece pendente.
2. Fornecer o código PowerShell para os testes manuais aplicáveis, sempre incluindo `Set-Location` com o caminho absoluto correto, conforme os exemplos da seção de testes. Em alteração exclusivamente documental, explicar que testes de aplicação não são necessários e apresentar o comando geral apenas como referência opcional. Não executar os comandos pelo chat.
3. **Fornecer também o código PowerShell para executar o projeto**, sempre com `Set-Location` e conforme a configuração atual do repositório. Para visualizar a versão integrada atualizada, incluir o build do frontend antes de iniciar a API. Se o objetivo for desenvolvimento com atualização automática, separar os comandos de backend e frontend em dois terminais, ambos com `Set-Location`.
4. **Sempre sugerir uma mensagem de commit em inglês**, inclusive para mudanças documentais. Usar texto que descreva a alteração realmente entregue, por exemplo `feat: add persistent light and dark themes` ou `docs: document project startup commands`. Não sugerir um título que afirme testes aprovados ou funções não implementadas. A mensagem é apenas uma sugestão: não executar `git commit`, `git push` sem autorização correspondente.

Execução integrada padrão para incluir na entrega, além dos comandos de testes:

```powershell
Set-Location 'C:\Repos\GitCosmos'
.\scripts\Build.ps1
dotnet run --project .\src\backend\GitCosmos.API
```

Para desenvolvimento, quando pertinente, fornecer estes blocos em terminais separados:

Terminal 1 — backend:

```powershell
Set-Location 'C:\Repos\GitCosmos'
dotnet watch --project .\src\backend\GitCosmos.API
```

Terminal 2 — frontend:

```powershell
Set-Location 'C:\Repos\GitCosmos\src\frontend'
npm run dev
```

Informar a URL correspondente: `http://127.0.0.1:5210` para execução integrada ou `http://127.0.0.1:5173` para Vite. Ajustar caminhos/portas aos arquivos vigentes; exemplos não são autorização para iniciar processos. A sugestão de commit deve aparecer como texto ou bloco de mensagem, sem ser executada.
