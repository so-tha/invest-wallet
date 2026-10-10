# InvestWallet

API REST de carteira de investimentos (.NET 8 + PostgreSQL). Projeto de portfólio.
Documentação de planejamento em [`docs/`](docs/). Acompanhamento em [`PROGRESSO.md`](PROGRESSO.md).

## Rodando localmente (parcial, será completado na etapa 9)
```bash
cp .env.example .env          # ajuste a senha
docker compose up -d db       # sobe o PostgreSQL
set -a; source .env; set +a   # exporta as variáveis para o shell
dotnet tool restore           # instala o dotnet-ef (uma vez)
dotnet ef database update --project src/InvestWallet.Infrastructure --startup-project src/InvestWallet.Api
dotnet run --project src/InvestWallet.Api
```
Health check: `GET /health`.

## Logs
- **Development:** texto legível no console, com o `TraceId` em cada linha. Para ver o SQL do EF, defina em `appsettings.Development.json`: `"Serilog": { "MinimumLevel": { "Override": { "Microsoft.EntityFrameworkCore.Database.Command": "Information" } } }`.
- **Outros ambientes:** JSON, um evento por linha, pronto para ser coletado.
- Toda resposta de erro traz um `traceId`; filtre o log por ele para ver tudo o que aconteceu naquela requisição.
- Nunca são registrados corpos de requisição nem valores de parâmetros SQL.
