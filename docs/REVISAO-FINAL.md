# Revisão final: InvestWallet × vaga (CN Innovation, dev júnior full-stack)

Data: 2026-10-10. Este documento cruza o projeto com **cada** item da vaga, com a evidência de onde está atendido, e diz com franqueza o que **não** foi feito.

Legenda: ✅ atendido e verificado · 🟡 parcial ou preparado, mas não exercitado · ❌ não feito

## 1. Responsabilidades da vaga

| Item da vaga | Situação | Evidência / observação |
|---|---|---|
| Funcionalidades ponta a ponta, do banco à interface | 🟡 | Do banco à API: completo. **Não há interface** (frontend fica para a fase 2). |
| Backend C#/.NET | ✅ | Solução em 4 camadas, .NET 8. |
| Frontend Angular ou React | ❌ | Não iniciado. |
| Modelagem de dados | ✅ | [`02-modelo-de-dados.md`](02-modelo-de-dados.md), migrations, FKs, índices únicos, `CHECK`. |
| Contratos de API | ✅ | [`03-api-endpoints.md`](03-api-endpoints.md) e Swagger com 15 endpoints, validado por testes de contrato. |
| Divisão em módulos/serviços | ✅ | Camadas Domain / Application / Infrastructure / Api ([`04-arquitetura.md`](04-arquitetura.md)). |
| Registro de decisões (ADRs leves) | ✅ | 5 ADRs em [`adr/`](adr). |
| Código limpo e princípios SOLID | ✅ | S: controllers só traduzem HTTP, regras ficam em serviços/domínio. D: serviços dependem de interfaces de repositório. L/I/O: ver nota abaixo. |
| Code review e fluxo de pull request | 🟡 | Template de PR e CI **preparados**; mas todo o histórico foi commitado direto na `main`, **nenhum PR real foi aberto**. |
| Versionamento com Git | ✅ | 16 commits pequenos e descritivos (com este), sem segredos versionados. |
| Documentação técnica | ✅ | README, `docs/`, Swagger, ADRs, guia de estudo. |
| Testes unitários | ✅ | 74 testes (domínio, validadores, serviços com mock e fake). |
| Testes de integração | ✅ | 88 testes: API real + PostgreSQL descartável (Testcontainers), incluindo corridas reais de concorrência. |
| Testes E2E (Playwright) | ❌ | Exige interface; fase 2. |
| Esteira de CI/CD | 🟡 | CI escrito (build `-warnaserror`, testes, imagem, smoke test); **nunca rodou no GitHub** (sem repositório remoto ainda). CD (implantação) não implementado. |
| Logs, monitoramento, tratamento de erros | 🟡 | ✅ logs estruturados (Serilog/JSON), `traceId` ponta a ponta, tratador global de erros, health `live`/`ready`, **aviso de erros 500 via Sentry (opcional)**. ❌ Sem métricas nem tracing distribuído (OpenTelemetry/Prometheus). **Verificado com envio real** a um projeto Sentry de teste (o SDK confirmou o envio e o payload foi inspecionado); a conferência no painel é do dono do projeto. |
| Investigação de bugs em dev e produção | ✅ | Demonstrado: banco derrubado → 500 com `traceId` → 4 linhas no log com o mesmo id. |
| Integrar IA (LLM, extração de documentos; fallback, custo, latência) | ❌ | Etapa 10 deliberadamente não feita. |
| Pairing, refinamento com PM, demos | n/a | Não é verificável em código. O roteiro de apresentação está no [`ESTUDO.md`](ESTUDO.md). |

*Nota SOLID:* L (Liskov) e I (Interface Segregation) aparecem pouco neste escopo: não há hierarquias de herança, e as interfaces de repositório são pequenas e por agregado. O (Open/Closed) aparece de forma modesta, na adição de validadores e filtros do Swagger sem alterar código existente. Prefiro dizer isso a forçar uma narrativa de "apliquei os cinco".

## 2. Requisitos e qualificações

| Requisito | Situação | Evidência |
|---|---|---|
| Graduação ou portfólio comprovado | ✅ (portfólio) | Este repositório, **ainda local**: falta publicá-lo. |
| Lógica, estruturas de dados, POO | ✅ | `PositionCalculator` (algoritmo de preço médio com ordenação), entidades com invariantes. |
| HTTP/REST | ✅ | Verbos, status 200/201/204/400/404/409/422/500, paginação, `Location`, ProblemDetails. |
| Arquitetura em camadas | ✅ | Referências entre projetos impõem a direção das dependências. |
| Uma linguagem backend + um framework frontend | 🟡 | C#/.NET ✅; frontend ❌. |
| SQL e modelagem relacional básica | ✅ | Esquema normalizado, chaves, índices, restrições, transações, advisory lock. |
| Git e PR/code review | 🟡 | Git ✅; PR não exercitado (ver acima). |
| Testes automatizados básicos | ✅ | 162 testes, cobertura de linhas ~99% (medida antes dos últimos testes do limpador; ver nota). |
| Leitura de documentação técnica em inglês | n/a | Não verificável em repositório. |

## 3. Diferenciais

