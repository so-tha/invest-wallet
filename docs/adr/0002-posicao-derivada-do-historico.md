# ADR 0002: Posição derivada do histórico, não armazenada

**Status:** aceita (2026-10-10)

**Contexto:** a carteira precisa informar quantidade e preço médio de cada ativo.

**Decisão:** não existe tabela de saldo. A posição é calculada a partir das transações por uma função pura
(`PositionCalculator`) a cada consulta e a cada nova transação.

**Alternativas:** coluna/tabela de saldo atualizada a cada operação (consulta O(1), mas há duas fontes de verdade que podem divergir por bug, falha parcial ou edição manual); eventos/CQRS (excesso para o escopo).

**Consequências:** o saldo nunca diverge do histórico e a regra é testável sem banco. O custo da consulta cresce com o número de transações do par (carteira, ativo); se medições mostrarem lentidão, evoluir para *snapshots* atualizados na mesma transação de banco.
