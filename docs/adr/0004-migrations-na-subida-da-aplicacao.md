# ADR 0004: Migrations aplicadas na subida, atrás de uma flag

**Status:** aceita (2026-10-10)

**Contexto:** o container da API precisa criar as tabelas num banco vazio para o `docker compose up` funcionar de ponta a ponta, e a imagem de runtime não tem o `dotnet ef`.

**Decisão:** com `Database:MigrateOnStartup=true` a API aplica as migrations pendentes antes de começar a atender (e registra o que aplicou). Desligada por padrão; o `docker-compose.yml` a liga.

**Alternativas:** *bundle* de migrations (`dotnet ef migrations bundle`) executado como passo de deploy ou container de inicialização (a opção mais correta em produção com várias réplicas); script SQL idempotente revisado por DBA; aplicar sempre, sem flag (perigoso: qualquer ambiente alteraria seu banco ao subir).

**Consequências:** simples e suficiente para uma instância (desenvolvimento, demonstração, CI). Com várias réplicas subindo ao mesmo tempo há risco de corrida entre migrations (o EF 8 não trava a execução); nesse cenário, migrar a um passo de deploy dedicado.
