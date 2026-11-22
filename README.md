# GitCosmos

Um cliente Git local para gerenciar repositórios independentes em um workspace unificado.

As Milestones 1 e 2, incluindo seleção nativa, clonagem e adição automática de repositórios, foram validadas pelos testes manuais do usuário. A Milestone 3 está em andamento: seu primeiro incremento implementa o dashboard com branch e estado local dos arquivos, aguardando novos testes e cobertura.

## Requisitos

- Windows, Git no PATH, .NET SDK 10.0.300 (ou patch compatível com `global.json`).
- Node.js 24.21.0 e npm.

## Preparar

Na raiz do repositório:

```powershell
dotnet restore GitCosmos.slnx
cd src/frontend
npm ci
```

## Desenvolvimento com atualização automática

Durante o desenvolvimento, backend e frontend são executados em processos separados. O .NET executa a API; o Node.js executa o servidor de desenvolvimento do Vite, que atualiza a interface ao editar os arquivos Vue.

Terminal 1 — backend:

```powershell
Set-Location C:\Repos\GitCosmos
dotnet watch --project .\src\backend\GitCosmos.API
```

Terminal 2 — frontend:

```powershell
Set-Location C:\Repos\GitCosmos\src\frontend
npm run dev
```

- Interface de desenvolvimento: [http://127.0.0.1:5173](http://127.0.0.1:5173).
- Scalar: [http://127.0.0.1:5210/scalar/v1](http://127.0.0.1:5210/scalar/v1).
- OpenAPI: [http://127.0.0.1:5210/openapi/v1.json](http://127.0.0.1:5210/openapi/v1.json).
- Estado local: GET [http://127.0.0.1:5210/api/System/GetStatus](http://127.0.0.1:5210/api/System/GetStatus).

O Vite encaminha as chamadas `/api` recebidas na porta `5173` para o backend na porta `5210`, preservando o Host original para a validação de origem da API. Durante a edição do frontend, acesse a porta `5173` para acompanhar as alterações.

### Debug do frontend no Visual Studio Code

1. Abra **a pasta `C:\Repos\GitCosmos\src\frontend`** no VS Code (File → Open Folder). As configurações estão na pasta `.vscode` desse frontend; não é necessário abrir a solução .NET no VS Code.
2. Instale as dependências com `npm ci`, se ainda não tiver feito a preparação inicial.
3. Inicie o backend no Rider, mantendo a API em `http://127.0.0.1:5210`.
4. Em **Run and Debug**, selecione **GitCosmos: Frontend (Chrome)** ou **GitCosmos: Frontend (Edge)**.
5. Clique em **Run → Start Debugging** ou pressione **F5**.

O VS Code inicia a tarefa `GitCosmos: start Vite`, aguarda a mensagem de disponibilidade na porta `5173` e abre o navegador escolhido com o depurador conectado. O navegador correspondente precisa estar instalado. Os source maps permitem depurar o código fonte do frontend; coloque breakpoints no TypeScript ou no bloco `<script setup>` dos componentes Vue. As alterações continuam sendo atualizadas pelo Vite ao salvar.

Se você já iniciou `npm run dev` pelo terminal, selecione a configuração correspondente com **Vite already running**. Ela abre o navegador com debug sem tentar iniciar um segundo servidor na mesma porta. Esses perfis também servem para reutilizar o Vite entre sessões de debug.

O botão de parar encerra a sessão de debug do navegador. A tarefa Vite permanece no terminal dedicado do VS Code; para encerrar o servidor, use **Ctrl+C** nesse terminal ou **Terminal → Terminate Task** e selecione `GitCosmos: start Vite`. Nenhuma configuração inicia ou interrompe o backend do Rider, instala dependências ou executa testes automaticamente.

Os arquivos de configuração são `src/frontend/.vscode/launch.json` (perfis de navegador) e `src/frontend/.vscode/tasks.json` (inicialização e detecção de disponibilidade do Vite).

A interface permite selecionar português, espanhol ou inglês e consultar o estado do backend. Ela apresenta mensagens localizadas, carregamento e erros de conexão. Acesse **Workspaces** na navegação para criar e listar workspaces persistidos no SQLite. Os botões de cada workspace permitem renomear e confirmar a exclusão. O botão **Repositórios** permite adicionar um diretório Git existente, listar os vínculos e confirmar sua remoção. As operações Git de edição e sincronização serão implementadas nas próximas milestones.

## Workspaces — Milestone 2

- Criar workspace com nome obrigatório de até 100 caracteres, removendo espaços das extremidades.
- Nomes ativos únicos sem diferenciar maiúsculas/minúsculas; conflitos retornam HTTP 409 localizado.
- Listar com paginação de base zero, 10 itens por página na interface e ordenação por nome.
- Renomear com as mesmas regras do cadastro, permitindo mudar a capitalização do próprio nome.
- Excluir após confirmação: o workspace fica inativo no banco e seus vínculos são removidos em uma transação. Repositórios e arquivos no computador são preservados.
- Estados de carregamento, lista vazia, erro com retry e mensagens em pt-BR, es e en.
- API: `POST /api/Workspace/CreateWorkspace` com `{ "name": "Meu workspace" }`, `GET /api/Workspace/GetAllWorkspaces?page=0&pageSize=10`, `PUT /api/Workspace/UpdateWorkspace` com `{ "id": 1, "name": "Novo nome" }` e `DELETE /api/Workspace/DeleteWorkspace?id=1`, documentadas no Scalar.
- IDs inválidos retornam HTTP 400, workspaces inexistentes/inativos retornam 404 e conflitos de nome retornam 409, com mensagens nos três idiomas.

O tooling EF 10.0.8 está fixado em `dotnet-tools.json`; para gerar migrations futuras, execute `dotnet tool restore` na raiz. Essa ferramenta não precisa ser executada para iniciar a aplicação.

Teste manual do fluxo: abra Workspaces, crie um nome e tente repeti-lo com outra capitalização. Renomeie, confira a persistência após reiniciar, cancele uma exclusão e depois confirme-a. Após renomear/excluir, a lista volta à primeira página para refletir a ordenação e evitar uma página vazia. Este incremento não altera o schema e não exige nova migration. O gerenciamento de repositórios descrito abaixo completa o escopo funcional da Milestone 2; esse incremento foi validado pelo usuário antes das melhorias de seleção de pasta e clonagem.

## Repositórios locais nos workspaces

No botão **Repositórios** de cada workspace, informe um caminho absoluto, sem aspas, para um diretório existente. O Git identifica a raiz da árvore de trabalho: informar uma subpasta produz o mesmo vínculo que informar a raiz. Caminhos com espaços e acentos, repositórios sem commits e worktrees vinculadas são aceitos. Repositórios bare são rejeitados porque não possuem árvore de trabalho.

- A identidade usa o caminho canônico retornado pelo Git, sem separador final e sem diferenciar maiúsculas/minúsculas no Windows. A validação usa [`git rev-parse --path-format=absolute --show-toplevel`](https://git-scm.com/docs/git-rev-parse.html).
- Um repositório pode pertencer a vários workspaces. Tentar repeti-lo no mesmo workspace retorna HTTP 409.
- Nome e caminho ficam visíveis na lista; a contagem do workspace é atualizada após adicionar/remover.
- **Remover vínculo** pede confirmação e afeta somente o workspace selecionado. Preserva os arquivos e os vínculos nos outros workspaces. A remoção continua disponível se o diretório tiver sido movido ou apagado fora do GitCosmos.
- Caminhos inválidos retornam 400; workspace/vínculo inexistente retorna 404. Git indisponível ou validação acima de 10 segundos gera mensagem localizada. O runner encerra o processo em timeout/cancelamento; requisições desconectadas não geram erro 500.
- Criação do registro e vínculo ocorre em uma transação SQLite, com verificação de workspace ativo e índices únicos. Não há nova migration neste incremento.

Endpoints documentados no Scalar:

- `GET /api/WorkspaceRepository/GetRepositories?workspaceId=1` — lista os vínculos, ordenados por nome/caminho.
- `POST /api/WorkspaceRepository/AddRepository` — JSON com `workspaceId` e `localPath` (o cliente HTTP serializa as barras do caminho).
- `DELETE /api/WorkspaceRepository/RemoveRepository?workspaceId=1&repositoryId=2` — remove o vínculo.

Teste manual sugerido: adicione a raiz de um repositório, tente repeti-la usando uma subpasta, vincule-o a outro workspace e remova o primeiro vínculo. Confira a persistência após reiniciar a API e a preservação dos arquivos e do segundo vínculo. As suites incluem testes isolados com Git real em diretórios temporários; o Git precisa estar no PATH do processo que executa os testes. As suites permanecem de execução manual.

## Selecionar pastas e clonar repositórios

### Adicionar repositórios de uma pasta principal

Em **Adicionar repositório local**, escolha ou digite a pasta e clique em **Adicionar repositório**. A detecção é automática, sem checkbox: se a pasta for a raiz de um repositório Git válido, somente ela será adicionada. Caso contrário, o sistema verifica apenas suas subpastas imediatas.

Por exemplo, se `C:\Repos\ReposTestes` não for uma raiz Git, o sistema verifica `GitCosmosTest1`, `GitCosmosTest2` e `OutraPasta`, que são seus filhos imediatos. Somente as pastas que forem raízes de árvores de trabalho Git válidas são vinculadas. Um repositório em `OutraPasta\RepositorioValido` não será encontrado nem adicionado. Se a pasta selecionada já for uma raiz Git, seus filhos não serão pesquisados. Não há busca recursiva, e links/junctions de diretórios são excluídos da enumeração.

O resultado apresenta o caminho e a situação de cada pasta: adicionada, já vinculada, ignorada ou com falha, além de um resumo com as quantidades. Duplicados não geram vínculos extras. Cada inclusão é independente: resultados anteriores permanecem vinculados se uma pasta posterior falhar ou a requisição for cancelada. O caminho principal é preservado para uma nova tentativa. Nenhum arquivo é alterado e não há nova migration.

Endpoint no Scalar: `POST /api/RepositoryImport/ImportRepositories`, com `{ "workspaceId": 1, "localPath": "C:/Repos/ReposTestes" }`. Retorna `CustomResponse` com `result.items` contendo `localPath`, `status` (`added`, `duplicate`, `ignored`, `failed`) e `message` localizada. O cliente não aplica timeout HTTP fixo a essa operação; cada inspeção Git mantém seu limite de 10 segundos. Falhas de execução do Git na pasta selecionada são retornadas como erro, sem iniciar uma busca alternativa.

### Seleção de pasta e clonagem

Ao abrir **Repositórios** de um workspace, escolha **Adicionar repositório local** ou **Clonar repositório**. O caminho pode ser digitado ou informado pelo botão **Selecionar pasta…**, que abre o seletor nativo do Windows e retorna o caminho completo. Cancelar a seleção preserva o valor anterior. A API precisa estar rodando em uma sessão interativa do Windows, no mesmo computador; digitar o caminho continua disponível quando o seletor não puder ser aberto. Nenhum arquivo é enviado pelo navegador.

Para clonar:

1. Informe a URL remota e uma pasta existente onde o novo diretório será criado.
2. Clique em **Revisar destino**. Por exemplo, `https://github.com/RafaelKampa/GitCosmosTest2.git` e `C:/Repos` apresentam `C:\Repos\GitCosmosTest2` como pasta a criar.
3. Confira o caminho e confirme a clonagem. O sistema executa Git e vincula o repositório ao workspace automaticamente.

O destino final não pode existir, mesmo vazio; arquivos existentes nunca são sobrescritos pelo fluxo de clone. A clonagem usa uma pasta temporária própria e só publica a pasta final ao concluir. O botão de cancelamento encerra a operação em andamento e tenta limpar essa pasta temporária. Um clone já concluído é preservado caso o vínculo ao workspace falhe; a mensagem orienta adicioná-lo como repositório local.

A seleção e a clonagem não têm o timeout HTTP de 30 segundos das consultas comuns. Há no máximo duas clonagens simultâneas no processo da API. Autenticação utiliza o Git Credential Manager ou SSH já configurado no computador; não informe tokens ou senhas na URL. HTTPS, HTTP, SSH, git e o formato SSH `git@host:owner/repository.git` são aceitos.

Endpoints adicionais, com mensagens em pt-BR, es e en e documentação no Scalar:

- `POST /api/LocalFolder/SelectFolder`: `{ "initialPath": "C:/Repos" }`; cancelar retorna `cancelled: true` sem alterar a entrada.
- `POST /api/RepositoryClone/Preview`: `{ "workspaceId": 1, "remoteUrl": "https://github.com/RafaelKampa/GitCosmosTest2.git", "parentPath": "C:/Repos" }`; calcula e valida o destino sem clonar.
- `POST /api/RepositoryClone/CloneRepository`: mesmos campos, mais `destinationPath` exatamente como recebido na revisão; clona e cria o vínculo.

Não há nova migration nem dependência de pacote para essas melhorias. Os novos testes usam repositórios temporários locais e não clonam a URL de exemplo. A validação manual deve conferir a seleção/cancelamento de pastas, a revisão do destino, a clonagem e a rejeição de uma pasta final já existente.
## Dashboard do workspace — Milestone 3

Na lista de workspaces, clique em **Abrir dashboard**. A página `/workspaces/{id}` mostra o nome do workspace e um card por repositório, com nome, caminho, branch atual e estado **Limpo**, **Com alterações**, **Com conflitos** ou **Indisponível**. HEAD destacado e repositórios sem commits são identificados explicitamente.

Os cards mostram contagens de arquivos alterados, modificados, adicionados, excluídos, renomeados, não rastreados e em conflito, além de preparados/não preparados para commit. O total de alterados conta cada entrada Git uma vez; as categorias podem se sobrepor (por exemplo, um arquivo pode estar preparado e também ter novas modificações). Cópias são contabilizadas como adições; alterações de tipo como modificações. Arquivos ignorados não entram na contagem; submódulos aparecem como entradas do repositório principal.

A consulta usa [`git status --porcelain=v2 -z`](https://git-scm.com/docs/git-status), com nomes separados por NUL e detecção de renomeações, sem editar arquivos, atualizar o índice ou acessar remotes. As consultas são sequenciais por workspace, com no máximo duas leituras de estado simultâneas no processo da API. Cada comando mantém o timeout de 10 segundos; o cliente não aplica timeout fixo ao conjunto e cancela a requisição ao sair da página ou trocar workspace/idioma. Uma pasta removida, movida ou que deixou de ser raiz Git aparece indisponível sem ocultar os demais repositórios.

Clique em **Atualizar estado** para consultar novamente após alterações externas. Não há atualização em tempo real neste incremento. As mensagens de erro são localizadas em pt-BR/es/en. A navegação de retorno permite gerenciar os vínculos pelo fluxo existente.

Endpoint no Scalar: `GET /api/WorkspaceDashboard/GetDashboard?workspaceId=1`, retornando `CustomResponse<WorkspaceDashboardResponse>` com identificação do workspace, instante de conclusão da leitura (`checkedAt`) e resultados por repositório. IDs inválidos retornam 400 e workspaces inexistentes/inativos retornam 404. Sem nova migration ou dependência.

Os testes preparados cobrem parsing, Git real em diretórios temporários (incluindo preservação do índice), disponibilidade parcial, validações, cancelamento, contratos HTTP, componentes e navegação. Execute-os manualmente com o script indicado abaixo. Para validar visualmente, abra um workspace com repositórios limpos, alterados e um caminho indisponível; confira branch, contagens, atualização e os dois temas. Histórico de commits e visualização de diferenças permanecem como próximos incrementos da Milestone 3.

## Marca e temas

A logo PNG fornecida é exibida no cabeçalho a partir de `src/frontend/src/shared/assets/branding`, preservando sua transparência e sem fundo aplicado por CSS. O favicon e o ícone para dispositivos Apple usam o PNG transparente de `src/frontend/public`. As versões SVG permanecem disponíveis nos assets originais, sem uso na interface atual.

O botão de tema no cabeçalho alterna entre **Cosmic Dark** e **Cosmic Light**, preservando a escolha no navegador (`gitcosmos.theme`). Cosmic Dark é o padrão para a primeira visita e para preferências inválidas. Uma escolha clara já salva é respeitada; trocar o padrão não apaga a preferência do usuário. O idioma também é persistido localmente (`gitcosmos.language`). A logo é apresentada diretamente sobre o fundo do cabeçalho nos dois temas.

A interface utiliza as paletas Cosmic Light e Cosmic Dark, com cores centralizadas nos tokens globais.

Todas as cores customizadas ficam em `src/frontend/src/shared/styles/tokens.css`: os tokens semânticos `--gc-*` reproduzem as paletas aprovadas para fundo, superfícies, texto, bordas, ações e estados Git. Os aliases `--color-*` conectam esses tokens à interface e ao preset PrimeVue. Cosmic Dark é a base de `:root`; `:root[data-theme='cosmic-light']` aplica o tema claro. Hover e escalas auxiliares são derivados dessas cores, sem valores fixos nos componentes. A classe `.dark` sincroniza o modo do PrimeVue. Alterações na paleta devem ser refletidas nos tokens globais e nos seus usos na interface.

## Execução integrada com frontend compilado

Neste modo, o ASP.NET Core serve a API e os arquivos estáticos do frontend no mesmo processo. Após preparar as dependências, execute:

```powershell
Set-Location C:\Repos\GitCosmos
.\scripts\Build.ps1
dotnet run --project .\src\backend\GitCosmos.API
```

O `Build.ps1` executa `npm run build`, compila o Vue em HTML, CSS e JavaScript, copia o resultado para `src/backend/GitCosmos.API/wwwroot` e compila a solução .NET. Acesse a aplicação em [http://127.0.0.1:5210](http://127.0.0.1:5210).

O Vue é executado no navegador. O Node.js é necessário para desenvolver e compilar o frontend, mas não precisa permanecer em execução para servir a versão já compilada. Esse modelo será a base para distribuir o GitCosmos como uma aplicação local única.

**O comando `dotnet run` não recompila o frontend automaticamente.** A porta `5210` apresenta a última versão copiada para `wwwroot`. Para atualizar essa versão após alterar o Vue, interrompa a aplicação, execute novamente `Build.ps1` e inicie a API. Para desenvolvimento contínuo, utilize os dois terminais e a porta `5173`.

## Testes e cobertura

Execute manualmente, na raiz, após restaurar as dependências:

```powershell
Set-Location C:\Repos\GitCosmos
.\scripts\Test.ps1
```

Os testes unitários devem atingir no mínimo 95% de linhas e branches em backend e frontend. Os catálogos estáticos em `src/core/i18n/translations` são omitidos do relatório de cobertura porque não possuem itens executáveis contabilizados (0/0). Os testes de idiomas, consistência das chaves e mensagens dos componentes continuam ativos; os limiares de cobertura não foram reduzidos. O script falha se os testes ou a cobertura não passarem. Relatórios: `artifacts/coverage/backend` e `src/frontend/coverage`. A CI usa os mesmos scripts e publica os relatórios.

Por preferência do responsável, agentes devem fornecer esses comandos para execução manual, sem iniciar testes automaticamente. A CI continua validando builds e cobertura nos pushes e pull requests.

## Organização

- `src/backend`: API, Application, Domain e Infrastructure.
- `src/frontend/src`: app, core, features, layout e shared.
- `tests`: testes unitários backend.
- `scripts`: build e validação.

SQLite e EF Core possuem o modelo inicial de Workspace, GitRepository e WorkspaceRepository. A API aplica migrations pendentes automaticamente antes de aceitar requisições, sem recriar o banco. A conexão padrão usa `Data Source=gitcosmos.db`, relativa ao diretório de trabalho do processo. Para fixar o local, configure `ConnectionStrings__GitCosmos` com caminho absoluto. O relacionamento permite compartilhar repositórios entre workspaces e impede vínculos repetidos; esses vínculos já podem ser gerenciados pela interface e pela API. O MVP usa Git CLI, respeita a autenticação já configurada no computador e não requer Azure ou Docker.

Licença MIT. Consulte [LICENSE](LICENSE).
