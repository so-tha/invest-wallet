# Guia de estudo: InvestWallet

Documento vivo: cresce a cada etapa. No final vira o material de preparação para entrevista.
Legenda: 🟢 já aplicado no projeto · 🟡 vem nas próximas etapas.

---

## 1. Perguntas de entrevista (com resposta-guia)

### P1. Por que você não guardou o saldo da carteira numa coluna? 🟢 (etapa 0)
**Resposta-guia:** O saldo (quantidade e preço médio) é *derivável* das transações. Se eu guardasse o valor calculado numa coluna, haveria duas fontes de verdade: o histórico e o saldo. Qualquer bug, falha no meio de uma operação ou edição manual poderia deixá-las divergentes. Calculando a partir do histórico, a única fonte da verdade é a lista de transações, e o saldo nunca fica errado.
**Contrapartida (mostra maturidade):** com milhões de transações o cálculo ficaria lento; aí se pode usar cache, uma tabela de resumo atualizada na mesma transação de banco, ou *snapshots* periódicos. Para o escopo do projeto, calcular na hora é mais simples e seguro.

### P2. O que significa o Domain não poder referenciar a Infrastructure? Que problema isso evita? 🟢 (etapa 1)
**Resposta-guia:** O Domain guarda as regras de negócio ("não vender mais do que possui"). A Infrastructure guarda detalhes técnicos (EF Core, Postgres). Se o Domain conhecesse a Infrastructure, as regras ficariam acopladas ao banco: não daria para testá-las sem subir um banco, e trocar de tecnologia exigiria mexer nas regras. Por isso as dependências apontam **para o núcleo**, e o núcleo não conhece os detalhes. No .NET isso é imposto pelos `ProjectReference`: se o Domain tentar usar algo da Infrastructure, o projeto não compila.
**Termo para citar:** *Inversão de Dependência* (o "D" do SOLID) e *Clean Architecture*.

### P3. Por que repetir a regra `quantidade > 0` no C# e também no banco? Não é duplicação? 🟢 (etapa 2)
**Resposta-guia:** É *defesa em profundidade*. A regra no C# dá feedback rápido e mensagens claras ao usuário. A restrição `CHECK` no banco protege os dados mesmo se alguém inserir por SQL direto, por outro serviço, por um script de migração ou por um bug em outra camada. O banco é o último guardião: dado inválido nunca persiste. O custo é pequeno e o ganho em integridade é grande.

### P4. Qual a diferença entre 400, 409 e 422? 🟢 (etapas 3 e 5)
**Resposta-guia:** `400 Bad Request`: a requisição está malformada ou inválida (campo faltando, tipo errado, texto grande demais). `409 Conflict`: a requisição é válida, mas conflita com o estado atual (ticker já existe, apagar carteira que tem transações). `422 Unprocessable Entity`: a requisição é bem formada, mas viola uma regra de negócio (vender mais do que possui). Regra prática: 400 = "não entendi/está inválido"; 409 = "o estado atual impede"; 422 = "entendi, mas o negócio não permite".
**Critério que usei no projeto:** se dá para decidir olhando só a requisição (e o relógio), é *validação* e vira 400 (nome vazio, quantidade ≤ 0, data no futuro). Se precisa consultar o estado do sistema (saldo, existência de registro), é *regra de negócio*: 422 ou 409.

### P5. Por que o controller não tem regra de negócio? 🟢 (etapa 3)
**Resposta-guia:** O controller só traduz HTTP: lê a requisição, chama um serviço e devolve o status certo. A regra (ticker único, não apagar carteira com transações) fica no serviço e na entidade, porque assim posso testá-la sem subir servidor nem banco, e reaproveitá-la se amanhã houver outra porta de entrada (fila, CLI, gRPC). É o princípio de *responsabilidade única* (o "S" do SOLID).

### P6. Para que serve a interface `IWalletRepository` se só existe uma implementação? 🟢 (etapa 3)
**Resposta-guia:** Ela é o ponto onde a *inversão de dependência* acontece: a Application declara o que precisa ("buscar carteira por id") e a Infrastructure implementa. Nos testes unitários, troco a implementação por um mock sem tocar em banco. Também permite trocar o EF por outra tecnologia sem mexer nas regras.

