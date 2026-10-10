# ADR 0005: Convenção de códigos de erro HTTP

**Status:** aceita (2026-10-10)

**Decisão:**
- **400:** a requisição pode ser julgada inválida olhando só para ela (e para o relógio): campo ausente, formato, limite, data futura. Validada na borda (FluentValidation) e devolve todos os erros de uma vez, em `errors`.
- **404:** o recurso identificado na URL não existe.
- **409:** a requisição é válida, mas conflita com o estado atual (ticker duplicado, excluir algo com dependentes). Inclui o caso em que o índice único do banco decide uma corrida.
- **422:** a requisição é válida, mas viola uma regra de negócio que depende de estado (vender mais do que possui) ou referencia, no corpo, algo que não existe (ativo).
- **500:** erro inesperado; mensagem genérica, detalhe só no log; sempre com `traceId`.

Todos os erros usam `application/problem+json` (RFC 7807).

**Consequências:** o cliente distingue "corrija os dados" (400), "o estado impede" (409) e "o negócio não permite" (422). A mesma regra, verificada em camadas (borda → domínio → banco), nunca é a única defesa.
