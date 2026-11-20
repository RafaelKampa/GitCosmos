# GitCosmos — Guia de Identidade Visual e Tema Cosmic Light

**Status:** proposta de tema claro aprovada como direção visual inicial.  
**Projeto:** [GitCosmos](https://github.com/RafaelKampa/GitCosmos)  
**Data:** 20/11/2026  
**Objetivo:** fornecer especificações para implementar o tema claro do frontend Vue 3 + TypeScript + PrimeVue, em conjunto com o tema Cosmic Dark, sem pressupor que o layout final já esteja definido.

## 1. Visão geral

O tema **Cosmic Light** é a contraparte clara do **Cosmic Dark**. Deve preservar a identidade espacial da marca GitCosmos, com fundos brancos frios e cinzas levemente azulados, enquanto azul, violeta e laranja aparecem como acentos funcionais. A interface deve ser limpa, profissional, confortável e adequada a uma ferramenta de desenvolvimento utilizada por períodos prolongados.

**Princípio central:** a maior parte da interface deve utilizar superfícies claras e discretas; as cores da logo destacam ações, seleção e identidade, sem competir com os dados dos repositórios.

### Identidade da marca

- **Logo escolhida:** símbolo de galáxia com três planetas conectados; palavra **Git** em laranja `#EA580C`; palavra **Cosmos** em degradê azul-violeta; órbita decorativa sob o nome.
- **Cor de marca principal:** `#EA580C` — preservar no logotipo em ambos os temas.
- **Ação principal no tema claro:** azul `#2563EB`.
- **Acento de seleção:** violeta `#7C3AED`.
- **Acento auxiliar:** ciano `#0891B2`.
- **Não alterar a logo para adequá-la ao tema:** adaptar a interface à identidade já aprovada.

## 2. Paleta base

| Token sugerido | Hex | Aplicação |
|---|---|---|
| `--gc-bg` | `#F8FAFF` | Fundo principal da aplicação |
| `--gc-sidebar` | `#EEF2FF` | Sidebar e navegação lateral |
| `--gc-surface` | `#FFFFFF` | Cards, tabelas e painéis |
| `--gc-surface-raised` | `#F1F5F9` | Superfícies secundárias, menus e áreas elevadas quando apropriado |
| `--gc-border` | `#CBD5E1` | Bordas de controles e painéis |
| `--gc-divider` | `#E2E8F0` | Divisores discretos |
| `--gc-text` | `#0F172A` | Texto principal |
| `--gc-text-secondary` | `#475569` | Texto secundário e metadados |
| `--gc-text-muted` | `#64748B` | Texto discreto, dicas e detalhes menos importantes |
| `--gc-primary` | `#2563EB` | Ação principal e navegação |
| `--gc-brand` | `#EA580C` | Logotipo e acentos especiais da marca |
| `--gc-violet` | `#7C3AED` | Seleção, foco e detalhes especiais |
| `--gc-cyan` | `#0891B2` | Informações auxiliares e detalhes técnicos |

### Uso e hierarquia

- Fundo principal `#F8FAFF` ocupa a maior parte da tela, com um leve tom azulado.
- Sidebar `#EEF2FF` diferencia a navegação sem criar contraste excessivo.
- Cards e linhas de repositórios utilizam `#FFFFFF`, separados por bordas `#E2E8F0`.
- Áreas secundárias e estados de hover podem utilizar `#F1F5F9`, conforme contraste e hierarquia.
- Bordas de controles utilizam `#CBD5E1`; divisores discretos, `#E2E8F0`.
- Priorizar texto `#0F172A`, secundário `#475569` e discreto `#64748B`.
- Reservar o laranja para a marca e destaques pontuais; **não pintar todos os botões de laranja**.

## 3. Cores semânticas de operações Git

| Estado | Texto/ícone | Fundo de badge | Exemplos | Ícone sugerido |
|---|---|---|---|---|
| Sucesso / limpo | `#15803D` | `#DCFCE7` | operação concluída, working tree clean | `check-circle` |
| Atenção / alterações | `#A16207` | `#FEF3C7` | arquivos modificados, validação que exige atenção | `alert-triangle` |
| Erro / falha | `#DC2626` | `#FEE2E2` | operação falhou, repositório inacessível | `x-circle` |
| Informação / behind | `#1D4ED8` | `#DBEAFE` | commits atrás do remoto, informações de sincronização | `info` |
| Em andamento | `#7C3AED` | `#EDE9FE` | fetch/pull/push em execução | `loader-circle` |
| Ignorado / inativo | `#475569` | `#F1F5F9` | repositório pulado, operação não iniciada | `minus-circle` |

**Regra de acessibilidade:** não transmitir estado exclusivamente por cor. Usar sempre texto e/ou ícone, por exemplo, “Modified”, “Behind 2”, “Failed”, “Clean”. Não confundir `behind` com erro nem tratar `modified` como falha. Para estados cancelado e ignorado, manter rótulos distintos mesmo quando compartilharem tons neutros.

## 4. Distribuição visual

Proporção aproximada para composição geral:

- **60%:** fundos claros e áreas de respiro.
- **25%:** cards, painéis e superfícies.
- **15%:** acentos, controles, indicadores e elementos de destaque.

Essa proporção é uma **diretriz visual**, não uma regra matemática. O azul deve ser a cor de ação mais comum; violeta é pontual; laranja permanece restrito à identidade e a poucas chamadas especiais.

## 5. Aplicação por componente

### 5.1. Estrutura da aplicação

- **Página:** `#F8FAFF`.
- **Sidebar:** `#EEF2FF`, com itens inativos em `#475569` e item ativo destacado por azul/violeta e fundo sutil.
- **Cabeçalho:** branco ou lavanda muito clara, com borda inferior discreta; evitar gradientes extensos.
- **Conteúdo:** cards brancos, espaçamento confortável e separação por bordas suaves em vez de sombras pesadas.

### 5.2. Botões

- **Primário:** fundo `#2563EB`; texto branco; ações comuns de maior prioridade.
- **Secundário:** fundo `#FFFFFF` ou `#F1F5F9`; texto `#0F172A`; borda `#CBD5E1`.
- **Destaque de marca:** `#EA580C`, texto branco; uso restrito e sujeito a validação de contraste em cada estado.
- **Destrutivo:** vermelho semântico `#DC2626`, com texto/ícone e confirmação para operações perigosas.
- **Desabilitado:** aparência esmaecida, sem depender apenas da cor para comunicar indisponibilidade.
- **Hover/pressed:** tons consistentes, sem brilho neon ou mudança abrupta de saturação.

**Exemplo:** Fetch All pode utilizar o botão azul principal; o laranja não precisa ser usado em toda operação Git.

### 5.3. Cards e linhas de repositórios

- Fundo `#FFFFFF`, com borda `#E2E8F0`.
- Nome do repositório em `#0F172A`.
- Branch e caminho em `#475569` ou `#64748B`, conforme relevância.
- Ícone de repositório pode usar violeta `#7C3AED`.
- Estado Git usa badge suave + ícone + texto.
- Hover pode usar `#F1F5F9`; seleção deve ter indicação adicional visível (borda, fundo e/ou ícone).
- Não colorir todo o card de verde, amarelo ou vermelho para representar estado.

### 5.4. Tabelas

- Cabeçalho branco ou `#F1F5F9` com texto `#0F172A`.
- Divisores `#E2E8F0` e bordas de controle `#CBD5E1`.
- Hover discreto; seleção claramente diferenciada.
- Status em badges de fundo suave, com texto e ícone de contraste adequado.
- Manter legibilidade para nomes de branches, SHAs, caminhos e mensagens extensas.

### 5.5. Formulários

- Inputs brancos, com borda `#CBD5E1`.
- Texto `#0F172A`; placeholder `#64748B` quando apropriado.
- Foco visível em azul `#2563EB` ou violeta `#7C3AED`, com contorno de espessura perceptível.
- Erros em `#DC2626` com mensagem textual; não apenas borda vermelha.
- Labels sempre legíveis e associados aos respectivos controles.

### 5.6. Modais, menus e notificações

- Modais e menus podem utilizar `#FFFFFF`, com borda `#E2E8F0` e sombra suave; `#F1F5F9` pode diferenciar áreas internas.
- Overlay suficientemente perceptível para separar o diálogo do restante da tela.
- Toasts e alertas utilizam cores semânticas, ícones e mensagens explícitas.
- Logs e detalhes de erro devem ser legíveis, selecionáveis e copiáveis.

### 5.7. Indicadores de progresso

- Execução: violeta `#7C3AED`.
- Conclusão: verde `#15803D`.
- Falha: vermelho `#DC2626`.
- Ignorado/cancelado: cinza `#475569`, com texto que diferencie as condições.
- Mostrar progresso por repositório e resumo geral, sem sugerir que uma operação coletiva é atômica.

## 6. Tipografia e legibilidade

**Títulos H1:** usar a cor de marca #EA580C em ambos os temas, por meio do token global --gc-heading-h1: var(--gc-brand). Aplicar a regra ao seletor h1; não alterar a cor dos demais níveis de título por consequência desta decisão.

- Preferir fontes sans-serif legíveis para a UI; a fonte definitiva ainda não foi escolhida.
- Para caminhos, branches, SHAs, comandos e logs, usar fonte monoespaçada.
- Texto principal `#0F172A`, secundário `#475569` e discreto `#64748B`.
- Evitar textos longos em laranja, violeta ou ciano.
- Não utilizar cores muito claras para textos essenciais; o contraste deve ser validado sobre a superfície real.

## 7. Acessibilidade

- Verificar contraste conforme WCAG 2.2 AA: em geral **4,5:1 para texto normal** e **3:1 para texto grande e componentes gráficos essenciais**, conforme critérios aplicáveis.
- O laranja `#EA580C` é parte da identidade e deve permanecer na logo; em textos pequenos, validar contraste sobre o fundo real.
- Botões com fundo azul `#2563EB` e texto branco devem ser testados nos estados normal, hover, pressed e disabled.
- Oferecer indicador de foco visível e navegação por teclado.
- Não comunicar estados apenas por cor; utilizar ícone e rótulo.
- Considerar pessoas com daltonismo, ambientes de muito brilho e monitores com diferentes calibrações.
- Testar os dois temas em condições reais de uso.

## 8. Gradientes e efeitos especiais

- **Degradê azul-violeta:** adequado para a palavra Cosmos, detalhes de identidade e pequenas ilustrações.
- **Glow espacial:** reservado a imagens e momentos especiais; não aplicar brilho constante a botões, bordas ou cards.
- **Planetas, estrelas e órbitas:** podem aparecer em onboarding e empty states, mas não devem prejudicar a leitura do dashboard.
- **Sombras:** leves e discretas, com preferência por bordas para definir superfícies.
- **Animações:** sutis e opcionais, respeitando preferências de redução de movimento.

## 9. Tokens CSS iniciais

```css
:root[data-theme='cosmic-light'] {
  color-scheme: light;

  --gc-bg: #F8FAFF;
  --gc-sidebar: #EEF2FF;
  --gc-surface: #FFFFFF;
  --gc-surface-raised: #F1F5F9;
  --gc-border: #CBD5E1;
  --gc-divider: #E2E8F0;

  --gc-text: #0F172A;
  --gc-text-secondary: #475569;
  --gc-text-muted: #64748B;

  --gc-primary: #2563EB;
  --gc-brand: #EA580C;
  --gc-heading-h1: var(--gc-brand);
  --gc-violet: #7C3AED;
  --gc-cyan: #0891B2;

  --gc-success: #15803D;
  --gc-success-bg: #DCFCE7;
  --gc-warning: #A16207;
  --gc-warning-bg: #FEF3C7;
  --gc-danger: #DC2626;
  --gc-danger-bg: #FEE2E2;
  --gc-info: #1D4ED8;
  --gc-info-bg: #DBEAFE;
  --gc-running: #7C3AED;
  --gc-running-bg: #EDE9FE;
  --gc-inactive: #475569;
  --gc-inactive-bg: #F1F5F9;
}
```

**Nota de implementação:** estes tokens são a referência semântica do GitCosmos. Integrá-los à API de temas/presets da versão de **PrimeVue** instalada, evitando sobrescritas CSS globais frágeis. Não presumir nomes internos de tokens PrimeVue sem verificar a versão.

### Alternância entre temas

- Oferecer opções **Claro**, **Escuro** e **Sistema**.
- **Claro:** aplica `cosmic-light`.
- **Escuro:** aplica `cosmic-dark`.
- **Sistema:** acompanha a preferência de cores do sistema operacional.
- Persistir a preferência de modo consistente com a arquitetura local do GitCosmos.
- Usar os **mesmos nomes de tokens semânticos** em ambos os temas sempre que possível, alterando seus valores; mapear aliases adicionais conforme necessário.
- Evitar que componentes dependam diretamente de hexadecimais espalhados pelo código.

## 10. Exemplo conceitual de dashboard

Um dashboard de workspace poderá apresentar:

- Cabeçalho GitCosmos com o logotipo aprovado.
- Sidebar lavanda muito clara com seleção de workspace e navegação.
- Título “My Workspace” e contagem de repositórios.
- Ações Fetch All, Pull All, Push All e Checkout All, com prioridade visual definida pelo contexto.
- Lista de repositórios em cards brancos:
  - `authentication-api` — `develop` — **Clean** (verde).
  - `orders-api` — `feature/orders` — **Modified** (amarelo).
  - `products-api` — `develop` — **Clean** (verde).
  - `admin-frontend` — `main` — **Behind 2** (azul).
- Barra de status com resumo das operações e informações relevantes.

**Atenção:** esses nomes e estados são fictícios e servem apenas para demonstrar a paleta. O layout e a experiência final ainda precisam ser projetados.

## 11. Recomendações de implementação no GitCosmos

1. Reutilizar a estrutura de tokens semânticos definida para o Cosmic Dark.
2. Configurar um preset PrimeVue compatível com a versão instalada e com alternância entre claro/escuro.
3. Implementar o **Cosmic Light** como opção de aparência, sem substituir o Cosmic Dark.
4. Criar componentes de status Git reutilizáveis, com rótulo, ícone e cor semântica.
5. Aplicar cores por **função**, evitando hexadecimais dispersos nos componentes.
6. Criar estados hover, active, focus, disabled, loading e error para controles.
7. Validar contraste, foco e navegação por teclado.
8. Testar dashboard, modais, formulários, tabelas, notificações e estados vazios.
9. Preservar a logo original com **Git `#EA580C`** e **Cosmos azul-violeta**.
10. Registrar ajustes de cores aprovados na documentação e nos tokens.

## 12. O que já foi decidido e o que continua aberto

### Direção visual aprovada

- Nome do tema: **Cosmic Light**.
- Base branca fria `#F8FAFF`.
- Sidebar lavanda muito clara `#EEF2FF`.
- Cards brancos `#FFFFFF` e superfícies secundárias `#F1F5F9`.
- Azul `#2563EB` como cor principal de ação.
- Laranja `#EA580C` preservado na palavra “Git” da logo e como acento de marca.
- Violeta `#7C3AED` para seleção/detalhes especiais.
- Cores semânticas escuras e fundos suaves para estados Git.
- Coexistência com o tema **Cosmic Dark** e possibilidade de seguir o tema do sistema.
- Estética profissional, clara e discreta, sem excesso de brilho.

### Ainda não definido

- Fonte oficial.
- Layout definitivo.
- Tamanhos, espaçamentos e raios oficiais.
- Preset PrimeVue final e mecanismo exato de persistência da preferência.
- Regras exatas de hover/pressed e variantes de botões.
- Componentes e ícones definitivos.

## 13. Instrução para outro chat de desenvolvimento

> Utilize este documento como referência para implementar o tema claro **Cosmic Light** no frontend Vue 3 + TypeScript + PrimeVue do GitCosmos, **em conjunto com o Cosmic Dark já documentado**. Reutilize tokens semânticos e componentes de status Git, preservando o logotipo aprovado: “Git” em `#EA580C`, “Cosmos” em degradê azul-violeta e ícone da galáxia com três planetas conectados. Ofereça opções Claro, Escuro e Sistema; priorize legibilidade, acessibilidade e manutenção. Não considere o exemplo de dashboard um layout definitivo; verifique primeiro a estrutura existente do projeto e a versão instalada do PrimeVue.