### P7. O que é um DTO e por que não devolver a entidade direto? 🟢 (etapa 3)
**Resposta-guia:** DTO (*Data Transfer Object*) é o formato dos dados que entram e saem da API. Se eu devolvesse a entidade, qualquer mudança no banco ou no domínio quebraria o contrato público da API, e eu poderia expor campos internos sem querer (ex.: coleções de navegação, gerando ciclos no JSON). Com DTOs, o contrato é estável e explícito.

### P8. Diferença entre `Scoped`, `Transient` e `Singleton`? Por que o `DbContext` é `Scoped`? 🟢 (etapa 3)
**Resposta-guia:** *Transient*: nova instância a cada vez que é pedida. *Scoped*: uma instância por requisição HTTP. *Singleton*: uma só para a aplicação inteira. O `DbContext` é Scoped porque (1) não é thread-safe, então duas requisições não podem dividir a mesma instância; (2) ele rastreia as mudanças das entidades que carrega, funcionando como uma *unidade de trabalho* da requisição; como Singleton, esse rastreamento acumularia dados velhos para sempre; como Transient, o repositório e o serviço da mesma requisição receberiam instâncias diferentes e não enxergariam as mudanças um do outro.

### P9. Como você garante que não dá para vender mais do que se possui? 🟢 (etapa 4)
**Resposta-guia:** Em três níveis. (1) *Regra de domínio*: o `PositionCalculator` reprocessa o histórico em ordem cronológica e lança exceção se a quantidade ficar negativa em qualquer ponto, não só no total final. (2) *Concorrência*: duas vendas simultâneas poderiam ler o mesmo saldo e ambas passar; por isso a validação roda dentro de uma transação de banco com um *advisory lock* por (carteira, ativo), serializando as operações do mesmo par. Testei: sem a trava, 6 vendas de 10 sobre um saldo de 20 passaram todas (saldo final −40); com a trava passam exatamente 2. (3) O banco ainda tem `CHECK (quantity > 0)` como última defesa.

### P10. Por que apagar uma transação pode ser proibido? 🟢 (etapa 4)
**Resposta-guia:** Porque o saldo é derivado do histórico. Apagar uma compra antiga pode deixar uma venda posterior sem lastro (saldo negativo no passado). Antes de remover, recalculo o histórico sem aquela transação; se ficar inválido, devolvo 422. É a regra RN6.

### P11. Como o preço médio é calculado? E por que vender não o altera? 🟢 (etapa 4)
**Resposta-guia:** Preço médio = custo total ÷ quantidade. Cada compra soma `quantidade × preço` ao custo. Uma venda retira do custo a fração proporcional à quantidade vendida (`custo × qtdVendida ÷ qtdAtual`), então o custo por unidade restante permanece o mesmo. O preço *de venda* só interessaria para calcular lucro realizado, que está fora do escopo. Quando a posição zera, o custo é zerado também, e uma nova compra começa um preço médio novo.

### P12. Por que usar `decimal` e não `double` para dinheiro? 🟢 (etapas 2 e 4)
**Resposta-guia:** `double` é binário e não representa exatamente valores como 0,1; em `double`, `0.1 + 0.2 != 0.3`. `decimal` é base 10 e exato para valores monetários. Há um teste unitário que mostra isso (`Usa_decimal_sem_erro_de_ponto_flutuante`). No banco, `numeric(18,6)`.

### P13. Por que testar o `PositionCalculator` sem banco? 🟢 (etapa 4)
**Resposta-guia:** Porque é uma função pura: dadas as transações, devolve a posição. Os 14 testes rodam em ~50 ms, sem Docker. Isso só é possível porque a regra mora no Domain e não depende de infraestrutura, que é o resultado prático da arquitetura em camadas.

### P14. Com milhões de transações por carteira, calcular a posição do zero a cada consulta seria viável? 🟢 (etapa 4)
**Resposta-guia:** Não. O custo cresce linearmente com o histórico. E não dá para simplesmente dividir o trabalho entre requisições, porque o preço médio é *sequencial*: cada transação depende do estado deixado pela anterior. O que se faz é não recalcular do zero: guardar um *snapshot* da posição (ex.: a cada N transações, ou uma tabela de resumo atualizada **na mesma transação de banco** em que a transação é gravada) e calcular só o que veio depois dele; ou usar cache com invalidação. A contrapartida é voltar a ter dois dados que podem divergir, por isso só se adota quando a medição mostra que é necessário (evitar otimização prematura).

