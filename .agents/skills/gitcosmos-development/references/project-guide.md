# Guia consolidado do GitCosmos

Contexto completo e estado corrente: [.agents/GITCOSMOS_PROJECT_CONTEXT.md](../../../GITCOSMOS_PROJECT_CONTEXT.md), relativo à raiz do repositório.

## Produto e decisões de base

- Repositório público open source MIT: `https://github.com/RafaelKampa/GitCosmos`; raiz local `C:\Repos\GitCosmos`.
- Nome escolhido: GitCosmos. Alternativas históricas: GitOrbit, GitHive, RepoCosmos, EnxameGit, EnxameFlow. Conta pessoal, sem organização dedicada; disponibilidade de marca INPI/domínios não foi confirmada.
- Descrição: “A local-first Git client for managing multiple repositories in a unified workspace.” Slogan sugerido: “One workspace. A universe of repositories.”
- Público: desenvolvedores com microsserviços ou múltiplos repositórios relacionados. Também é projeto de portfólio e divulgação técnica; não importar dados pessoais do chat de LinkedIn como requisitos do produto.
- Aplicação web local-first no Windows. Frontend/backend no mesmo repositório do produto, mas os repositórios gerenciados não viram um monorepo.
- Backend é monólito modular com Clean Architecture. Sem servidor central, autenticação cloud própria ou serviços Azure obrigatórios. Internet necessária para remotes e serviços externos.
- Docker foi discutido e dispensado: manter execução nativa no Windows. Não adicionar Docker/Compose ou infraestrutura externa por iniciativa própria.
- Logo e favicons PNG transparentes fornecidos pelo usuário estão integrados ao frontend, sem fundo branco ou moldura CSS na logo. Os SVGs originais são preservados, sem uso na interface atual. Temas claro/escuro são selecionáveis e persistidos em gitcosmos.theme; cores customizadas centralizadas em shared/styles/tokens.css e consumidas também pelo preset PrimeVue. Preservar os originais da marca. Toda alteração visual deve ler e respeitar as paletas de .agents/GitCosmos-Cosmic-Light-Design-System.md e .agents/GitCosmos-Cosmic-Dark-Design-System.md. Cosmic Dark é o padrão sem preferência válida; escolhas light/dark existentes são respeitadas, e tema/idioma ficam no navegador. Tokens --gc-* reproduzem os guias, com aliases --color-* e data-theme cosmic-dark/cosmic-light. Fonte oficial, detalhamento de componentes e dashboard final continuam pendentes; consultar a seção 18 do contexto.

## Backend e dependências

```text
API -> Application -> Domain
API -> Infrastructure -> Application e Domain

Controller -> IService -> Service -> UseCase -> IRepository -> Repository -> EF Core
                                         -> IGit... -> Git CLI
```

`Domain` não referencia os outros projetos. `API` conhece Infrastructure para composição; regras e persistência não ficam em controllers. Git CLI usa `System.Diagnostics.Process`, com argumentos seguros, operações permitidas e caminhos validados, sem execução arbitrária de shell via API.

Pastas em `src/backend`:

| Projeto | Organização |
|---|---|
| `GitCosmos.API` | Controllers, Middlewares, Hubs, Extensions, Services, Program/ApiHost |
| `GitCosmos.Application` | Common/Exceptions, Common/Localization, CustomResponse, PagedResult, DTOs/Requests e Responses por domínio, Interfaces/Services e Git, Services, UseCases e Validators por domínio, DependencyInjection |
| `GitCosmos.Domain` | Common, Entities, Enums, ReadModels, Interfaces/Repositories |
| `GitCosmos.Infrastructure` | Data, Migrations/Ef, Repositories, Git, FileSystem, DependencyInjection |

`AddApplication()` e `AddInfrastructure(...)` registram dependências; composição na API. Respeitar padrão `BaseEntity` com identidade, UTC, ativação/desativação e repositórios base/especializados onde houver persistência. Adaptar os mapeamentos PostgreSQL do Auth para SQLite.

