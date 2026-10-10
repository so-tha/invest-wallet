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

### P4. Qual a diferença entre 400, 409 e 422? 🟡 (etapas 3 e 5)
**Resposta-guia:** `400 Bad Request`: a requisição está malformada ou inválida (campo faltando, tipo errado, texto grande demais). `409 Conflict`: a requisição é válida, mas conflita com o estado atual (ticker já existe, apagar carteira que tem transações). `422 Unprocessable Entity`: a requisição é bem formada, mas viola uma regra de negócio (vender mais do que possui). Regra prática: 400 = "não entendi/está inválido"; 409 = "o estado atual impede"; 422 = "entendi, mas o negócio não permite".

### P5. Por que o controller não tem regra de negócio? 🟢 (etapa 3)
**Resposta-guia:** O controller só traduz HTTP: lê a requisição, chama um serviço e devolve o status certo. A regra (ticker único, não apagar carteira com transações) fica no serviço e na entidade, porque assim posso testá-la sem subir servidor nem banco, e reaproveitá-la se amanhã houver outra porta de entrada (fila, CLI, gRPC). É o princípio de *responsabilidade única* (o "S" do SOLID).

### P6. Para que serve a interface `IWalletRepository` se só existe uma implementação? 🟢 (etapa 3)
**Resposta-guia:** Ela é o ponto onde a *inversão de dependência* acontece: a Application declara o que precisa ("buscar carteira por id") e a Infrastructure implementa. Nos testes unitários, troco a implementação por um mock sem tocar em banco. Também permite trocar o EF por outra tecnologia sem mexer nas regras.

### P7. O que é um DTO e por que não devolver a entidade direto? 🟢 (etapa 3)
**Resposta-guia:** DTO (*Data Transfer Object*) é o formato dos dados que entram e saem da API. Se eu devolvesse a entidade, qualquer mudança no banco ou no domínio quebraria o contrato público da API, e eu poderia expor campos internos sem querer (ex.: coleções de navegação, gerando ciclos no JSON). Com DTOs, o contrato é estável e explícito.

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
- `pkill -f nome` matou o próprio shell, porque a linha de comando continha o mesmo texto. Lição: guarde o PID (`$!`) e use `kill`.
