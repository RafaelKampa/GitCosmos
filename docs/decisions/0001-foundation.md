# ADR 0001 — Fundação técnica

Data: 20/11/2026. Estado: implementado para a base inicial.

O backend usa quatro projetos .NET 10 com dependências dirigidas ao domínio e os padrões de services, use cases, controllers e respostas do Blux LATAM Auth. O código da referência foi adaptado para o namespace GitCosmos. SQLite substitui PostgreSQL, e o atributo de serialização de `TotalRows` usa System.Text.Json, o serializer efetivamente utilizado pela API.

O SDK foi fixado em 10.0.300 (instalado no ambiente) com roll-forward apenas de patch. Node 24.21.0 foi registrado em `.nvmrc`. Dependências npm ficam reproduzíveis pelo lockfile.

O backend escuta em `127.0.0.1:5210`. Durante desenvolvimento, Vite em `127.0.0.1:5173` encaminha `/api` para o backend. O frontend compilado pode ser servido pela própria API. O middleware de origem exige a mesma autoridade HTTP para requisições de navegador. Ainda não há endpoints que modifiquem repositórios; proteção adicional de operações sensíveis deverá ser definida antes deles.

O fluxo inicial é GET `/api/System/GetStatus`: controller → ISystemService → SystemService → GetSystemStatusUseCase. Retorna nome, versão, idioma efetivo e mensagem localizada. Não há acesso a repositório para uma consulta sem persistência. Contratos de repositório e migrations serão criados junto ao modelo de workspaces na Milestone 2, evitando interfaces sem implementação ou propósito.

O DbContext e o provedor SQLite estão configurados, sem entidades ou migrations nesta base. Não é necessário criar um arquivo de banco para consultar o estado da aplicação. SignalR está registrado no container; hubs e eventos de operações serão implementados com os fluxos de execução.

pt-BR, es e en são fornecidos por catálogos no backend e frontend. O cliente HTTP envia Accept-Language e mantém o texto de errors[] devolvido pelo backend; falhas de rede, timeout e cancelamento possuem fallback local traduzido. O idioma da interface é persistido localmente. A normalização segue a regra documentada de usar o primeiro idioma, com fallback pt-BR.

A cobertura inclui controllers, middlewares, composição de serviços, componentes, bootstrap e cliente HTTP. Os tipos TypeScript em `*.model.ts` não possuem código executável; os testes são excluídos da medição do produto. A única exclusão técnica C# é `**/obj/**/*.generated.cs`: suporte XML produzido pelo source generator do OpenAPI, que não é código mantido pelo projeto. Program.cs e todo código executável escrito para a aplicação permanecem incluídos. Os limiares são verificados localmente e na CI; o frontend também os exige por arquivo.

Atualizações explícitas de OpenAPI.NET e do bundle nativo SQLite evitam os avisos transitivos identificados na primeira restauração. Overrides npm de glob/minimatch corrigem dependências das ferramentas de testes; o build e os testes devem ser revalidados após alterações no lockfile.
