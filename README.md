# GitCosmos

Um cliente Git local para gerenciar repositórios independentes em um workspace unificado.

As Milestones 1 e 2, incluindo seleção nativa, clonagem e adição automática de repositórios, foram validadas pelos testes manuais do usuário. A Milestone 3 está em andamento. A interface utiliza Cosmic Explorer e Mission Control, com navegação persistente, catálogo global, alterações/diffs e histórico individual com grafo de commits. O incremento inicial de histórico foi validado por testes manuais: backend com 276 testes aprovados, 98,90% de linhas e 97,87% de branches; frontend com 210 testes aprovados, 100% de linhas e 99,86% de branches. Filtros por branch e busca de commits foram implementados e aguardam validação manual deste incremento; histórico de arquivos e blame continuam planejados.

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

Na lista de workspaces, clique em **Abrir dashboard**, ou selecione o workspace no Explorer. A página `/workspaces/{id}` reutiliza a visão de repositórios do Mission Control, filtrada pelo workspace, com lista compacta ou cards, busca, favoritos, seleção e estado **Limpo**, **Com alterações**, **Com conflitos** ou **Indisponível**. O gerenciamento de vínculos continua disponível no botão **Repositórios**. As contagens detalhadas dos arquivos e os diffs ficam na tela individual.

O resumo mostra total de repositórios, modificados, atrás e à frente do upstream. Ahead/behind usam as referências locais existentes e não executam fetch; a falta de dados de rastreamento é indicada por `—`. O resumo conta repositórios, enquanto a linha/card mostra a quantidade individual de commits à frente/atrás. No detalhe, o total de arquivos alterados conta cada entrada Git uma vez; categorias podem se sobrepor. Arquivos ignorados não entram na contagem; submódulos aparecem como entradas do repositório principal.

