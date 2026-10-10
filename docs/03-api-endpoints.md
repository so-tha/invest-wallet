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
