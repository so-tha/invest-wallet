# Arquitetura

```
InvestWallet/
├── src/
│   ├── InvestWallet.Domain/          # Entidades, enums, regras puras (sem dependências)
│   ├── InvestWallet.Application/     # Serviços (casos de uso), DTOs, interfaces, validações
│   ├── InvestWallet.Infrastructure/  # EF Core, DbContext, repositórios, migrations
│   └── InvestWallet.Api/             # Controllers, Program.cs, middlewares, Swagger
├── tests/
│   ├── InvestWallet.UnitTests/       # Regras de domínio e serviços (com mocks)
│   └── InvestWallet.IntegrationTests/# API real + Postgres de teste
├── docs/ , docker-compose.yml , .github/workflows/
```

Fluxo: `Controller → Service (Application) → Repository (interface) → EF Core (Infrastructure) → PostgreSQL`.
Dependências apontam para dentro: Api → Application → Domain; Infrastructure → Application/Domain.

## Por quê
- **Camadas** separam responsabilidades: controller só traduz HTTP; regra de negócio fica no domínio/serviço, testável sem banco nem HTTP (S e D do SOLID).
- **Interfaces de repositório** na Application, implementadas na Infrastructure: inversão de dependência; nos testes unitários trocamos por mocks.
- **Sem CQRS/MediatR/DDD completo**: seria excesso para o escopo júnior; fica como evolução citada em entrevista.

## Stack
.NET 8, ASP.NET Core Web API (controllers), EF Core + Npgsql, FluentValidation, Serilog, Swashbuckle, xUnit + FluentAssertions + NSubstitute, Testcontainers (integração), Docker Compose, GitHub Actions.