### P15. Onde você valida: no controller, no serviço, no domínio ou no banco? 🟢 (etapa 5)
**Resposta-guia:** Em camadas, cada uma com um papel. **Borda (FluentValidation, na Application):** formato e limites do que o cliente enviou, com mensagens claras e *todos* os erros de uma vez → 400. **Domínio:** invariantes da entidade e regras de negócio ("não vender mais do que possui") → 422; é a última defesa em código. **Banco:** `CHECK`, `UNIQUE` e chaves estrangeiras; a defesa final, inclusive contra concorrência. Validar na borda dá boa experiência ao usuário; validar no domínio e no banco garante a integridade mesmo que outra porta de entrada seja criada no futuro.

### P16. Você checa "o ticker já existe?" antes de inserir. Isso basta? 🟢 (etapa 5)
**Resposta-guia:** Não. Entre o `SELECT` e o `INSERT` outra requisição pode inserir o mesmo ticker (*check-then-act*, uma condição de corrida). A checagem existe para dar uma mensagem amigável no caso comum, mas quem garante a unicidade é o **índice único do banco**. Quando ele rejeita (erro `23505` do PostgreSQL), eu traduzo para 409 em vez de deixar virar 500. Testei: 8 POSTs simultâneos do mesmo ticker resultaram em 1× 201 e 7× 409, com 1 linha no banco.

### P17. O que é ProblemDetails e por que padronizar o formato de erro? 🟢 (etapas 3 e 5)
**Resposta-guia:** É o padrão RFC 7807/9457 para erros de API em JSON (`title`, `status`, `detail`, `instance`, e extensões como `errors` e `traceId`). Padronizar permite ao cliente tratar *todo* erro do mesmo jeito. O `traceId` liga a resposta ao log do servidor, e por isso 500 mostra uma mensagem genérica ao usuário (segurança) e o detalhe fica só no log.

### P18. Qual a diferença entre log de texto e log estruturado? Por que usar `{Campo}` em vez de interpolar string? 🟢 (etapa 6)
**Resposta-guia:** No log de texto a informação está "achatada" numa frase; para achar algo é preciso fazer busca textual frágil. No log **estruturado**, cada evento é um registro com *propriedades* (`TransactionId`, `Ticker`, `WalletId`, `StatusCode`, `Elapsed`), gravado em JSON em produção, então ferramentas (Seq, Elastic, Datadog, Application Insights) filtram e agregam por campo: "todos os 500 de `/api/wallets` com mais de 1 s". Por isso escrevo `LogInformation("Transação {TransactionId} registrada", id)` (*message template*) e **não** `$"Transação {id} registrada"`: com o template, `TransactionId` vira propriedade pesquisável e o template (constante) agrupa eventos iguais; a interpolação gera uma string diferente a cada vez e perde a estrutura.

### P19. Como você acha a causa de um erro 500 reportado por um usuário? 🟢 (etapa 6)
**Resposta-guia:** A resposta de erro devolve um `traceId`. O usuário (ou o front-end) me informa esse id e eu busco por ele no log: todas as linhas daquela requisição carregam o mesmo `TraceId`, inclusive a exceção com stack trace, o SQL que falhou e a linha-resumo com status e duração. Testei: derrubei o banco, a API devolveu 500 genérico com `traceId`, e filtrando por ele encontrei as 4 linhas da falha. Em sistemas distribuídos esse id segue o padrão W3C Trace Context e atravessa vários serviços.

### P20. O que não se deve registrar em log, e que níveis usar? 🟢 (etapa 6)
**Resposta-guia:** Nunca registrar segredos (senhas, tokens, connection strings), dados pessoais/sensíveis (LGPD) nem corpos de requisição inteiros; o EF só imprime valores de parâmetros com `EnableSensitiveDataLogging`, que fica desligado. Níveis: `Information` = fluxo normal e fatos de negócio; `Warning` = algo inesperado mas tratado e causado pelo cliente (409/422); `Error` = falha nossa que precisa de atenção (500); `Fatal` = a aplicação não consegue continuar. Erros esperados (validação, "não encontrado") **não** devem ser `Error` com stack trace, senão o alerta perde o sentido: eu mesmo precisei silenciar o middleware do ASP.NET que registrava todo 422 como erro.

