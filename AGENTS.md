# GitCosmos

Leia [.agents/GITCOSMOS_PROJECT_CONTEXT.md](.agents/GITCOSMOS_PROJECT_CONTEXT.md) antes de implementar. É a referência das decisões do projeto, armazenada na pasta `.agents`.

## Skill do projeto

Para desenvolver, revisar ou documentar o GitCosmos, utilizar a skill [gitcosmos-development](.agents/skills/gitcosmos-development/SKILL.md). Ela consolida as decisões do projeto e o procedimento de entrega. Consultar seu guia de referência conforme o escopo da tarefa.

- Backend .NET 10 com Clean Architecture e o padrão Auth: Controller → IService → Service → UseCase → IRepository → Repository → EF Core. Git CLI através de contratos de Application.
- Preservar o modelo de controllers, `CustomResponse`, exceptions e middlewares descrito no contexto.
- Frontend Vue 3 componentizado: `core`, `features`, `layout`, `shared`. Chamadas HTTP apenas nos serviços de domínio.
- Toda alteração visual deve consultar e respeitar `.agents/GitCosmos-Cosmic-Light-Design-System.md` e `.agents/GitCosmos-Cosmic-Dark-Design-System.md`. Cores centralizadas em `shared/styles/tokens.css`; Cosmic Dark é o padrão, preservando tema e idioma escolhidos no navegador.
- pt-BR, es e en obrigatórios, incluindo erros e validações. Nova chave em todos os catálogos.
- Testes unitários com no mínimo 95% de linhas e branches em backend e frontend. Não excluir código de aplicação para atingir cobertura.
- Executar builds e testes com concorrência limitada. Backend: `-m:1`; Vitest: um worker.
- Por preferência explícita do usuário, não executar testes automaticamente. Sempre que a alteração exigir testes, criar/atualizar os testes e fornecer os comandos PowerShell completos, incluindo `Set-Location` com o caminho absoluto correto, para execução manual pelo usuário. Builds podem ser executados; não afirmar que testes pendentes passaram. A CI mantém suas verificações automáticas.
- API apenas em loopback, credenciais Git fora do banco. Não executar operações Git destrutivas como recuperação automática.
- Não realizar commits ou pushes sem pedido explícito.
- Ao final de cada entrega, fornecer os comandos PowerShell de testes manuais aplicáveis e de execução do projeto, todos com `Set-Location` e caminho absoluto. Em alterações documentais, indicar que testes são opcionais. Sempre sugerir uma mensagem de commit em inglês, sem executar o commit.
- Documentar mudanças no contexto e em README/ADRs quando aplicável.