| Diferencial | Situação | Evidência / observação |
|---|---|---|
| Docker | ✅ | Dockerfile multi-stage, usuário não-root, Compose com healthchecks. |
| CI/CD (GitHub Actions) | 🟡 | Escrito e com comandos validados localmente; não executado no GitHub. |
| Nuvem (Azure) | ❌ | Sem implantação. |
| Playwright / Cypress | ❌ | Exige frontend. |
| APIs de LLM e prompts | ❌ | Etapa 10 não feita. |
| Mensageria / filas | ❌ | Não aplicável a este escopo; não implementado. |
| Cache | ❌ | Discutido (P14 do estudo: snapshots/cache), não implementado por ser desnecessário no volume atual. |
| Observabilidade | 🟡 | Logs + `traceId` + health + rastreamento de erros (Sentry, opcional); sem métricas e tracing. |
| Interesse em fintech | ✅ | O domínio do projeto: carteira, ativos, preço médio, precisão decimal. |
| Projeto no GitHub / portfólio publicado | ❌ | **Próximo passo imediato**: publicar o repositório. |

## 4. Verificações desta revisão

| Verificação | Resultado |
|---|---|
| Clone limpo → `dotnet build -c Release -warnaserror` | 0 warnings, 0 erros |
| Clone limpo → testes | 71 unitários + 47 de integração, todos passando (a suíte cresceu depois desta verificação; ver a linha da suíte final) |
| Clone limpo → roteiro do README (`docker compose up --build`) | API e banco `healthy`; exemplo do README devolve 201, posição correta (10 a 35,5) e 422 |
| Suíte final em Release com `-warnaserror` | 74 unitários + 88 de integração, todos passando |
| Cobertura de linhas (unitários + integração somados, sem código gerado: migrations e regex gerado) | **98,9%** de 703 linhas (Api 98,0 · Application 100 · Domain 98,0 · Infrastructure 99,3). Contando o regex gerado: 93,5% de 1003 linhas |
| Mutações manuais (quebrar de propósito e ver o teste falhar) | 6 execuções, todas detectadas: trava de concorrência, ordenação cronológica, tradução de erro de unicidade, documentação (2 alterações de uma vez), vazamento de mensagem no 500 e Sentry (limpador de segredos e filtro de exceção removidos de uma vez) |
| Segredos versionados | nenhum (`.env` ignorado; `.env.example` sem segredos reais) |

**Sobre a cobertura:** o número mostra o que foi *executado*, não o que seria *percebido* se estivesse errado. Por isso as mutações importam mais. As linhas sem cobertura são: cancelamento pelo cliente no tratador de erros, a falha `Fatal` de inicialização e o ramo `throw;` de erros de banco desconhecidos.

## 5. O que esta revisão encontrou e corrigiu

1. **`/health` não verificava o banco**: dizia "ok" mesmo com o banco fora do ar. Separado em *liveness* (`/health`) e *readiness* (`/health/ready`, com consulta ao banco), com testes e verificação real (banco parado → 503).
2. **O caminho do erro 500 não tinha teste automatizado**, e é onde mora a garantia de não vazar mensagens internas. Agora há teste, com mutação comprovando que ele detecta o vazamento.
3. **Migrations na subida, FK e unicidade eram verificadas só à mão**: agora têm teste.
4. **Documentos desatualizados**: README com contagem antiga de testes; `04-arquitetura.md` citava FluentAssertions (nunca usado); o `PROGRESSO.md` prometia branches e PRs que não existiram.

## 5b. Extensão pós-revisão: rastreamento de erros (Sentry)
Integração opcional, desligada sem DSN ([ADR 0006](adr/0006-rastreamento-de-erros-com-sentry.md)). O que os testes revelaram, contrariando suposições minhas: (1) o SDK **lança exceção na inicialização se o DSN for nulo**, o que derrubaria a API em desenvolvimento, CI e `docker compose`; (2) ele **captura até os 404/409/422** tratados pelo nosso handler (corrigido com filtros); (3) o meu limpador de segredos **deixava vazar tokens `Bearer`** (corrigido; visto falhando antes). **Verificado depois com envio real** (3 eventos de teste): o SDK confirmou `successfully sent` e a inspeção do payload real achou o nome da máquina e o usuário do sistema nos caminhos, que os testes simulados não cobriam; corrigido e reconfirmado (0 ocorrências). Fica um `user.id` anônimo de instalação.

## 6. Limitações conhecidas

- **Sem autenticação/autorização.** Esconder o Swagger não protege nada.
- **Sem HTTPS, CORS, rate limiting** configurados: esperado atrás de um proxy/ingress; necessários para uso real e para o frontend.
- **Senha do banco na connection string do container** (visível em `docker inspect`): aceitável em desenvolvimento, use segredos em produção.
- **Migrations na subida** servem a uma instância (ADR 0004).
- **Posição calculada na hora** (ADR 0002): correta e simples; sem snapshot, o custo cresce com o histórico.
- **Um erro de desserialização de JSON oculta os demais erros** até ser corrigido (limitação do pipeline do MVC).

## 7. Próximos passos, em ordem de valor para a candidatura

1. **Publicar no GitHub** e ver o CI rodar (corrigir o que ele mostrar). Esse é o maior ganho imediato: a vaga lista "projetos no GitHub" como diferencial.
2. **Trabalhar por branches e Pull Requests** daqui para a frente (o template já existe), para praticar o fluxo de code review que a vaga exige.
3. **Frontend em React** consumindo a API (gerando o cliente tipado a partir do `swagger.json`), com **testes E2E em Playwright**: fecha o "ponta a ponta" e dois diferenciais.
4. **Autenticação JWT** e dados por usuário.
5. **Etapa 10: importação de nota de corretagem via LLM**, com fallback para entrada manual, timeout e limite de custo, testada com um provedor simulado.
6. **Métricas e tracing** com OpenTelemetry.
7. **Implantação no Azure** (Container Apps ou App Service) com a imagem Docker já existente.