### P21. Qual a diferença entre teste unitário e de integração? Como você dividiu no projeto? 🟢 (etapa 7)
**Resposta-guia:** O unitário testa uma peça isolada, sem I/O, e roda em milissegundos: regras do domínio, validadores e serviços com dependências substituídas. O de integração sobe a aplicação de verdade (HTTP, serialização, DI, EF, migrations) contra um PostgreSQL real e prova que as peças funcionam **juntas**. No projeto: 71 unitários em ~0,3 s e 38 de integração em ~1 s depois de o container subir. Seguem a *pirâmide de testes*: muitos unitários baratos na base, menos testes de integração caros no meio e pouquíssimos E2E no topo (esses entram na fase 2, com Playwright, quando houver interface).

### P22. Mock, fake e stub: qual a diferença? Quando usou cada um? 🟢 (etapa 7)
**Resposta-guia:** *Stub* devolve respostas fixas. *Mock* é programado e **verifica interações** ("o `AddAsync` foi chamado 0 vezes?"). *Fake* é uma implementação simplificada que funciona de verdade (um repositório em memória). Usei **mock** (NSubstitute) para repositórios simples, onde interessa verificar "não salvou" ou "chamou com o ticker normalizado". Usei **fake** para as transações, porque os testes precisam ver o histórico crescer a cada operação; programar isso com mock seria verboso e frágil.

### P23. Como você sabe que seus testes realmente protegem o código? 🟢 (etapa 7)
**Resposta-guia:** Cobertura só mostra que a linha foi *executada*, não que um defeito seria *percebido*. Por isso fiz **testes de mutação manuais**: quebrei de propósito uma proteção e confirmei que algum teste falhava. Desliguei a trava de concorrência (o teste de vendas simultâneas falhou), ignorei a ordem cronológica (3 testes falharam, em duas camadas) e removi a tradução de erro de unicidade (o teste da corrida de ticker falhou). Ferramentas como Stryker.NET automatizam essa ideia.

### P24. Como testar código que depende da hora atual? E concorrência? 🟢 (etapa 7)
**Resposta-guia:** **Hora:** injeto uma abstração (`TimeProvider`) e nos testes uso um relógio falso (`FakeTimeProvider`) fixo e que posso avançar, então o teste é determinístico (a regra "data no futuro" é verificada no segundo exato). **Concorrência:** não dá para provar com unitário; uso teste de integração com requisições realmente paralelas (`Task.WhenAll`) contra o banco real, e asserto o *invariante* (nunca vender mais do que a posição), não a ordem em que as requisições terminam. Testes de concorrência bem escritos não dependem de timing.

### P25. Por que o teste de integração usa um banco real e não um InMemory? 🟢 (etapa 7)
**Resposta-guia:** O provedor InMemory do EF não aplica chaves estrangeiras, índices únicos, `CHECK`, transações nem `advisory lock`: justamente o que garante a integridade do meu sistema. Um teste que passa no InMemory pode falhar em produção. Com Testcontainers, cada execução sobe um PostgreSQL idêntico ao de produção, aplica as migrations reais e é descartado depois.

*(Novas perguntas serão adicionadas a cada etapa.)*

---

## 2. Conceitos explicados

### Arquitetura e SOLID
- **Camadas (Domain / Application / Infrastructure / Api):** cada camada tem uma responsabilidade; dependências apontam para dentro. Facilita testes e manutenção.
- **Inversão de Dependência:** o código de alto nível (regras) depende de *interfaces*, não de implementações concretas (banco). A implementação é "plugada" de fora.
- **ADR (Architecture Decision Record):** documento curto que registra uma decisão técnica: contexto, decisão, alternativas e consequências. Veja `docs/adr/`.