A consulta usa [`git status --porcelain=v2 -z`](https://git-scm.com/docs/git-status), com nomes separados por NUL e detecção de renomeações, sem editar arquivos, atualizar o índice ou acessar remotes. As consultas são sequenciais por workspace, com no máximo duas leituras de estado simultâneas no processo da API. Cada comando mantém o timeout de 10 segundos; o cliente não aplica timeout fixo ao conjunto e cancela a requisição ao sair da página ou trocar workspace/idioma. Uma pasta removida, movida ou que deixou de ser raiz Git aparece indisponível sem ocultar os demais repositórios.

Clique em **Atualizar estado** para consultar novamente após alterações externas. Não há atualização em tempo real neste incremento. As mensagens de erro são localizadas em pt-BR/es/en. A navegação de retorno permite gerenciar os vínculos pelo fluxo existente.

Endpoint no Scalar: `GET /api/WorkspaceDashboard/GetDashboard?workspaceId=1`, retornando `CustomResponse<WorkspaceDashboardResponse>` com identificação do workspace, instante de conclusão da leitura (`checkedAt`) e resultados por repositório. IDs inválidos retornam 400 e workspaces inexistentes/inativos retornam 404. Sem nova migration ou dependência.

Os testes preparados cobrem parsing, Git real em diretórios temporários (incluindo preservação do índice nas consultas), disponibilidade parcial, validações, cancelamento, contratos HTTP, componentes e navegação. Execute-os manualmente com o script indicado abaixo. Para validar visualmente, abra um workspace com repositórios limpos, alterados e um caminho indisponível; confira branch, contagens, atualização e os dois temas. A visualização individual de alterações locais está descrita abaixo; o histórico de commits e suas diferenças estão descritos na seção Histórico de commits.

## Abrir repositório e visualizar diferenças locais

No dashboard, clique em **Abrir repositório** no item desejado. A rota `/workspaces/{workspaceId}/repositories/{repositoryId}` abre uma tela com nome, caminho e branch do repositório. A lista lateral separa arquivos **Preparados para commit** e **Não preparados**. Um arquivo com alterações em ambos os lados aparece nos dois grupos. Conflitos ficam no grupo não preparado. Nomes e caminhos originais de renomeações são preservados.

Selecione um arquivo para ver o diff unificado, com números de linhas, contexto e adições/remoções destacadas pelos tokens dos temas Cosmic. A comparação preparada é entre HEAD e índice; a não preparada, entre índice e árvore de trabalho. Repositórios sem commits são aceitos. Arquivos novos não rastreados têm prévia textual UTF-8 apresentada como adições; arquivos vazios ou sem diferenças textuais têm mensagem própria.

Binários, conflitos e submódulos são identificados com explicações específicas. O diff textual de conflitos e o conteúdo interno de submódulos ficam para etapas posteriores. Saídas acima de 200.000 caracteres ou 4.000 linhas não são renderizadas; arquivos novos também têm limite de leitura de 200.000 bytes. Conteúdo não UTF-8 ou com NUL na prévia de arquivo novo é tratado como binário. Há limite de 1.000.000 de caracteres para a listagem de status. O processo continua drenando a saída ao atingir o limite, mantendo uso de memória limitado e respeitando timeout/cancelamento.

O backend verifica workspace ativo e vínculo em cada consulta, usa o caminho persistido e revalida a lista de alterações antes de gerar o diff. Seleções removidas ou que mudaram de grupo retornam 409 e pedem atualização. Caminhos são tratados literalmente, com rejeição de escapes do diretório, metadados `.git` e links/junctions em caminhos de arquivos. O [`git diff`](https://git-scm.com/docs/git-diff) é chamado sem shell, diff externo ou textconv. São apenas consultas: não há stage, unstage, commit ou escrita nos arquivos/índice por esta tela.

Use **Atualizar estado** após edições externas. Trocar arquivo, repositório ou idioma, atualizar a lista ou sair cancela as consultas anteriores e descarta respostas tardias. Falhas de diff preservam a seleção e permitem **Tentar novamente**. Listagens e comparações têm mensagens em pt-BR/es/en. Não há dependências ou migrations novas.

Endpoints documentados no Scalar, ambos com envelope `CustomResponse`:

- `GET /api/RepositoryChanges/GetChanges?workspaceId=1&repositoryId=2`: identificação, branch/contagens e arquivos com os estados de índice e working tree.
- `GET /api/RepositoryChanges/GetDiff?workspaceId=1&repositoryId=2&path=src%2Fexample.txt&area=working`: comparação de um arquivo selecionado; `area` aceita `working` ou `staged`. O frontend codifica o caminho em parâmetros de consulta.

Validação manual sugerida: escolha um arquivo modificado, prepare parte das alterações por uma ferramenta externa e edite novamente. Compare os dois grupos, confira um arquivo novo, um binário e uma renomeação; troque rapidamente de arquivo e teste atualização e os dois temas. Os testes unitários preparados usam apenas fixtures temporárias locais, sem serviços externos; sua execução continua manual. O histórico/grafo e o fluxo de Stage/Unstage/Commit foram acrescentados nos incrementos seguintes. Staging por trechos e comparação lado a lado continuam pendentes.

## Cosmic Explorer e Mission Control

A entrada `/` é o **Mission Control**: quatro indicadores de estado, atalhos para workspaces e repositórios do catálogo único. **Todos os repositórios** (`/repositories`) compartilha a mesma base visual; a visão de workspace aplica seu escopo. A logo PNG aprovada permanece no cabeçalho, sem fundo branco. Cores, foco, seleção e estados usam os tokens Cosmic existentes; H1 mantém a cor de marca aprovada.

O Explorer permanece à esquerda, com workspaces expansíveis e a seção **Sem workspace**. Nomes longos são abreviados visualmente; a largura pode ser ajustada em **Configurações**. O botão do cabeçalho recolhe a árvore. Em telas estreitas ela começa recolhida se não houver preferência salva. A barra inferior informa a disponibilidade do catálogo local e o total registrado. A antiga página de conexão continua em `/connection`, acessível por Configurações.

Nome/link navega; checkbox seleciona para um contexto independente. **Selecionar os visíveis** considera apenas as linhas resultantes da busca/filtros. Alternar lista/cards e trocar tema não apaga a seleção ou os filtros. Favoritos, tema, idioma, visualização, largura e recolhimento do Explorer ficam no navegador; filtros/seleção por escopo e aba do repositório permanecem durante a sessão.

**Adicionar repositório** no catálogo permite selecionar uma raiz Git ou um contêiner para detecção automática dos filhos imediatos, sem criar um workspace implícito. **Clonar repositório** também funciona sem workspace, preservando revisão/confirmação do destino e autenticação Git existente. Um cadastro por caminho canônico é reutilizado em todos os contextos. Remover um vínculo ou excluir workspace mantém o registro global e os arquivos; sem vínculos ativos, ele aparece em Sem workspace. A rota `/repositories/{id}` abre os diffs diretamente, sem depender de workspace.

O detalhe do repositório tem cabeçalho compacto, indicadores de arquivos/ahead/behind e somente duas abas centrais: **Alterações locais** e **Histórico**. No Explorer, cada repositório possui uma seta independente para expandir sua navegação: atalhos para essas duas abas e grupos **Branches**, **Remotes**, **Tags**, **Stashes** e **Submódulos**. A estrutura também atende repositórios sem workspace, com recuo progressivo entre os níveis da árvore. Expandir o repositório não navega nem consulta o Git; abrir **Branches** consulta as branches locais e **Remotes** consulta as referências remotas já presentes no computador. Nenhum fetch ocorre ao abrir esses grupos. O estado de expansão do repositório permanece durante a sessão, separado por workspace/repositório. Listagem de Tags, Stashes e Submódulos continua futura; criar uma tag já está disponível no menu da branch.

### Branches locais

Expanda um repositório no Explorer e abra **Branches**. A lista identifica a branch atual, upstream conhecido localmente e uso em outra worktree. **Buscar branch por nome** filtra a lista sem diferenciar caixa. Clicar no nome apenas abre o histórico e move a seleção para seu commit da ponta; conserva **Todos os commits** ou qualquer filtro que você tenha aplicado explicitamente. Não faz checkout nem filtra automaticamente a ancestralidade dessa branch. Se necessário, carrega páginas adicionais e revela o commit recolhido dentro de um merge, mantendo os demais commits. Se o filtro excluir a ponta, os detalhes continuam acessíveis com aviso e o filtro é mantido.

Cada linha possui um botão **⋯** com o menu daquela branch. O menu pode ser usado pelo teclado (setas, Home, End e Escape), fecha ao escolher uma ação ou clicar fora e abre um diálogo específico da operação. Não há formulário genérico de ações no sidebar. As mutações exigem pré-validação, revisão de origem/destino/SHA e confirmação. **Copiar nome** usa a área de transferência sem operação Git.

- **Criar:** informe o nome e selecione a branch de origem. Cria a partir do SHA exibido, sem trocar a branch atual, configurar upstream ou propagar a criação para submódulos.
- **Renomear:** altera o nome da referência e preserva sua configuração local. Permite renomear a branch atual; bloqueia branches em uso em outra worktree e destinos já existentes.
- **Trocar:** usa somente uma branch local existente e disponível. Exige árvore de trabalho limpa, incluindo ausência de arquivos não rastreados. Não descarta alterações, sobrescreve arquivos ignorados, atualiza submódulos ou cria branches por inferência de nomes remotos.
- **Excluir:** exige outra branch, fora de uso em qualquer worktree, com seus commits integrados ao HEAD atual. Usa `git branch -d`, sem exclusão forçada, e preserva os arquivos. O Git ainda pode recusar a exclusão por suas verificações de upstream.

O menu local também oferece **checkout em nova worktree**, **fast-forward para upstream**, **push**, **Pull Request**, **merge na branch atual**, **rebase normal/interativo da branch atual sobre a selecionada**, **nova tag** e **definir/remover tracking**. Fast-forward valida ancestralidade; para uma branch não ativa usa atualização da referência com SHA anterior esperado, sem sobrescrever commits divergentes. Push envia o SHA revisado a uma branch/remoto explícitos, sem force, mirror, tags ou submódulos implícitos. Worktree exige uma pasta absoluta nova, fora do repositório atual, com pai existente e sem junctions/links nos pais. Tags podem ser simples ou anotadas com mensagem, sem publicação automática.

### Branches remotas e integração

O grupo **Remotes** organiza branches por remoto configurado, ignora aliases simbólicos como `origin/HEAD` e permite buscar nomes. Seu menu tem checkout, worktree, pull para a branch atual, merge, rebase normal/interativo, Pull Request, nova branch/tag, exclusão remota e cópia do nome. Checkout cria uma branch local com tracking; se o nome escolhido já existir e acompanhar aquela mesma referência, abre sua ponta local sem fazer pull. Worktree remota cria uma branch local nova com tracking na nova pasta. Uma branch em uso em outra worktree não pode ser reutilizada como checkout local.

**Fetch** é uma ação explícita no grupo Remotes. **Pull** faz fetch da branch escolhida e tenta merge `--ff-only`, preservando alterações e arquivos ignorados; divergência gera falha sem merge automático. A exclusão remota exige digitar o nome completo e usa `--force-with-lease` exclusivamente como comparação do SHA revisado para remover a referência: se o servidor mudou, a exclusão é recusada. Isso não habilita push forçado de commits ou reescrita de branches remotas. URLs/destinos de push são revalidados e incluídos apenas no hash do estado; credenciais permanecem no Git Credential Manager/SSH.

Merge e rebase exigem árvore limpa e branch atual ativa. Rebase avisa sobre reescrita e referências remotas conhecidas; não usa autostash, atualizações implícitas de outras branches nem push automático. O modo interativo permite reordenar de 1 a 200 commits lineares e escolher **Pick**, **Drop**, **Squash** e **Fixup**; Squash combina mensagens existentes. Não aceita comandos `exec` ou texto livre na sequência. O editor de sequência é um arquivo temporário gerado pelo servidor com apenas ações/SHA validados e removido após a chamada. Merges dentro do intervalo exigem tratamento separado.

Conflitos ou cancelamentos podem deixar merge/rebase pendente. Os controles **Continuar** e **Abortar** exigem revisão, e abortar avisa sobre perda das resoluções daquela operação. A resolução/edição e preparação de conflitos ainda precisam ser feitas pela ferramenta Git/IDE externa; o fluxo inicial de Stage/Unstage continua bloqueando arquivos em conflito. Não há reset, descarte, retry ou rollback automático. Outros estados pendentes, como cherry-pick/revert/bisect, continuam bloqueando novas mutações.

**Pull Request** prepara um link HTTPS para o formulário do GitHub, GitLab.com, Bitbucket.org ou Azure DevOps, inclusive a partir dos formatos SSH suportados. A ponta local deve estar publicada na referência remota conhecida. O usuário abre o link para revisar e concluir no provedor; o aplicativo não publica PRs silenciosamente, não armazena tokens e não chama APIs de criação automaticamente. Servidores/provedores não reconhecidos retornam mensagem localizada.

Nomes têm até 255 caracteres e são validados pelo Git. A revisão inclui token de HEAD, referências, índice, status, worktrees, configuração de destino e request. Para rebase interativo, a edição da sequência exige nova pré-validação com o mesmo token de estado-base. O bloqueio de mutações é compartilhado com Stage/Unstage/Commit e com acessos por outros workspaces/worktrees. Ferramentas Git externas não participam desse bloqueio: em falha/cancelamento, atualizar antes de repetir. Sucesso ou resultado incerto atualiza o painel; o rascunho de commit será reiniciado, como informado na revisão.

API com `CustomResponse`, mensagens pt-BR/es/en e documentação Scalar: `GET /api/RepositoryBranch/GetBranches?repositoryId=2&workspaceId=1`, `GET /api/RepositoryBranch/GetResources?repositoryId=2&workspaceId=1`, `POST /api/RepositoryBranch/Prepare` e `POST /api/RepositoryBranch/Execute`. Use `workspaceId=0` para repositórios do catálogo sem contexto de workspace. O cliente envia IDs; o servidor resolve e valida a raiz cadastrada. Não há migration ou dependência nova. Testes deste incremento estão preparados e aguardam execução manual.

Stage, Unstage, Commit, Amend e Commit and Push possuem o fluxo individual descrito abaixo. Fetch/Pull/Push individuais estão nos menus de branches/grupo Remotes; os atalhos gerais no cabeçalho ainda não estão ligados a esses fluxos. O painel inferior **Operações** continua vazio para operações em lote, sem progresso fictício ou integração SignalR inexistente.

Novos endpoints do catálogo, todos documentados no Scalar com `CustomResponse`:

- `GET /api/RepositoryCatalog/GetExplorer`: metadados reais de workspaces ativos, repositórios únicos e IDs dos vínculos.
- `GET /api/RepositoryCatalog/GetOverview`: os mesmos dados com leitura real de estado Git por repositório.
- `POST /api/RepositoryCatalog/AddRepository`: `{ "localPath": "C:/Repos" }`, com relatório por pasta.
- `POST /api/RepositoryCatalog/Preview` e `/CloneRepository`: `remoteUrl`, `parentPath` e, na confirmação, `destinationPath`.
- `GET /api/RepositoryCatalog/GetChanges?repositoryId=1` e `/GetDiff?repositoryId=1&path=src%2Ffile.txt&area=working`: acesso ao registro global, com as mesmas proteções do diff.

As rotas e APIs existentes de workspace continuam disponíveis. Não há nova migration ou dependência. A validação visual considerou desktop, ambos os temas e largura de 390 px, usando dados locais reais. Execute manualmente a suíte completa para verificar testes e cobertura deste incremento.

### Alterações locais, Stage e commit

A seção **Alterações locais** apresenta duas árvores à esquerda: **Unstaged** acima e **Staged** abaixo, com pastas recolhíveis, ícones e indicadores `M` amarelo, `+` verde para adicionados (inclusive arquivos novos ainda não rastreados) e `−` vermelho para removidos. O diff fica à direita. O formulário com mensagem, descrição, checkbox **Amend** e botão com a quantidade de arquivos preparados ocupa toda a largura abaixo das duas colunas. Os painéis de árvores e diff têm a altura de uma janela inteira (100dvh), ajustada automaticamente ao redimensionar. A página mantém sua rolagem para acessar títulos, caminho, indicadores, botões de navegação e o formulário de commit abaixo dos painéis. As árvores e o diff também têm rolagem interna; em janelas estreitas, os painéis são empilhados, cada um com a altura da janela. Sem seleção no grupo, Stage/Unstage atua nos arquivos elegíveis desse grupo; com um arquivo selecionado, atua somente nele. Operações mutáveis ficam bloqueadas enquanto há outra operação em andamento nesta tela.

Stage adiciona apenas os caminhos revisados ao índice; Unstage retira do índice e preserva os arquivos locais, inclusive antes do primeiro commit. Os caminhos são literais, limitados e validados na raiz persistida; metadados `.git`, escapes, links/junctions, submódulos, conflitos e operações Git em andamento são rejeitados neste fluxo inicial. Stage captura a versão atual do arquivo quando acionado. O rascunho de mensagem/descrição é preservado após Stage/Unstage e após falhas ou cancelamento da revisão.

Commit abre uma revisão com mensagem, descrição e arquivos preparados. Confirmar cria o commit somente com o índice; alterações não preparadas não entram. Amend substitui o último commit e muda seu SHA, com aviso adicional quando há referência remota local conhecida que o contém. Amend sem arquivos novos no índice permite alterar apenas a mensagem. É necessário estar em uma branch ativa, com identidade Git configurada; hooks e assinatura configurados no computador são respeitados. Mensagem tem até 200 caracteres e descrição até 10 mil.

A seta do botão abre **Commit and Push**. Essa opção exige upstream configurado e um único destino de push, exibido na revisão; não cria configuração de upstream automaticamente. Após criar o commit, envia aquele SHA para a branch de destino explicitamente identificada, sem force, mirror, envio automático de tags ou push de submódulos. Não há fetch implícito. Credenciais continuam no Git Credential Manager/SSH já configurados. Se o envio falhar, for cancelado ou o destino mudar durante o commit, o commit local permanece: a resposta informa o SHA e o aviso de envio não confirmado. Atualize o estado e envie esse commit existente pela ferramenta Git; repetir um commit não é uma tentativa de push.

Endpoints POST `/api/RepositoryEdit/Prepare` e `/api/RepositoryEdit/Execute` seguem `CustomResponse`/Scalar e os três idiomas. Prepare valida o repositório e devolve um token do estado (HEAD, branch, status, índice, opções e destino); Execute revalida antes de escrever. Operações são serializadas por diretório Git comum, inclusive entre workspaces/worktrees, com um conjunto limitado de locks. A proteção é otimista em relação a ferramentas Git externas: evite modificar simultaneamente o índice por outro programa durante a execução. Falha ou cancelamento não desfaz comandos já concluídos; atualize e revise antes de repetir. Não há reset destrutivo, limpeza da árvore de trabalho ou rollback automático. Comandos mutáveis têm timeout de dois minutos e captura limitada; a interface não aplica o timeout HTTP curto às execuções.

Este incremento foi antecipado do fluxo individual da Milestone 4 por solicitação do usuário. Os testes novos foram preparados, incluindo push para um bare local temporário e falha de envio sem serviços externos; builds podem ser verificados pelo agente, mas testes/cobertura aguardam execução manual. Não há nova migration ou dependência. Pesquisa/histórico de arquivos/blame da Milestone 3 e staging por trechos continuam planejados.

### Histórico de commits

Na tela individual de um repositório, abra **Histórico**. A aba apresenta grafo e lista paginada de commits das referências locais, com linhas compactas e colunas para mensagem, autor com avatar, SHA e data/horário no idioma escolhido. Branches, tags e referências remotas aparecem em pills com ícones e nome curto; o nome completo fica disponível ao passar o mouse, e a branch atual recebe uma marca. O card do grafo fica acima dos detalhes, ambos com largura total. A altura máxima do grafo é 36vh no desktop e 22,5vh nas telas menores, metade da anterior, com rolagem interna. As linhas do grafo continuam entre páginas; pais ainda não carregados permanecem como linhas de continuação. Nenhum fetch é iniciado ao navegar. O histórico disponível em clones rasos segue os objetos presentes no computador.

Use **Branch** e **Buscar commit**, depois **Filtrar**, para consultar uma branch local específica e/ou mensagem (assunto e descrição) ou início do SHA, sem diferenciar caixa. A pesquisa é literal: `[` e `*` não são expressões regulares. As opções de branch são consultadas ao focar o seletor. **Todos os commits** restaura o escopo geral. O backend pesquisa além das páginas carregadas e pagina os resultados; o token vincula a continuação às referências e aos filtros. Mudar filtros reinicia a página e cancela detalhes/diffs anteriores. Filtros explícitos permanecem durante a sessão; clicar numa branch local/remota apenas muda a seleção, preservando inclusive a busca. Se a ponta não estiver carregada, a navegação tenta carregar até 200 páginas adicionais; detalhes continuam disponíveis se o filtro a excluir ou ela estiver fora desse intervalo. Nenhum desses controles faz checkout. A pesquisa textual examina no máximo 10 mil commits por consulta; exceder o limite retorna erro para refinar o escopo sem apresentar resultados parciais como completos. O grafo filtrado conserva somente commits e conexões conhecidas, sem inventar ligações. Os parâmetros opcionais `branch` e `search` de `GetHistory` preservam compatibilidade com as requisições anteriores.

As referências aparecem em uma linha abaixo da mensagem, somente quando presentes. Mensagens ou conjuntos de pills maiores que a coluna oferecem setas para rolar lateralmente, com movimento suave e respeito à preferência por movimento reduzido. As setas não selecionam o commit. Cada linha do grafo usa uma cor dos tokens globais da paleta Cosmic, com cores preservadas ao recolher/expandir; os nós são círculos sem escala desigual. Merges possuem círculos maiores com seta para baixo (expandido) ou direita (recolhido).

Clique no círculo de um merge para ocultar ou mostrar os commits alcançáveis pelos pais secundários que não pertencem à ancestralidade do primeiro pai. A ação altera somente a visualização das páginas já carregadas; carregar mais mantém os recolhimentos e aplica-os aos novos commits. Merges aninhados preservam seu estado até atualizar o histórico ou trocar de contexto. Se o commit selecionado for ocultado, os detalhes passam para o merge. Nenhuma operação de merge Git é executada por esses controles.

As fotos usam [Gravatar](https://docs.gravatar.com/sdk/images/) pelo SHA-256 do e-mail normalizado do autor, com carregamento lazy via HTTPS e sem enviar referrer. Quando não há imagem disponível, e-mail ou conexão, aparecem iniciais locais. A consulta de avatares depende desse serviço externo; a navegação e os dados Git continuam locais.

Selecione um commit para consultar sua mensagem completa, autor/e-mail, pais e arquivos alterados. Clique no nome de um arquivo para visualizar seu diff unificado em um acordeão imediatamente abaixo dele; clicar novamente fecha. Somente um arquivo pode ficar expandido por vez. Fechar ou trocar de arquivo cancela consultas pendentes e descarta respostas atrasadas; o botão de retry mantém o acordeão aberto. A comparação usa o primeiro pai do commit, inclusive em merges; commits iniciais são comparados com a árvore vazia. Renomeações preservam o caminho original; binários e diferenças acima do limite recebem aviso. Conteúdo de commits é tratado como texto, sem executar HTML, filtros externos ou leitura de arquivos da árvore de trabalho.

O visualizador mostra somente as linhas de código do diff, com suas cores e numeração: cabeçalhos Git (`diff --git`, `index`, `---`, `+++`) e cabeçalhos de blocos (`@@`) ficam ocultos. Linhas de código que começam com esses caracteres são preservadas. Mudanças somente de metadados exibem a indicação de ausência de diferenças textuais. Os indicadores de arquivo são compartilhados entre histórico e alterações locais: `M` amarelo para modificado, `+` verde para adicionado e `−` vermelho para removido, com descrição traduzida ao passar o mouse.

Endpoints de consulta, com `CustomResponse`, validação e documentação no Scalar:

- `GET /api/RepositoryHistory/GetHistory?repositoryId=1&workspaceId=0&page=0&pageSize=50`.
- `GET /api/RepositoryHistory/GetCommit?repositoryId=1&workspaceId=0&sha=<SHA completo>`.
- `GET /api/RepositoryHistory/GetDiff?repositoryId=1&workspaceId=0&sha=<SHA completo>&path=<caminho codificado>`.

`workspaceId=0` consulta o catálogo global; um workspace informado deve estar ativo e conter o vínculo. A primeira página devolve `snapshot` e `hasMore`; páginas seguintes devem enviar o mesmo `snapshot`. Mudanças nas referências ou no HEAD invalidam a paginação com erro localizado 409: utilize **Atualizar** para reiniciar. Páginas têm até 100 commits, a interface solicita 50. Metadados e listas são limitados a 2 milhões de caracteres por comando, e os diffs a 200 mil caracteres/4 mil linhas. Saídas truncadas não são apresentadas como completas. Não há nova migration ou dependência.

### Identidade e paletas

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

O frontend usa o pool `threads` do Vitest com um único worker, sem paralelismo entre arquivos e com isolamento por arquivo. Essa configuração evita depender da inicialização de um processo Node separado para cada arquivo no Windows. Se um worker não iniciar, a execução está incompleta: os percentuais de cobertura dessa rodada não representam a suíte inteira.

## Organização

- `src/backend`: API, Application, Domain e Infrastructure.
- `src/frontend/src`: app, core, features, layout e shared.
- `tests`: testes unitários backend.
- `scripts`: build e validação.

SQLite e EF Core possuem o modelo inicial de Workspace, GitRepository e WorkspaceRepository. A API aplica migrations pendentes automaticamente antes de aceitar requisições, sem recriar o banco. A conexão padrão usa `Data Source=gitcosmos.db`, relativa ao diretório de trabalho do processo. Para fixar o local, configure `ConnectionStrings__GitCosmos` com caminho absoluto. O relacionamento permite compartilhar repositórios entre workspaces e impede vínculos repetidos; esses vínculos já podem ser gerenciados pela interface e pela API. O MVP usa Git CLI, respeita a autenticação já configurada no computador e não requer Azure ou Docker.

Licença MIT. Consulte [LICENSE](LICENSE).
