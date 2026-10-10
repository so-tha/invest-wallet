# Modelo de dados

```mermaid
erDiagram
    WALLET ||--o{ TRANSACTION : possui
    ASSET  ||--o{ TRANSACTION : referenciado_em

    WALLET {
        uuid id PK
        varchar name "NOT NULL, max 100"
        varchar description "max 300"
        timestamptz created_at
    }
    ASSET {
        uuid id PK
        varchar ticker UK "NOT NULL, maiúsculo"
        varchar name "NOT NULL, max 150"
        int type "enum AssetType"
        timestamptz created_at
    }
    TRANSACTION {
        uuid id PK
        uuid wallet_id FK
        uuid asset_id FK
        int type "Buy | Sell"
        numeric quantity "> 0"
        numeric unit_price "> 0"
        timestamptz executed_at
        timestamptz created_at
    }
```

**Posição** não é tabela: é calculada a partir das transações (fonte única da verdade, sem risco de saldo dessincronizado).

Índices: `assets(ticker)` único; `transactions(wallet_id, asset_id, executed_at)` para consultas de posição.
Chaves: UUID gerado pela aplicação. `numeric(18,6)` para quantidade e preço (nunca float para dinheiro).