### .NET
- **Solution (`.sln`) e projeto (`.csproj`):** a solution agrupa projetos (como um workspace). Cada `.csproj` é como um `package.json`: tipo do projeto, versão do .NET, dependências e referências a outros projetos.
- **`Program.cs`:** ponto de entrada (como o `server.ts` do Express). Primeiro *registra serviços* (`builder.Services...`), depois monta o *pipeline* de requisição (`app.Use...`/`app.Map...`).
- **Injeção de dependência (DI):** em vez de uma classe criar suas dependências (`new Repositorio()`), ela as recebe pelo construtor, e o framework entrega a instância registrada. Permite trocar a implementação (ex.: mock em teste).
- **Configuração por variáveis de ambiente:** `ConnectionStrings__Default` no ambiente vira `ConnectionStrings:Default` na configuração. Segredos ficam fora do código e do Git (`.env` ignorado, `.env.example` versionado).
- **Health check (`/health`):** endpoint simples que diz se a aplicação está viva; usado por Docker, Kubernetes e CI.

### C#
- **Propriedade com `private set`:** `public string Name { get; private set; }`. Qualquer um lê, só a própria classe escreve. As regras ficam protegidas dentro da entidade.
- **Tipos anuláveis (`string?`):** o `?` diz que o valor pode ser `null`; o compilador avisa se você esquecer de tratar. Sem `?`, o valor não deve ser nulo.
- **Pattern matching (`is { Length: > 300 }`):** testa a "forma" de um objeto de forma concisa.
- **`decimal` vs. `float`/`double`:** `float` é binário e acumula erros de arredondamento (`0.1 + 0.2 ≠ 0.3`). Para dinheiro e quantidades, use sempre `decimal` (no banco, `numeric`).
- **Construtor privado sem parâmetros:** usado pelo EF Core para materializar objetos lidos do banco; o construtor público é o único caminho para criar uma entidade válida.

### Banco de dados e EF Core
- **ORM:** mapeia tabelas para classes. O EF Core gera o SQL.
- **`DbContext` / `DbSet<T>`:** o `DbContext` é uma sessão com o banco; cada `DbSet` representa uma tabela.
- **Migration:** histórico versionado do esquema, gerado a partir do modelo C#. Vai para o Git; `database update` aplica o que falta.
- **`IEntityTypeConfiguration<T>`:** mantém o mapeamento fora das entidades, preservando o Domain "limpo".
- **`ON DELETE RESTRICT` (`DeleteBehavior.Restrict`):** o banco recusa apagar um registro referenciado por outro.
- **Índice único:** garante unicidade (ticker) e acelera buscas.
- **`CHECK constraint`:** regra validada pelo próprio banco.
- **Convenção snake_case:** `CreatedAt` → `created_at`, o padrão no PostgreSQL.

### Docker
- **Volume nomeado (`pgdata`):** os dados do banco sobrevivem a `docker compose down`.
- **Healthcheck:** o Compose sabe quando o banco está pronto (`pg_isready`).

### Etapa 3: CRUD
- **DTO como `record`:** `record WalletRequest(string Name, string? Description)` declara um tipo imutável com construtor, propriedades e igualdade por valor em uma linha. Ideal para dados que só trafegam.
- **Interface + implementação + DI:** `IWalletService` (contrato) e `WalletService` (código). O `AddScoped<IWalletService, WalletService>()` diz ao contêiner: "quando alguém pedir a interface, entregue esta classe".
- **Tempos de vida na DI:** *Transient* (nova instância a cada pedido), *Scoped* (uma por requisição HTTP, ideal para `DbContext` e serviços), *Singleton* (uma para a aplicação inteira).
- **`async`/`await` e `Task`:** operações de I/O (banco) não bloqueiam a thread; o servidor atende outras requisições enquanto espera. Equivale a `Promise`/`await` do TypeScript.
- **`CancellationToken`:** se o cliente desiste da requisição, o token sinaliza e a consulta ao banco é cancelada, economizando recursos.
- **`[ApiController]`:** ativa comportamentos automáticos: valida o corpo (400 com detalhes), infere de onde vêm os parâmetros e padroniza erros.
- **`CreatedAtAction`:** devolve `201 Created` com o cabeçalho `Location` apontando para o novo recurso, como manda o REST.
- **Rastreamento do EF (`AsNoTracking`):** por padrão o EF rastreia as entidades lidas para detectar mudanças. Em consultas só de leitura, `AsNoTracking` evita esse custo.
- **Paginação (`Skip`/`Take`) com ordenação estável:** sem `OrderBy`, a página pode repetir ou pular itens. O `ThenBy(Id)` desempata.
- **Limite máximo de `pageSize`:** evita que alguém peça 1 milhão de linhas e derrube o servidor.
- **ProblemDetails (RFC 7807):** formato padrão de erro em JSON (`title`, `status`, `detail`, `instance`). Todo erro da API tem o mesmo formato.
- **`IExceptionHandler` (.NET 8):** ponto único que converte exceções em respostas HTTP. Em 500 nunca expõe a mensagem interna (segurança); só registra no log.
- **Enums como texto no JSON:** `"Stock"` em vez de `1`, mais legível e estável se a ordem do enum mudar.
- **Mapeamento manual (`ToResponse`) em vez de AutoMapper:** para poucos campos, é mais explícito, fácil de depurar e sem "mágica".