Referência Auth: `Controllers/RoleController.cs`, `PermissionController.cs`, `Application/Common`, `RoleService`, `IRoleService`, `UseCases/Roles`, `Validators`, `Infrastructure/Data/AuthDbContext.cs`, `Infrastructure/Repositories/Repository.cs` e middlewares/Program. RoleService instancia use cases em campos readonly e delega tasks; os use cases já retornam `CustomResponse`. Também existem use cases registrados em DI. Não uniformizar a referência por suposição nem criar envelope duplo.

Usar records para DTOs, projeções como `RoleResponse.Projection` onde pertinente, primary constructors, namespaces por arquivo, nullable e async com CancellationToken até os componentes canceláveis. Não copiar integrações Blux de identidade, campanhas, e-mail, JWT ou PostgreSQL como requisitos do GitCosmos.

### Controllers, respostas e paginação

Padrão de escrita:

```csharp
[ApiController]
[Route("api/[controller]")]
public class WorkspaceController(IWorkspaceService workspaceService) : ControllerBase
{
    [HttpPost]
    [Route("CreateWorkspace")]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var response = await workspaceService.CreateAsync(request, cancellationToken);
        if (!response.Success)
            return BadRequest(response);
        return Ok(response);
    }
}
```

Leituras seguem a variante equivalente do Auth: Role verifica Success e pode devolver NotFound em falha; Permission devolve Ok diretamente. Lista vazia bem-sucedida é HTTP 200, sucesso e coleção vazia, com mensagem localizada quando pertinente. Não adicionar try/catch repetitivo, retornar entidades cruas ou substituir os controllers por Minimal APIs de negócio.

`Application/Common/CustomResponse<T>` mantém:

- `Success: bool`, `Errors: List<string>`, `Messages: List<string>`, `Result: T?`, `TotalRows: int?`; JSON camelCase.
- Construtores vazio e `(success, errors, result, totalRows = null, messages = null)`.
- Factories `Ok(result, totalRows)`, `Ok(result, message, totalRows)`, `Fail(errors, result)`, `Fail(error)`, `OkList(items, totalRows)`, `FailList(errors)` e `FailList(error)`.
- List factories retornam `CustomResponse<List<T>>`; falhas de lista têm coleção vazia.
- A implementação local usa `JsonIgnore(Condition = WhenWritingNull)` para `TotalRows` com System.Text.Json; não depender do atributo Newtonsoft do Auth para esse serializer.

`Domain/Common/PaginationRequest`: página inicial 0, tamanho padrão 10, mínimo 1, máximo 100. `Application/Common/PagedResult<T>`: Items, Page, PageSize, TotalRows e TotalPages calculado. Contrato paginado `CustomResponse<PagedResult<T>>`; modelo Vue inclui totalPages.

### Exceptions e middlewares

`AppException` abstrata expõe StatusCode, ErrorCode e coleção Errors; construtores para mensagem e coleção, com fallback quando vazia. FluentValidation usa `ValidateAndThrowApp`, lançando ValidationAppException.

| Exception | HTTP | errorCode |
|---|---|---|
| ValidationAppException | 400 | validation_error |
| AuthenticationFailedAppException | 401 | authentication_failed |
| AccessDeniedAppException | 403 | access_denied |
| NotFoundAppException | 404 | not_found |
| ConflictAppException | 409 | conflict |
| OperationFailedAppException | 500 | operation_failed |
| ExternalServiceAppException | Construtor | Construtor |

FirstAccessLinkAppException (410) existe no Auth e é específica daquele domínio; não criar primeiro acesso no GitCosmos por isso. BusinessRuleAppException e IntegrationAppException não foram encontradas na referência analisada.

Ordem relativa: RequestLanguageMiddleware → RequestLoggingMiddleware → ExceptionHandlingMiddleware → proteções locais → endpoints.

