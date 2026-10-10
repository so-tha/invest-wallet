## O que muda e por quê
<!-- Contexto em 2-3 linhas: qual problema resolve ou qual requisito atende. -->

## Como testar
<!-- Comandos ou passos. Ex.: `dotnet test`, ou as requisições de exemplo do Swagger. -->

## Checklist
- [ ] `dotnet build -warnaserror` e `dotnet test` passam localmente
- [ ] Regras novas têm testes (unitários e, se tocam banco/HTTP, de integração)
- [ ] Endpoint novo ou alterado: Swagger atualizado (resumo, exemplos, respostas de erro)
- [ ] Mudança de esquema: migration criada e revisada
- [ ] Decisão de arquitetura relevante: ADR em `docs/adr/`
- [ ] Nada sensível (senhas, tokens, dados pessoais) em código ou logs
