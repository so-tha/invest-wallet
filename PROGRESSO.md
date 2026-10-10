# Progresso do projeto: InvestWallet (API de carteira de investimentos)

**Vaga:** Dev júnior full-stack, CN Innovation
**Stack:** C# / .NET 8, ASP.NET Core, EF Core, PostgreSQL (Docker), xUnit, Swagger, GitHub Actions
**Nível da usuária:** C# iniciante · TS médio · SQL médio · Git avançado · Docker médio
**Última atualização:** 2026-10-10

## Requisitos da vaga → onde serão atendidos
| Requisito | Tipo | Etapa / local |
|---|---|---|
| Backend C#/.NET | Obrigatório | Todo o projeto |
| POO, SOLID, camadas | Obrigatório | Etapa 1 e 3 (Domain/Application/Infrastructure/Api) |
| HTTP/REST | Obrigatório | Etapa 3 em diante |
| SQL e modelagem relacional | Obrigatório | Etapa 2 (migrations, FKs, índices) |
| Git e fluxo de PR | Obrigatório | Branches + PRs por etapa |
| Testes automatizados | Obrigatório | Etapa 7 (unitários + integração) |
| Documentação técnica / ADRs leves | Desejável | docs/adr, Etapa 8 |
| Tratamento de erros, logs | Responsabilidade | Etapa 5 |
| Docker | Diferencial | Etapa 1-2 (Postgres) e 9 |
| CI/CD GitHub Actions | Diferencial | Etapa 9 |
| Integração com LLM (fallback, custo) | Diferencial | Etapa 10 (opcional) |
| Frontend (React/Angular) + Playwright | Responsabilidade | Fase 2 (fora deste escopo) |
| Fintech | Diferencial | Tema do projeto |

## Etapas
- [x] 0. Análise da vaga e planejamento (docs/)
- [x] 1. Setup: solução, projetos, Git, Docker Compose, configuração
- [x] 2. Banco de dados: entidades, DbContext, migration inicial
- [x] 3. CRUD de Carteiras e Ativos
- [x] 4. Transações + regras de negócio (compra/venda, posição, preço médio)
- [x] 5. Validações (FluentValidation → 400) e refinar erros. *(GlobalExceptionHandler com ProblemDetails já feito na etapa 3)*
- [x] 6. Logs estruturados
- [ ] 7. Testes automatizados (unitários + integração)
- [ ] 8. Swagger/OpenAPI documentado
- [ ] 9. README, Dockerfile da API, GitHub Actions
- [ ] 10. (Opcional) Importar nota de corretagem via LLM com fallback
- [ ] 11. Revisão final e docs/ESTUDO.md

## Próxima etapa
Etapa 7: Testes automatizados (unitários dos serviços/validadores + integração dos endpoints com Postgres de teste).

## Decisões e suposições
- PostgreSQL em Docker (a vaga não especifica banco) — suposição.
- Sem autenticação na v1 (foco no CRUD); listado como melhoria — decisão de escopo.
- Frontend e E2E ficam para a fase 2 — Playwright precisa de interface.

## Problemas encontrados
- `pkill -f` com nome do projeto matou o próprio shell (a linha de comando continha o padrão) → usar `pgrep` + `kill <pid>` ou guardar o PID.
- `.env` com `;` sem aspas quebrou `source .env` (o shell tratou `;` como separador) → valor entre aspas. O `/health` não detectou porque não toca no banco.
- `dotnet ef migrations script --no-build` usou binário antigo e saiu sem as tabelas → rodar sem `--no-build` logo após criar a migration.
- zsh não divide variável com espaços em palavras (`$P cmd args`) → usar função shell.
- Adiantei o `GlobalExceptionHandler` (404/409/422/500) para a etapa 3, senão "não encontrado" viraria 500 e não daria para testar.
- Pendências para a etapa 5: validação de campo (nome vazio, ticker vazio) hoje devolve 422 e deve ser 400; mensagem de enum inválido ("Banana") ficou confusa; capturar `DbUpdateException` de unicidade (corrida entre dois POSTs com o mesmo ticker).
- `createdAt` da resposta do POST tem 7 casas decimais de segundo e o banco guarda 6 (microssegundos) → GET devolve ligeiramente diferente. Inofensivo; truncar no domínio se incomodar.
- Etapa 4: provei com 6 vendas paralelas que sem a trava o saldo fica negativo (−40) e com a trava só 2 de 6 passam. Experimento exigiu parar e restaurar o arquivo (ver ESTUDO.md, seção 4).
- Etapa 4: 14 testes unitários do `PositionCalculator` já escritos (a etapa 7 cobrirá serviços e integração/endpoints).
- Pendência etapa 5: quantidade/preço com mais de 6 casas ou 0 hoje dão 422; devem ser 400 (validação de entrada). O enum inválido continua com mensagem confusa.
- Etapa 5 concluída: FluentValidation (400), domínio (422), banco (409 via SqlState 23505/23503). Corrida de ticker: 8 POSTs simultâneos → 1×201 + 7×409.
- Limitação conhecida: se o JSON tem um campo de tipo inválido (ex.: enum "Banana"), o MVC reporta só esse erro e os demais campos só aparecem depois de corrigi-lo (a desserialização falha antes da validação).
- Para a etapa 6: o EF registra em nível `fail` os erros de unicidade que depois viram 409 (ruído no log); ajustar o nível da categoria `Microsoft.EntityFrameworkCore.Update`.
- Etapa 6 concluída: Serilog (texto em Development, JSON nos demais ambientes), `TraceId` W3C em toda linha e na resposta de erro, log de negócio na criação/remoção de transações. Provado: banco derrubado → 500 com traceId → 4 linhas no log com o mesmo id.
- Para a etapa 7: `Program` precisa ser `public partial` para o `WebApplicationFactory`; os validadores e `TransactionService` (agora com `ILogger`) pedem testes; usar `NullLogger<T>` e `FakeTimeProvider`.