- Idioma: Accept-Language, RequestLanguage.Current, Content-Language.
- Logs: método, rota, status, duração, ErrorCode, TraceId, CorrelationId e JourneyId; validar/devolver X-Correlation-ID e X-Journey-ID. Adaptar para logs locais, sem dependência de telemetria cloud ou vazamento de dados.
- Exceptions: AppException mantém status/código/errors; BadHttpRequestException → 400/bad_request, UnauthorizedAccessException → 401/unauthorized, exceção inesperada → 500/internal_server_error. Mapear unicidade SQLite para Conflict, sem vazar SQL/stack trace.
- `InvalidModelStateResponseFactory` retorna erro localizado no contrato global, sem ProblemDetails inesperado.
- `LocalOriginMiddleware` da base protege a origem das requisições de navegador. API somente em loopback; detalhar proteção adicional antes de endpoints Git sensíveis.
- MirrorAccessReadOnlyMiddleware e AdministrativeAuditMiddleware do Auth são específicos de Blux: reutilizar o padrão quando houver regra equivalente, sem copiar claims ou justificativas administrativas como requisitos novos.

Erro global de exceptions/model binding:

```json
{"success":false,"errors":["Mensagem localizada"],"errorCode":"validation_error","traceId":"id"}
```

Esse erro não exige result/messages/totalRows. O frontend deve aceitá-lo além do envelope de negócio. Falhas individuais de operação coletiva são resultados por repositório. Middlewares HTTP não capturam automaticamente falhas em jobs/SignalR; tratar esses limites com os contratos e a correlação correspondentes.

Documentação obrigatória: Microsoft.AspNetCore.OpenApi e Scalar.AspNetCore; `/openapi/v1.json`, `/scalar/v1`, IDs únicos e contratos/status/erros documentados. Sem substituir por Swashbuckle/NSwag/Swagger UI.

## Frontend

Em `src/frontend/src`, adaptar a referência Admin Angular para Vue, preservando responsabilidades:

- `app`: bootstrap, router e configuração.
- `core/api`: cliente HTTP base, ApiError e opções; `core/i18n/translations/{pt-BR,es,en}`; `core/realtime`: SignalR.
- `core/<domain>`: `<domain>-api.service.ts`, `models/requests`, `models/responses`, `stores` Pinia.
- `features/<feature>`: páginas, dialogs, componentes e composables exclusivos.
- `layout`: shell, menus e composição das páginas.
- `shared`: components, composables, models, utils, validators, constants, styles.

Componentes pequenos, Vue SFC, script setup TypeScript, Composition API, props/emits e dados/resultados de dialogs tipados. PrimeVue como biblioteca. Ref/computed para estado local; Pinia para compartilhado; sem estado duplicado. Rotas lazy via Vue Router, cleanup SignalR ao desmontar. Páginas tratam loading, sucesso, erro e vazio. Formulários validam UX sem substituir o backend.

HTTP apenas nos serviços de domínio. Cliente base concentra URL/query, Accept-Language, timeout, cancelamento, success e normalização ApiError com status/origem/traceId. Models readonly, camelCase, kebab-case nos arquivos. Não usar any para contornar contratos. Prettier e editorconfig orientam formatação.

## Três idiomas

pt-BR, es e en são requisitos do MVP nos dois lados. Normalizar PT para pt-BR, ES/es-PY/variantes para es, EN/variantes para en; fallback pt-BR. Referência Auth usa es-PY, mas o catálogo GitCosmos usa es.

Backend: RequestLanguage/LocalizedText.Get em Common/Localization, isolamento por requisição; localizar validações, binding, exceptions, erros inesperados, sucesso, vazio e progresso. Capturar o idioma explicitamente para operações em segundo plano/SignalR. Não traduzir identificadores de protocolo nem depender de stderr bruto para explicar erros ao usuário.

