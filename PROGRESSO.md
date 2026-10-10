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
- [ ] 2. Banco de dados: entidades, DbContext, migration inicial
- [ ] 3. CRUD de Carteiras e Ativos
- [ ] 4. Transações + regras de negócio (compra/venda, posição, preço médio)
- [ ] 5. Validações e tratamento de erros padronizado (ProblemDetails)
- [ ] 6. Logs estruturados
- [ ] 7. Testes automatizados (unitários + integração)
- [ ] 8. Swagger/OpenAPI documentado
- [ ] 9. README, Dockerfile da API, GitHub Actions
- [ ] 10. (Opcional) Importar nota de corretagem via LLM com fallback
- [ ] 11. Revisão final e docs/ESTUDO.md

## Próxima etapa
Etapa 2: Banco de dados (entidades, DbContext, migration inicial).

## Decisões e suposições
- PostgreSQL em Docker (a vaga não especifica banco) — suposição.
- Sem autenticação na v1 (foco no CRUD); listado como melhoria — decisão de escopo.
- Frontend e E2E ficam para a fase 2 — Playwright precisa de interface.

## Problemas encontrados
- `pkill -f` com nome do projeto matou o próprio shell (a linha de comando continha o padrão) → usar `pgrep` + `kill <pid>` ou guardar o PID.
