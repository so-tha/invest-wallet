# ADR 0003: Serializar operações por (carteira, ativo) com advisory lock do PostgreSQL

**Status:** aceita (2026-10-10)

**Contexto:** validar "não vender mais do que possui" é *ler o histórico e depois gravar*. Duas vendas simultâneas leem o mesmo saldo, ambas passam e juntas o deixam negativo. Reproduzido: 6 vendas de 10 sobre saldo 20 passaram todas (saldo final −40).

**Decisão:** ler, validar e gravar dentro de uma transação de banco que antes adquire `pg_advisory_xact_lock` com chave derivada de (carteira, ativo). O lock é liberado no commit/rollback.

**Alternativas:** `SELECT ... FOR UPDATE` numa linha (exigiria uma linha-pai por par); isolamento `Serializable` com retry (mais genérico, mas exige lógica de repetição); concorrência otimista com coluna de versão (idem); constraint no banco (não expressa "saldo acumulado ≥ 0").

**Consequências:** operações do mesmo par são serializadas; pares diferentes não se bloqueiam. Acopla a solução ao PostgreSQL. Coberto por teste de integração com requisições paralelas (e verificado por mutação: sem o lock, o teste falha).