Frontend: seletor e preferência persistida, catálogos completos por área, Accept-Language centralizado, textos/formatação de datas e números por idioma. Mostrar errors[] do backend sem retradução por strings; fallback local traduzido para rede/timeout/cancelamento. Identificar mensagens por chaves/códigos estáveis quando necessário.

Preparar testes de três idiomas, fallback/normalização, isolamento concorrente, headers, erros e progresso. Executar somente manualmente conforme SKILL.md.

## Escopo MVP e regras Git

Cliente Git de uso diário com referência no Fork, sem promessa de cópia integral de todas as suas funcionalidades. MVP inclui:

- Criar/renomear/excluir workspaces e adicionar/remover repositórios locais válidos, com persistência SQLite.
- Dashboard com nome, caminho, branch, remote, working tree, alterações pendentes, ahead/behind e última operação.
- Abrir, inicializar e clonar repositórios; validar destinos para evitar sobrescrita.
- Arquivos alterados, adicionados, removidos e não rastreados; diferenças working tree/staging, binários, stage/unstage por arquivo/conjunto/trecho.
- Commits, amend, descarte protegido e edição de .gitignore.
- Histórico com grafo, detalhes e diferenças de commits/referências, pesquisa/filtros, histórico por arquivo e blame.
- Branches locais: listar/criar/renomear/excluir/checkout; criar a partir de remote e configurar upstream.
- Tags locais simples/anotadas, publicação e exclusão remota explícita; gerenciamento de remotes.
- Fetch/pull/push individual, seleção de branch/remote e publicação de branches.
- Stash: criar/listar/diff/apply/pop/excluir; merge/rebase/cherry-pick; revert e reset soft/mixed/hard.
- Conflitos: identificar, visualizar/editar ou abrir ferramenta externa, marcar resolvido, continuar/abortar operações.
- Fetch/Pull/Push/Checkout coletivos com seleção de repositórios, pré-validação, resultados individuais, progresso, logs, cancelamento e histórico.

SQLite armazena configurações/workspaces/histórico de operações, não arquivos/commits/branches do Git. Preferências de tema e idioma são salvas no navegador via localStorage. O mesmo repositório pode pertencer a vários workspaces. Caminho canônico deve evitar duplicação considerando Windows. Um vínculo workspace/repositório não se repete. Remoção de vínculo/workspace nunca apaga automaticamente o diretório.

Não presumir mesmo histórico/branch/remote entre repositórios. Pull padrão --ff-only, sem merge implícito; checkout coletivo não cria branch ausente. Sem --force ou rollback distribuído automático. Não há transação atômica entre repositórios: registrar falhas parciais e permitir retry seguro de falhos. Mutações no mesmo repositório serializadas inclusive entre workspaces; paralelismo limitado entre repositórios distintos. Cancelamento não reverte resultados concluídos.

Descarte, reset hard, exclusões e reescrita de histórico exigem escolha explícita e confirmação adequada no produto. Usar Git instalado com Credential Manager/SSH; não guardar tokens/senhas no banco. Validar caminhos e origem, limitar API ao loopback e evitar concatenação insegura de comandos.

## Execução, ferramentas e distribuição

Configuração inicial: SDK 10.0.300 em global.json com latestPatch, target net10.0, Node 24.21.0 em .nvmrc; respeitar os arquivos atuais caso tenham sido atualizados. Não atualizar versões por suposição.

Desenvolvimento em dois terminais:

```powershell
Set-Location 'C:\Repos\GitCosmos'
dotnet watch --project .\src\backend\GitCosmos.API
```

```powershell
Set-Location 'C:\Repos\GitCosmos\src\frontend'
npm run dev
```

