# ADR 0002 — Marca e temas do frontend

Data: 20/11/2026. Estado: implementado; validação unitária manual pendente.

Utilizar os arquivos de marca fornecidos pelo usuário. O cabeçalho usa a logo PNG de `shared/assets/branding`, sem fundo, moldura ou preenchimento aplicado por CSS. Favicon e ícone para dispositivos Apple usam o PNG transparente de `public`. Os SVGs originais são preservados, mas não selecionados pelo HTML. Esta decisão substitui a preferência inicial por SVG e a base clara, conforme correção solicitada pelo usuário após observar o tema escuro.

Centralizar as cores customizadas em `shared/styles/tokens.css`, respeitando os documentos Cosmic Light e Cosmic Dark em `.agents` para toda alteração visual. Tokens `--gc-*` reproduzem as paletas base e semânticas aprovadas, e aliases `--color-*` integram a interface existente e o PrimeVue. Cosmic Dark é a base de `:root`; `:root[data-theme='cosmic-light']` aplica o claro. Hover e escalas auxiliares derivam dos tokens documentados. Componentes consomem variáveis, sem cores locais; a arte original da marca é preservada.

O preset de PrimeVue em `core/theme/primevue-preset.ts` usa as mesmas variáveis para os tokens semânticos. A classe `.dark` na raiz ativa o modo escuro da biblioteca, enquanto `data-theme` seleciona os tokens da aplicação. Alterar a paleta não exige reescrever os componentes.

`core/theme/theme.store.ts` mantém a escolha claro/escuro no Pinia e no localStorage (`gitcosmos.theme`). O bootstrap aplica a preferência antes de montar o Vue, com `data-theme` cosmic-dark/cosmic-light e classe `.dark` para o PrimeVue. Conforme orientação atual do usuário, sem preferência válida usar Cosmic Dark. Preferências anteriores light/dark permanecem compatíveis e o idioma também continua salvo no navegador. A alteração atual adota as paletas dos documentos e mantém as duas opções existentes; a opção Sistema descrita no guia claro permanece uma evolução, sem alterar o padrão escuro solicitado.

O botão primário usa azul `--gc-primary` em cada tema. No escuro, o texto do botão usa o azul-marinho profundo da paleta para melhorar o contraste com o azul principal; no claro, usa branco. Violeta indica foco/seleção, e laranja permanece reservado à marca. Separar painéis por bordas discretas, sem sombra pesada ou brilho constante.

O seletor fica em `shared/components/theme-switcher`, com estado acessível `aria-pressed`, foco visível e textos em pt-BR, es e en. Os testes preparados cobrem valores ausentes/inválidos, restauração de preferência, alternância nos dois sentidos, persistência, rótulos nos três idiomas, inicialização e integração das logos. Conforme preferência do usuário, não executar testes automaticamente; fornecer comandos PowerShell com Set-Location para validação manual.
