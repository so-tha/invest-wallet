# Requisitos: InvestWallet

API para registrar carteiras de investimento, ativos e transações de compra/venda, calculando a posição atual e o preço médio.

## Funcionais
- RF1. CRUD de **Carteiras** (nome, descrição).
- RF2. CRUD de **Ativos** (ticker, nome, tipo: Ação, FII, ETF, RendaFixa, Cripto).
- RF3. Registrar, listar, consultar e remover **Transações** (compra/venda) de um ativo em uma carteira.
- RF4. Consultar a **posição** de uma carteira: por ativo, quantidade atual, preço médio e custo total.
- RF5. Listar com paginação e filtros (ex.: transações por ativo e período).

## Regras de negócio
- RN1. Quantidade e preço devem ser maiores que zero.
- RN2. Ticker é único, salvo em maiúsculas.
- RN3. Não é possível vender mais do que a quantidade em carteira na data da venda.
- RN4. Preço médio considera apenas compras; vendas reduzem a quantidade sem alterar o preço médio.
- RN5. Não é possível excluir ativo com transações, nem carteira com transações (409 Conflict).
- RN6. Remover/alterar uma transação não pode deixar a posição negativa em nenhum ponto da história.
- RN7. Data da transação não pode estar no futuro.

## Não funcionais
- RNF1. API REST com JSON, códigos HTTP corretos.
- RNF2. Erros em formato único (RFC 7807 ProblemDetails), sem vazar stack trace.
- RNF3. Logs estruturados, com ids de correlação.
- RNF4. Cobertura de testes nas regras de negócio e nos endpoints principais.
- RNF5. Documentação Swagger/OpenAPI.
- RNF6. Execução reproduzível com `docker compose up`.
- RNF7. Segredos fora do código.