API em 127.0.0.1:5210; Vite em 127.0.0.1:5173 encaminha /api. Em desenvolvimento, acessar 5173 para atualização automática. Abrindo a pasta frontend no VS Code, os perfis em .vscode/launch.json permitem F5 com Chrome ou Edge; tasks.json inicia o Vite e detecta a disponibilidade antes de abrir o navegador. Os perfis Vite already running reutilizam um servidor existente. Manter a API separada no Rider. A tarefa Vite pode ser encerrada com Ctrl+C no terminal dedicado; nenhum perfil executa testes automaticamente.

Execução integrada:

```powershell
Set-Location 'C:\Repos\GitCosmos'
.\scripts\Build.ps1
dotnet run --project .\src\backend\GitCosmos.API
```

Build.ps1 compila o Vue, copia dist para API/wwwroot e compila .NET. ASP.NET serve os arquivos estáticos; Vue roda no navegador. Node é necessário para desenvolver/compilar, não para servir a versão pronta. dotnet run sozinho não recompila o Vue: 5210 apresenta a última cópia de wwwroot. Parar, recompilar e reiniciar para atualizar o modo integrado.

Não são necessários Docker, PostgreSQL, Azure, servidor SQLite ou instalação global de Vue/Vite. npm ci e dotnet restore restauram dependências de projeto. Não executar esses exemplos automaticamente ao consultar a skill.

Distribuição futura Windows self-contained com runtime .NET e frontend compilado; detalhes de instalador/executável/abertura do navegador continuam pendentes. Não publicar release por iniciativa própria.

## Roadmap e próximo passo

1. Fundação .NET/Vue, contratos, middlewares, idiomas, Scalar e CI. Base inicial implementada; confirmação manual final de testes pendente até evidência do usuário.
2. Workspaces: modelar Workspace, GitRepository e WorkspaceRepository; validar relacionamentos/regras/contratos antes da migration, implementar criação/listagem ponta a ponta e então completar CRUD e vínculos.
3. Dashboard, histórico e diferenças.
4. Fluxo básico de repositório individual.
5. Integração e recuperação Git.
6. Operações coletivas.
7. Execução e observabilidade, consolidando controles de segurança/concorrência já presentes desde cada operação.
8. Distribuição v0.1.0.

A estimativa antiga de 8–12 semanas a 6–10 horas semanais foi invalidada pela ampliação do MVP e precisa de reestimativa. Não prometer esse prazo.

Futuro: v0.2 import/export JSON portável e perfis de branches; v0.3 snapshots e SHAs/restauração protegida; v1.0 polimento/estabilidade/documentação. Clonagem, histórico e diferenças já são MVP. Colaboração, sincronização e cloud opcionais permanecem futuro. JSON deve usar URLs e dados portáveis, não caminhos locais universais.

Fundação existente: quatro camadas, tela Vue com idiomas, GET /api/System/GetStatus, contratos, exceptions/middlewares, Scalar, testes, scripts e workflow. DbContext sem entidades/migrations; SignalR registrado sem hubs de operações. Não descrever funcionalidades planejadas como já implementadas.

Pendências: schema/migrations, contratos específicos, proteção de endpoints sensíveis, eventos SignalR, concorrência padrão, cancelamento de processos, autenticação Git interativa, retenção de logs, instalador, JSON, identidade visual e prazo. Consultar sempre o contexto/código atuais para não perpetuar este retrato inicial.

## Manutenção da skill

Ao alterar uma decisão do projeto, atualizar o contexto e as instruções afetadas desta skill, evitando contradições. O contexto guarda os detalhes e o estado corrente; a skill guarda o comportamento reutilizável. A preferência de testes manuais vale também para futuras conversas e só muda por instrução explícita do usuário. Entregas sempre incluem os comandos manuais de testes aplicáveis (ou a referência opcional em mudanças documentais), os comandos para executar o projeto e uma sugestão de mensagem de commit em inglês. Todos os blocos PowerShell de testes e execução devem incluir Set-Location com o caminho absoluto correto. Isso não autoriza executar testes, iniciar a aplicação ou fazer commit/push pelo chat.
