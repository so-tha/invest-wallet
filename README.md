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
