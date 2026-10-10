# Endpoints

| Método | Rota | Descrição | Sucesso | Erros |
|---|---|---|---|---|
| GET | /api/wallets | Lista carteiras (paginado) | 200 | |
| GET | /api/wallets/{id} | Detalhe | 200 | 404 |
| POST | /api/wallets | Cria | 201 | 400 |
| PUT | /api/wallets/{id} | Atualiza | 200 | 400, 404 |
| DELETE | /api/wallets/{id} | Remove | 204 | 404, 409 |
| GET | /api/assets | Lista ativos | 200 | |
| GET | /api/assets/{id} | Detalhe | 200 | 404 |
| POST | /api/assets | Cria | 201 | 400, 409 |
| PUT | /api/assets/{id} | Atualiza | 200 | 400, 404, 409 |
| DELETE | /api/assets/{id} | Remove | 204 | 404, 409 |
| GET | /api/wallets/{walletId}/transactions | Lista (filtros: assetId, from, to) | 200 | 404 |
| GET | /api/wallets/{walletId}/transactions/{id} | Detalhe | 200 | 404 |
| POST | /api/wallets/{walletId}/transactions | Registra compra/venda | 201 | 400, 404, 422 |
| DELETE | /api/wallets/{walletId}/transactions/{id} | Remove | 204 | 404, 422 |
| GET | /api/wallets/{walletId}/positions | Posição por ativo | 200 | 404 |

Convenções: 400 = entrada inválida; 404 = não encontrado; 409 = conflito de estado/unicidade; 422 = regra de negócio violada (ex.: vender mais do que possui).

## Notas da etapa 4
- `POST .../transactions`: `404` se a **carteira** não existe (está na URL); `422` se o **ativo** enviado no corpo não existe ou se a regra de saldo/data for violada.
- `GET .../positions` devolve só posições abertas (quantidade > 0), ordenadas por ticker.
- `executedAt` aceita ISO 8601 com fuso (ex.: `2026-01-10T12:00:00Z`) e é gravado em UTC.
