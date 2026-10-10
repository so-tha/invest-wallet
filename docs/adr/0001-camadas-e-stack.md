# ADR 0001: Arquitetura em camadas com .NET 8 e PostgreSQL

**Status:** aceita (2026-10-10)

**Contexto:** a vaga pede C#/.NET ou Node, SOLID, camadas e SQL. Escopo é um CRUD de portfólio com regras de negócio.

**Decisão:** 4 projetos (Domain, Application, Infrastructure, Api), EF Core com PostgreSQL, controllers tradicionais.

**Alternativas:** Minimal APIs (mais enxuto, mas controllers são mais comuns em vagas); Node/TS (descartado: vaga lista C# primeiro e a usuária quer aprofundar .NET); SQL Server (Postgres roda mais leve em Docker/Linux).

**Consequências:** mais arquivos que um projeto único, em troca de testabilidade e clareza de responsabilidades.