### Etapa 4: Transações e posição
- **Método do preço médio:** custo total ÷ quantidade. Compra soma ao custo; venda retira o custo proporcional. É o método usado em carteiras de renda variável no Brasil.
- **Função pura:** mesma entrada, mesma saída, sem efeitos colaterais (sem banco, sem relógio). Fácil de testar.
- **Ordenação determinística:** ordenar por data, depois `CreatedAt`, depois `Id`. Sem desempate, transações no mesmo instante poderiam produzir resultados diferentes a cada execução.
- **Condição de corrida (race condition):** duas operações leem o mesmo estado, ambas decidem que podem prosseguir e juntas violam a regra. Clássico "check-then-act".
- **Transação de banco:** grupo de operações que acontecem todas ou nenhuma (`BEGIN`/`COMMIT`/`ROLLBACK`).
- **Advisory lock (PostgreSQL):** trava "lógica", identificada por um número, que a própria aplicação escolhe. `pg_advisory_xact_lock` é liberada sozinha no fim da transação. Alternativas: `SELECT ... FOR UPDATE` numa linha, isolamento `Serializable` com retry, ou concorrência otimista com coluna de versão.
- **`Func<Task<T>>`:** um "callback" assíncrono que devolve `T`. Permite passar um bloco de código para ser executado dentro da trava, como `async () => {...}` em TypeScript.
- **`TimeProvider` (.NET 8):** abstração do relógio. Em produção usa o relógio real; em testes, um relógio falso permite simular datas.
- **`DateTimeOffset` vs `DateTime`:** `DateTimeOffset` carrega o fuso; converto com `.UtcDateTime`. O Npgsql exige UTC em colunas `timestamptz`.
- **Teste parametrizado (`[Theory]` + `[InlineData]`):** um método de teste roda várias vezes com entradas diferentes.
- **Coleção expressions (`[Buy(...), Sell(...)]`, C# 12):** sintaxe curta para criar listas.
- **`Include` (eager loading):** pede ao EF para trazer a entidade relacionada (o `Asset`) no mesmo SELECT, evitando consultas extras (problema N+1).
- **422 para referência inexistente no corpo:** se o `assetId` enviado não existe, a URL é válida mas o conteúdo não é processável.

### Etapa 5: validação e erros
- **FluentValidation:** validação declarativa em classes `AbstractValidator<T>` (`RuleFor(x => x.Name).NotEmpty()...`), separada do modelo. Todas as regras são executadas e os erros voltam juntos.
- **`ValidateAndThrowAsync`:** executa o validador e lança `ValidationException` se houver erro; o `GlobalExceptionHandler` a converte em 400.
- **`Cascade(CascadeMode.Stop)`:** para na primeira regra que falha em uma propriedade, evitando erros redundantes (ex.: "maior que zero" e "casas decimais" para o mesmo valor).
- **`PrecisionScale(18, 6, ...)`:** valida total de dígitos e casas decimais; espelha o `numeric(18,6)` do banco.
- **`AddValidatorsFromAssemblyContaining<T>()`:** registra na DI todos os validadores do projeto automaticamente.
- **`SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`:** impede o MVC de criar validação própria (em inglês) para tipos não anuláveis; centralizamos tudo no FluentValidation.
- **`InvalidModelStateResponseFactory`:** personaliza a resposta quando o MVC não consegue montar o objeto (JSON malformado, enum inexistente, `?page=abc`).
- **`ModelBindingMessageProvider`:** traduz as mensagens de erro do próprio MVC.
- **`SaveChangesAsync` sobrescrito:** captura `DbUpdateException` com `PostgresException` e traduz por `SqlState` (`23505` = violação de unicidade, `23503` = violação de chave estrangeira) em `ConflictException`.
- **`OperationCanceledException`:** quando o cliente desiste da requisição, não é erro do servidor; ignoramos em vez de registrar como falha.
- **Versões fixas de pacotes:** `Version="8.0.*"` muda o build sem você perceber; fixar a versão exata torna o build reproduzível.

### Etapa 6: logs
- **Serilog:** biblioteca de logging estruturado. Configurada pelo `appsettings.json` (níveis, destinos, formato), então mudar o comportamento não exige recompilar.
- **Sink:** destino do log (console, arquivo, Seq, Elastic...). Usei o console: em containers a prática é escrever em `stdout` e deixar a plataforma coletar.
- **`RenderedCompactJsonFormatter`:** um evento por linha, em JSON (`@t` data, `@l` nível, `@m` mensagem, `@x` exceção + propriedades). Em desenvolvimento uso um formato legível.
- **Nível mínimo e `Override`:** `Default: Information`, mas bibliotecas barulhentas (`Microsoft.*`, `System.*`) ficam em `Warning`; é possível ajustar por categoria.
- **`LogContext.PushProperty`:** anexa uma propriedade (o `TraceId`) a toda linha escrita dentro do bloco `using`, sem precisar passá-la de método em método.
- **`UseSerilogRequestLogging`:** uma linha-resumo por requisição (método, rota, status, duração). Health checks são rebaixados para não poluir.
- **Ordem do pipeline:** o log de requisição fica antes do tratador de exceções, para registrar o status final; o contexto do `TraceId` fica antes de tudo.
- **TraceId (W3C Trace Context):** 32 caracteres hexadecimais gerados por requisição (`Activity.Current.TraceId`). Aparece na resposta de erro e em cada linha de log.
- **Bootstrap logger:** logger mínimo para registrar falhas de inicialização (ex.: connection string ausente), antes da configuração completa existir. `Log.CloseAndFlush()` no `finally` garante que as últimas linhas não se percam.
- **`GetLevel` personalizado:** decide o nível de cada requisição (erro de servidor → `Error`; `/health` → `Verbose`, ou seja, oculto).
- **12-Factor App (logs):** a aplicação não gerencia arquivos de log; escreve eventos no fluxo padrão e o ambiente os coleta.

### Etapa 7: testes
- **xUnit:** `[Fact]` (um caso) e `[Theory]` + `[InlineData]` (vários casos com a mesma lógica). Cada teste roda com uma instância nova da classe, então o construtor serve de *setup*.
- **Arrange / Act / Assert:** preparar, executar, verificar. Um teste deve verificar um comportamento e ter um nome que o descreva (`Venda_maior_que_a_posicao_lanca_DomainException_e_nao_grava`).
- **NSubstitute (mocks):** `Substitute.For<IRepo>()`, `.Returns(...)` programa respostas, `.Received(1)` / `.DidNotReceive()` verificam chamadas. `Arg.Any<T>()` casa qualquer argumento.
- **`FluentValidation.TestHelper`:** `TestValidate(x).ShouldHaveValidationErrorFor(p => p.Campo)` testa um validador sem subir nada.
- **`FakeTimeProvider`:** relógio controlável (`Advance`) para testar regras que dependem de "agora".
- **`WebApplicationFactory<Program>`:** sobe a API inteira em memória, com um `HttpClient` que fala com ela sem rede. Exige a linha `public partial class Program;` porque o `Program` gerado por top-level statements é interno.
- **Testcontainers:** sobe containers Docker pelo próprio código de teste. O **Ryuk** é um container auxiliar que remove tudo ao final, mesmo se o teste travar.
- **`IClassFixture` / `ICollectionFixture`:** compartilham um recurso caro (o container) entre vários testes; `[Collection]` agrupa as classes que o usam.
- **Isolamento sem limpar o banco:** cada teste cria dados únicos (ticker aleatório, carteira própria), então não dependem uns dos outros nem da ordem de execução.
- **Teste de mutação (manual):** estragar o código de propósito para provar que o teste certo falha.
- **Pirâmide de testes:** muitos unitários, alguns de integração, poucos E2E.
- **Asserir o invariante, não o timing:** no teste de concorrência verifico "no máximo 2 vendas passam", e não "qual requisição ganhou".

*(Novos conceitos serão adicionados a cada etapa.)*

---

## 3. Decisões técnicas (resumo)
| Decisão | Motivo | Alternativa descartada |
|---|---|---|
| Posição calculada, não armazenada | Única fonte da verdade | Coluna de saldo (risco de divergência) |
| 4 projetos em camadas | Testabilidade, clareza | Projeto único (mais simples, menos separação) |
| PostgreSQL em Docker | Leve, gratuito, comum no mercado | SQL Server |
| `decimal`/`numeric(18,6)` | Precisão financeira | `float` |
| Regras no C# **e** no banco | Defesa em profundidade | Só no C# |
| Sem autenticação na v1 | Foco no CRUD | JWT (melhoria futura) |

---

## 4. Erros que aconteceram (ótimo material para "conte um problema que você resolveu")
- `.env` com `;` sem aspas quebrou `source .env`: o shell tratou `;` como separador. Solução: aspas. Lição: um health check que não toca no banco não prova que o banco está configurado.
- `dotnet ef migrations script --no-build` gerou script vazio: usou binário antigo, anterior à migration. Lição: recompile depois de criar a migration.
- Ao tentar provar que a trava de concorrência era necessária, usei `wait $(jobs -p ...)` e o script esperou o próprio servidor da API para sempre, deixando o código com a trava desativada. Parei a tarefa, restaurei o arquivo de um backup e confirmei com `grep`. Lição: antes de um experimento destrutivo, guarde backup e planeje a restauração; e `wait` sem PIDs específicos espera todos os processos em segundo plano.
- O primeiro log que vi tinha **todo 422/409/400 como `ERR` com stack trace**: o middleware do ASP.NET registra como erro qualquer exceção, mesmo quando um `IExceptionHandler` a trata. Só percebi porque olhei o log de verdade em vez de supor que estava certo. Solução: silenciar essa categoria e deixar o nosso tratador decidir o nível.
- O NuGet escolheu `Serilog.AspNetCore 10.0.0` (para .NET 10) num projeto .NET 8. Sem `--version`, o NuGet pega a versão mais recente, que pode não casar com o seu runtime; troquei para a linha 8.x.
- Os 38 testes de integração passaram de primeira, e isso é suspeito: um teste que nunca falhou não provou nada. Quebrei 3 proteções de propósito (trava, ordenação, tradução de erro) e os testes certos falharam. Lição: desconfie de teste verde que nunca viu vermelho.
- Escrevi um teste de arredondamento com uma expressão sem sentido (`10m / 3m == 0 ? 1 : 3.333333333m`) e 9 casas decimais, que a própria entidade recusou. O teste estava errado, não o código; reescrevi com dados que realmente geram dízima (custo 4 para 3 unidades).
- Repeti o erro do `pkill -f`: usei `pgrep -f "InvestWallet.Api.dll"` num laço com `kill`, e ele também casou com o shell que rodava o comando (a linha de comando continha o texto), que foi encerrado (código 144). Lição: para achar um processo, use a porta (`ss -ltnp 'sport = :5265'`) ou guarde o PID ao iniciar. Além disso, `wait %2 %3...` esperou jobs errados; o correto é guardar os PIDs em um array e fazer `wait $PIDS`.
- Adicionei pacotes com `--version '8.0.*'` e o `.csproj` ficou com versão flutuante; só percebi na etapa 5 e fixei todas as versões.
- `pkill -f nome` matou o próprio shell, porque a linha de comando continha o mesmo texto. Lição: guarde o PID (`$!`) e use `kill`.
