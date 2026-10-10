# Progresso do projeto: InvestWallet (API de carteira de investimentos)

**Vaga:** Dev júnior full-stack, CN Innovation
**Stack:** C# / .NET 8, ASP.NET Core, EF Core, PostgreSQL (Docker), xUnit, Swagger, GitHub Actions
**Nível da usuária:** C# iniciante · TS médio · SQL médio · Git avançado · Docker médio
**Última atualização:** 2026-10-10 (revisão final)

## Requisitos da vaga → situação final
A matriz completa, com evidências e lacunas, está em [`docs/REVISAO-FINAL.md`](docs/REVISAO-FINAL.md). Resumo:

| Requisito | Situação |
|---|---|
| Backend C#/.NET, POO, SOLID, camadas, HTTP/REST | ✅ |
| SQL e modelagem relacional | ✅ |
| Testes automatizados (unitários e de integração) | ✅ 133 testes, 98,8% de cobertura de linhas |
| Documentação técnica, ADRs leves | ✅ README, `docs/`, Swagger, 5 ADRs |
| Docker | ✅ |
| Logs, tratamento de erros, investigação de bugs | ✅ (sem métricas/tracing distribuído) |
| Git | ✅ commits pequenos · 🟡 **fluxo de PR não exercitado** (tudo na `main`) |
| CI/CD (GitHub Actions) | 🟡 escrito e validado localmente; **nunca rodou no GitHub**; sem CD |
| Frontend (React/Angular) e testes E2E (Playwright) | ❌ fase 2 |
| Integração com LLM | ❌ etapa 10 não feita |
| Azure, mensageria, cache | ❌ |
| Repositório publicado no GitHub | ❌ **próximo passo** |

## Etapas
- [x] 0. Análise da vaga e planejamento (docs/)
- [x] 1. Setup: solução, projetos, Git, Docker Compose, configuração
- [x] 2. Banco de dados: entidades, DbContext, migration inicial
- [x] 3. CRUD de Carteiras e Ativos
- [x] 4. Transações + regras de negócio (compra/venda, posição, preço médio)
- [x] 5. Validações (FluentValidation → 400) e refinar erros. *(GlobalExceptionHandler com ProblemDetails já feito na etapa 3)*
- [x] 6. Logs estruturados
- [x] 7. Testes automatizados (unitários + integração)
- [x] 8. Swagger/OpenAPI documentado
- [x] 9. README, Dockerfile da API, GitHub Actions
- [ ] 10. (Opcional, **não feita**) Importar nota de corretagem via LLM com fallback
- [x] 11. Revisão final e docs/ESTUDO.md

## Próxima etapa
Projeto concluído no escopo combinado. Próximos passos, em ordem de valor (detalhes em [`docs/REVISAO-FINAL.md`](docs/REVISAO-FINAL.md), seção 7): publicar no GitHub e ver o CI rodar; trabalhar por branches e Pull Requests; frontend em React com Playwright; autenticação JWT; integração com LLM.

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
- Etapa 7 concluída: 71 testes unitários + 38 de integração (Testcontainers/PostgreSQL), todos passando. 3 mutações manuais detectadas (trava de concorrência, ordenação cronológica, tradução de erro de unicidade).
- Pendência: não medi cobertura de código (o `coverlet.collector` já está nos projetos de teste); E2E com Playwright fica para a fase 2 com o frontend.
- Para a etapa 9 (CI): os testes de integração precisam de Docker; os runners `ubuntu-latest` do GitHub Actions já têm.
- Etapa 8 concluída: 15/15 operações com resumo, 40 respostas de erro documentadas, exemplos nos 3 corpos de requisição, enums como texto, `application/problem+json` nos erros, `Swagger:Enabled` para expor fora de Development. 9 testes de contrato (suíte total: 71 unitários + 47 de integração).
- Decisão: Swagger desligado em produção por padrão (ver P28); o container do projeto o ligará via `Swagger__Enabled=true`.
- Para a etapa 9: a API em container roda em `Production` → definir `ConnectionStrings__Default`, `Swagger__Enabled=true`, aplicar migrations na subida (decidir como) e esperar o healthcheck do banco.
- Etapa 9 concluída: Dockerfile multi-stage (335 MB, usuário não-root), compose com API + banco (healthchecks), migrations na subida atrás de flag (ADR 0004), CI em 2 jobs, README final, template de PR e 5 ADRs.
- Verificado localmente: build Release com -warnaserror limpo, 118 testes passando, imagem sobe e fica healthy, migration aplicada em banco vazio, restart idempotente, comando do CI (`compose up --wait`) + smoke test executados.
- **Não verificado:** o workflow nunca rodou no GitHub (só YAML válido + comandos executados localmente).
- **Revisão final (2026-10-10):** clone limpo → build Release `-warnaserror` sem warnings, 118 testes (na época) passando e roteiro do README funcionando; depois, readiness com consulta ao banco, testes do 500, das migrations e da tradução de FK, chegando a **133 testes e 98,8% de cobertura**. Documentos corrigidos onde mentiam (PROGRESSO prometia PRs que não houve; docs/04 citava biblioteca não usada).
- **Extensão (pós-revisão): Sentry.** Integração opcional (desligada sem DSN), só erros 500, sem PII, limpador de segredos, tag `trace_id`; ADR 0006. Suíte: **154 testes (74 + 80), 98,9% de cobertura sem código gerado**. Verificado: container sobe saudável sem DSN; comportamento com transporte simulado; 1 mutação de 2 alterações detectada. **Não verificado: entrega real ao Sentry** (falta um DSN).
- **Estado final medido (após a verificação com envio real ao Sentry):** 162 testes (74 unitários + 88 de integração), cobertura de linhas **98,9% de 808 linhas** sem código gerado (89,6% contando os regex gerados). Sentry verificado com 3 eventos reais de teste; payload final sem nome da máquina nem usuário do sistema.
