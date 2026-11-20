# GitCosmos — Guia de Identidade Visual e Tema Cosmic Dark

**Status:** proposta de tema escuro aprovada como direção visual inicial.  
**Projeto:** [GitCosmos](https://github.com/RafaelKampa/GitCosmos)  
**Data:** 20/11/2026  
**Objetivo:** fornecer especificações para implementar o tema escuro do frontend Vue 3 + TypeScript + PrimeVue, sem pressupor que o layout final já esteja definido.

## 1. Visão geral

O tema **Cosmic Dark** deve refletir a identidade espacial da marca GitCosmos: galáxia, órbitas e repositórios independentes conectados. O resultado desejado é uma interface de ferramenta profissional para desenvolvedores: escura, discreta, legível e confortável para uso prolongado.

**Princípio central:** os fundos e painéis devem ser neutros e predominantemente azul-marinho. As cores da logo (laranja, azul e violeta) devem atuar como acentos, não competir com o conteúdo.

### Identidade da marca

- **Logo escolhida:** símbolo de galáxia com três planetas conectados; palavra **Git** em laranja `#EA580C`; palavra **Cosmos** em degradê azul-violeta; órbita decorativa sob o nome.
- **Cor de marca principal:** `#EA580C` — preservar no logotipo.
- **Cores complementares:** azul `#3B82F6` e violeta `#8B5CF6`.
- **Acento auxiliar:** ciano `#22D3EE`.
- **Não alterar o arquivo da logo para adequá-lo à interface:** adaptar os componentes à marca, não o contrário.

## 2. Paleta base

| Token sugerido | Hex | Aplicação |
|---|---|---|
| `--gc-bg` | `#090D1B` | Fundo principal da aplicação |
| `--gc-sidebar` | `#10172A` | Sidebar, navegação lateral |
| `--gc-surface` | `#172036` | Cards, tabelas, painéis |
| `--gc-surface-raised` | `#202B45` | Menus, modais, dropdowns, painéis elevados |
| `--gc-border` | `#334155` | Bordas, divisores, separadores |
| `--gc-text` | `#F1F5F9` | Texto principal |
| `--gc-text-muted` | `#94A3B8` | Texto secundário, metadados |
| `--gc-text-disabled` | `#64748B` | Texto desabilitado (não usar para informação essencial) |
| `--gc-primary` | `#3B82F6` | Ação principal e navegação |
| `--gc-brand` | `#EA580C` | Logotipo e acentos especiais da marca |
| `--gc-brand-on-dark` | `#FB923C` | Texto laranja pequeno sobre fundo escuro, quando necessário |
| `--gc-violet` | `#8B5CF6` | Seleção, foco e detalhes especiais |
| `--gc-cyan` | `#22D3EE` | Informação auxiliar, detalhes técnicos |

### Uso e hierarquia

- Fundo principal `#090D1B` ocupa a maior parte da tela.
- Sidebar `#10172A` separa a navegação do conteúdo.
- Cards e linhas de repositórios utilizam `#172036`.
- Componentes flutuantes podem usar `#202B45`.
- Usar `#334155` para contornos discretos, sem desenhar caixas em torno de absolutamente tudo.
- Priorizar texto `#F1F5F9` e metadados `#94A3B8`.
- Reservar o laranja para a marca, chamadas pontuais e ações de destaque; **não pintar todos os botões de laranja**.

## 3. Cores semânticas de operações Git

| Estado | Hex | Exemplos | Ícone sugerido |
|---|---|---|---|
| Sucesso / limpo | `#34D399` | operação concluída, working tree clean | `check-circle` |
| Atenção / alterações | `#FBBF24` | arquivos modificados, validação que exige atenção | `alert-triangle` |
| Erro / falha | `#F87171` | operação falhou, repositório inacessível | `x-circle` |
| Informação / behind | `#60A5FA` | commits atrás do remoto, informações de sincronização | `info` |
| Em andamento | `#A78BFA` | fetch/pull/push em execução | `loader-circle` |
| Ignorado / inativo | `#94A3B8` | repositório pulado, operação não iniciada | `minus-circle` |

**Regra de acessibilidade:** não transmitir estado exclusivamente por cor. Usar sempre texto e/ou ícone. Por exemplo, “Modified”, “Behind 2”, “Failed”, “Clean”. Não confundir `behind` (informação) com erro e não tratar `modified` como falha.

## 4. Distribuição visual

Proporção aproximada para composição geral:

- **60%:** fundos profundos.
- **25%:** painéis e superfícies.
- **15%:** acentos, controles, indicadores e elementos de destaque.

A proporção é uma **diretriz visual**, não uma regra de implementação matemática. Dentro dos acentos, azul deve ser o mais comum, violeta pontual e laranja mais restrito.

## 5. Aplicação por componente

### 5.1. Estrutura da aplicação

- **Página:** `#090D1B`.
- **Sidebar:** `#10172A`, com itens inativos em `#94A3B8` e ativo destacado por azul/violeta e fundo sutil.
- **Cabeçalho:** mesmo universo de cores da sidebar, sem gradientes grandes ou efeitos de brilho constantes.
- **Conteúdo:** superfícies `#172036` e espaços suficientes para leitura.

### 5.2. Botões

- **Primário:** fundo `#3B82F6`; texto branco; uso em ações comuns de maior prioridade.
- **Secundário:** fundo `#202B45`, texto `#F1F5F9`, borda `#334155`.
- **Destaque de marca:** `#EA580C`, texto branco; uso restrito para uma chamada realmente importante. Verificar contraste e estados hover/focus.
- **Destrutivo:** vermelho semântico `#F87171` ou variante mais escura com texto claro; confirmação para operações perigosas.
- **Desabilitado:** aparência esmaecida, sem depender apenas da cor para comunicar o bloqueio.
- **Hover/pressed:** derivar tonalidades consistentes; não usar glow forte em todos os botões.

**Exemplo de uso:** Fetch All pode ser uma ação primária; não é obrigatório que seja laranja. Reservar a cor da marca para identidade ou para um único CTA destacado quando houver justificativa.

### 5.3. Cards e linhas de repositórios

- Fundo `#172036`.
- Nome do repositório em `#F1F5F9`.
- Branch e caminho em `#94A3B8`.
- Ícone de repositório pode usar violeta.
- Estado Git usa cor semântica + ícone + texto.
- Hover e seleção devem ser distintos; seleção pode usar violeta/azul com borda ou fundo discreto.
- Não usar cores saturadas em toda a área do card para representar status.

### 5.4. Tabelas

- Cabeçalho levemente diferenciado da superfície.
- Linhas com divisores discretos.
- Hover sutil e seleção claramente visível.
- Status em badges com contraste adequado e rótulos textuais.
- Manter alinhamento e legibilidade para nomes de branches, SHAs e caminhos longos.

### 5.5. Formulários

- Inputs com superfície escura e borda `#334155`.
- Texto `#F1F5F9`; placeholder `#94A3B8` quando apropriado.
- Foco visível em azul ou violeta; não depender apenas de uma borda muito fina.
- Erros de validação em vermelho semântico com mensagem textual.
- Rótulos sempre legíveis.

### 5.6. Modais, menus e notificações

- Superfície elevada `#202B45`.
- Separação do conteúdo de fundo por borda e sombra discretas.
- Toasts e alertas utilizam cores semânticas, com ícones e mensagens explícitas.
- Logs e detalhes de erro devem ser legíveis, selecionáveis e copiáveis.

### 5.7. Indicadores de progresso

- Execução: violeta `#A78BFA`.
- Conclusão: verde `#34D399`.
- Falha: vermelho `#F87171`.
- Ignorado/cancelado: cinza, com texto que diferencie as condições.
- Mostrar progresso por repositório e resumo geral, evitando sugerir que uma operação coletiva é atômica.

## 6. Tipografia e legibilidade

**Títulos H1:** usar a cor de marca #EA580C em ambos os temas, por meio do token global --gc-heading-h1: var(--gc-brand). Aplicar a regra ao seletor h1; não alterar a cor dos demais níveis de título por consequência desta decisão.

- Preferir fontes sans-serif legíveis para a UI; a fonte definitiva ainda não foi escolhida.
- Para caminhos, branches, SHAs, comandos e logs, usar fonte monoespaçada.
- Texto principal `#F1F5F9` e secundário `#94A3B8`.
- Evitar textos extensos em violeta, laranja ou ciano.
- Usar cor `#64748B` somente para conteúdo realmente desabilitado ou decorativo; pode ter contraste insuficiente para texto essencial.

## 7. Acessibilidade

- Verificar contraste conforme WCAG 2.2 AA: em geral **4,5:1 para texto normal** e **3:1 para texto grande e componentes gráficos essenciais**, conforme critérios aplicáveis.
- O laranja da marca `#EA580C` pode funcionar como logotipo, mas não é automaticamente adequado para texto pequeno sobre qualquer superfície. Em texto pequeno sobre escuro, testar `#FB923C`.
- O azul `#3B82F6` sobre fundo escuro pode exigir texto branco e avaliação de contraste, dependendo do tamanho e contexto.
- Oferecer indicador de foco visível e navegação por teclado.
- Não comunicar estados apenas com cor.
- Considerar pessoas com daltonismo e ambientes de baixo brilho.
- Testar o tema em monitores diferentes e com brilho reduzido.

## 8. Gradientes e efeitos especiais

- **Degradê azul-violeta:** adequado para a marca, pequenos detalhes, ilustrações e indicadores especiais.
- **Glow espacial:** usar apenas em elementos pontuais de identidade, não em todas as bordas ou controles.
- **Planetas, estrelas e órbitas:** podem aparecer em onboarding, empty states ou identidade, mas não devem poluir a tela principal de trabalho.
- **Animações:** sutis e opcionais, respeitando preferências de redução de movimento.

## 9. Tokens CSS iniciais

```css
:root[data-theme='cosmic-dark'] {
  color-scheme: dark;

  --gc-bg: #090D1B;
  --gc-sidebar: #10172A;
  --gc-surface: #172036;
  --gc-surface-raised: #202B45;
  --gc-border: #334155;

  --gc-text: #F1F5F9;
  --gc-text-muted: #94A3B8;
  --gc-text-disabled: #64748B;

  --gc-primary: #3B82F6;
  --gc-brand: #EA580C;
  --gc-heading-h1: var(--gc-brand);
  --gc-brand-on-dark: #FB923C;
  --gc-violet: #8B5CF6;
  --gc-cyan: #22D3EE;

  --gc-success: #34D399;
  --gc-warning: #FBBF24;
  --gc-danger: #F87171;
  --gc-info: #60A5FA;
  --gc-running: #A78BFA;
  --gc-inactive: #94A3B8;
}
```

**Nota de implementação:** os tokens acima são a referência semântica do GitCosmos. Integrá-los à API de temas/presets da versão de **PrimeVue** efetivamente instalada, evitando sobrescritas CSS frágeis e globais. Não presumir nomes internos de tokens PrimeVue sem verificar a versão.

## 10. Exemplo conceitual de dashboard

Um dashboard de workspace poderá apresentar:

- Cabeçalho GitCosmos com o logotipo aprovado.
- Sidebar azul-marinho com seleção de workspace e navegação.
- Título “My Workspace” e contagem de repositórios.
- Ações Fetch All, Pull All, Push All e Checkout All, com prioridade visual definida pelo contexto.
- Lista de repositórios em cards escuros:
  - `authentication-api` — `develop` — **Clean** (verde).
  - `orders-api` — `feature/orders` — **Modified** (amarelo).
  - `products-api` — `develop` — **Clean** (verde).
  - `admin-frontend` — `main` — **Behind 2** (azul).
- Barra de status com resumo das operações e informações relevantes.

**Atenção:** esses nomes e estados são fictícios e servem apenas para demonstrar a aplicação da paleta. O layout e a experiência final ainda precisam ser projetados.

## 11. Recomendações de implementação no GitCosmos

1. Criar um arquivo central de tokens semânticos de tema.
2. Configurar um preset PrimeVue compatível com a versão instalada.
3. Implementar o tema escuro como padrão visual inicial.
4. Criar componentes de status Git reutilizáveis, sempre com rótulo e ícone.
5. Aplicar cores por **função**, evitando hexadecimais espalhados nos componentes.
6. Criar estados hover, active, focus, disabled, loading e error para controles.
7. Validar contraste e navegação por teclado.
8. Testar visualmente o dashboard, modais, formulários, tabelas e notificações.
9. Manter a logo original com **Git `#EA580C`** e **Cosmos azul-violeta**.
10. Registrar ajustes de cor aprovados no documento e nos tokens.

## 12. O que já foi decidido e o que continua aberto

### Direção visual aprovada

- Nome do tema: **Cosmic Dark**.
- Base azul-marinho quase preta.
- Superfícies escuras em camadas.
- Azul como principal cor de ação.
- Laranja `#EA580C` como cor da palavra “Git” na logo e destaque de marca.
- Violeta para seleção/detalhes especiais.
- Cores semânticas próprias para estados Git.
- Estética profissional, sem excesso de neon.

### Ainda não definido

- Fonte oficial.
- Layout definitivo.
- Tamanhos, espaçamentos e raios oficiais.
- Design do tema claro.
- Preset PrimeVue final.
- Regras exatas de hover/pressed e variantes de botões.
- Componentes e ícones definitivos.

## 13. Instrução para outro chat de desenvolvimento

> Utilize este documento como referência de identidade visual para o GitCosmos. Implemente o tema escuro **Cosmic Dark** no frontend Vue 3 + TypeScript + PrimeVue, com tokens semânticos reutilizáveis e suporte consistente a estados Git. Preserve a logo aprovada: “Git” em `#EA580C`, “Cosmos” em degradê azul-violeta e ícone da galáxia com três planetas conectados. Priorize legibilidade, acessibilidade e manutenção. Não considere o exemplo de dashboard um layout definitivo; primeiro verifique a estrutura existente do projeto e a versão instalada do PrimeVue.
