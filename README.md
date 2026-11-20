# GitCosmos

Um cliente Git local para gerenciar repositórios independentes em um workspace unificado.

O projeto está na fundação técnica. O MVP completo e as decisões estão em [.agents/GITCOSMOS_PROJECT_CONTEXT.md](.agents/GITCOSMOS_PROJECT_CONTEXT.md).

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

O Vite encaminha as chamadas `/api` recebidas na porta `5173` para o backend na porta `5210`. Durante a edição do frontend, acesse a porta `5173` para acompanhar as alterações.

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

A interface permite selecionar português, espanhol ou inglês e consultar o estado do backend. Ela apresenta mensagens localizadas, carregamento e erros de conexão. As funções Git e o CRUD de workspaces ainda não estão implementados.

## Marca e temas

A logo PNG fornecida é exibida no cabeçalho a partir de `src/frontend/src/shared/assets/branding`, preservando sua transparência e sem fundo aplicado por CSS. O favicon e o ícone para dispositivos Apple usam o PNG transparente de `src/frontend/public`. As versões SVG permanecem disponíveis nos assets originais, sem uso na interface atual.

O botão de tema no cabeçalho alterna entre **Cosmic Dark** e **Cosmic Light**, preservando a escolha no navegador (`gitcosmos.theme`). Cosmic Dark é o padrão para a primeira visita e para preferências inválidas. Uma escolha clara já salva é respeitada; trocar o padrão não apaga a preferência do usuário. O idioma também é persistido localmente (`gitcosmos.language`). A logo é apresentada diretamente sobre o fundo do cabeçalho nos dois temas.

Toda alteração visual deve respeitar as paletas de [.agents/GitCosmos-Cosmic-Light-Design-System.md](.agents/GitCosmos-Cosmic-Light-Design-System.md) e [.agents/GitCosmos-Cosmic-Dark-Design-System.md](.agents/GitCosmos-Cosmic-Dark-Design-System.md).

Todas as cores customizadas ficam em `src/frontend/src/shared/styles/tokens.css`: os tokens semânticos `--gc-*` reproduzem as paletas aprovadas para fundo, superfícies, texto, bordas, ações e estados Git. Os aliases `--color-*` conectam esses tokens à interface e ao preset PrimeVue. Cosmic Dark é a base de `:root`; `:root[data-theme='cosmic-light']` aplica o tema claro. Hover e escalas auxiliares são derivados dessas cores, sem valores fixos nos componentes. A classe `.dark` sincroniza o modo do PrimeVue. Alterações na paleta exigem atualização dos documentos de referência e dos tokens.

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

Os testes unitários devem atingir no mínimo 95% de linhas e branches em backend e frontend. O script falha se os testes ou a cobertura não passarem. Relatórios: `artifacts/coverage/backend` e `src/frontend/coverage`. A CI usa os mesmos scripts e publica os relatórios.

Por preferência do responsável, agentes devem fornecer esses comandos para execução manual, sem iniciar testes automaticamente. A CI continua validando builds e cobertura nos pushes e pull requests.

## Organização

- `src/backend`: API, Application, Domain e Infrastructure.
- `src/frontend/src`: app, core, features, layout e shared.
- `tests`: testes unitários backend.
- `docs/decisions`: decisões arquiteturais.
- `scripts`: build e validação.

SQLite e EF Core estão configurados; entidades e migrations serão adicionadas com os workspaces. O MVP usa Git CLI, respeita a autenticação já configurada no computador e não requer Azure ou Docker.

Licença MIT. Consulte [LICENSE](LICENSE).
