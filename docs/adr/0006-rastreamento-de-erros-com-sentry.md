# ADR 0006: Rastreamento de erros com Sentry (opcional), em vez de integração própria com Jira

**Status:** aceita (2026-10-10)

**Contexto:** logs só ajudam quando alguém os procura; um erro 500 precisa **avisar** quem opera o sistema. A primeira ideia foi criar automaticamente um card no Jira a cada erro.

**Decisão:** adotar o SDK do Sentry (`Sentry.AspNetCore`), **desligado por padrão** (ativado só se `Sentry__Dsn` estiver configurado). Regras:
- só erros **500** vão para o Sentry; as exceções esperadas (404, 409, 400, 422) são filtradas;
- `SendDefaultPii=false`, sem corpo de requisição, sem performance nem sessões;
- um limpador de segredos (`SentryEventScrubber`) roda **antes** do envio;
- o `traceId` vai como tag (`trace_id`), ligando o erro no Sentry à linha do log;
- o DSN vem de variável de ambiente, nunca do repositório.

**Alternativas:**
- *Adaptador próprio para Jira/GitHub Issues* (fila em memória, deduplicação por impressão digital, resiliência): mais controle e bom exercício, mas é reconstruir o que o Sentry já faz (agrupamento, deduplicação, alertas). Registrado como possível extensão.
- *Integração Sentry → Jira:* segundo a página do Sentry, a sincronização com o Jira é do plano **Team** em diante, portanto **não é gratuita**; não adotada.

**Consequências:**
- Stack traces e mensagens de exceção **saem do sistema** para um serviço externo; por isso o limpador de segredos e a regra de não colocar segredos em mensagens de exceção.
- O plano gratuito (Developer) é limitado (cerca de 5.000 erros/mês e um usuário, segundo fontes de terceiros; conferir na página oficial).
- Dois comportamentos do SDK só foram descobertos por teste e estão tratados: ele **lança exceção na inicialização se o DSN for nulo** (só se desativa com string vazia explícita) e **captura até as exceções que o nosso tratador converte em 4xx** (por isso os filtros).
- **Verificado com envio real** (3 eventos de teste, `environment=development`): o SDK confirmou o envio (`HttpTransport: Envelope ... successfully sent`) e o payload real foi inspecionado. A inspeção achou o que os testes simulados não cobriam: o nome da máquina (`server_name`, `request.env.SERVER_NAME`) e o usuário do sistema nos caminhos (stack frames e `debug_meta`), mesmo com `SendDefaultPii=false`. Corrigido no limpador e confirmado de novo no payload real (0 ocorrências). Ainda sai um `user.id` anônimo de instalação. A chegada no painel do Sentry quem confirma é o dono do projeto.
