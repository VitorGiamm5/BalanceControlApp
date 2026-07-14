# Evaluation rubric

Esta checklist lista itens que um avaliador humano provavelmente julgaria em um teste tecnico de API para controle de saldos com concorrencia, idempotencia e volume.

Use este documento como roteiro de revisao antes da entrega e como base para simular a avaliacao a partir de um clone limpo.

## 1. Primeira impressao do repositorio

- O repositorio nao usa nome da empresa avaliadora.
- O projeto abre com uma estrutura simples e compreensivel.
- O README responde rapidamente: o que e, como executar, quais endpoints existem e como testar.
- Nao ha dominios ou artefatos de outro projeto que confundam a avaliacao.
- Nao ha tela, BFF, MCP, Orders ou Products na entrega final.
- Nao ha arquivos temporarios de build/teste versionados por engano.
- A stack local tem nome claro: `BalanceControl-local`.

## 2. Execucao do zero

- `git clone` e `cd` estao documentados.
- Pre-requisitos estao documentados: Docker, .NET SDK, PowerShell, Git.
- O comando principal de subida funciona sem conhecimento previo.
- A API sobe com PostgreSQL e aplica migrations automaticamente.
- O Swagger abre sem passos ocultos.
- O avaliador consegue gerar token JWT e autorizar chamadas no Swagger.
- O health check responde `Healthy`.
- Existe smoke test simples.
- Existe caminho claro para parar e resetar a stack.
- Credenciais locais de PostgreSQL, Grafana e demais ferramentas estao documentadas.

## 3. Cobertura dos requisitos funcionais

- Existe endpoint para alterar saldo.
- Alteracao positiva credita saldo.
- Alteracao negativa debita saldo.
- Usuario inexistente e inicializado na primeira alteracao.
- Existe endpoint para consultar saldo atual.
- Existe endpoint para consultar extrato.
- Extrato retorna operacoes do usuario.
- Extrato e paginado.
- Consulta de usuario inexistente tem comportamento definido e documentado.
- Nao ha regra indevida de bloquear saldo negativo, ja que o enunciado nao exige.

## 4. Idempotencia e reprocessamento

- A identidade da operacao esta documentada.
- A chave idempotente e duravel: `(userId, operationId)`.
- Replay exato nao reaplica saldo.
- Replay exato retorna `applied = false`.
- Replay exato preserva o movimento original.
- Mesmo `userId` e `operationId` com payload diferente retorna `409 Conflict`.
- A comparacao de payload usa hash canonico, nao comparacao fragil de texto bruto.
- A garantia funciona com multiplas instancias porque depende do banco, nao de memoria local.
- Existe roteiro manual para validar idempotencia.
- Existe script automatizado para validar idempotencia: `scripts/idempotency.ps1`.

## 5. Concorrencia e consistencia

- Atualizacao de saldo e transacional.
- Atualizacao concorrente do mesmo usuario nao perde incremento.
- Banco serializa alteracoes do mesmo usuario por linha/constraint.
- Usuarios diferentes podem ser processados em paralelo.
- Replay concorrente aplica no maximo uma vez.
- Existem testes de concorrencia no repository/integracao.
- Existem scripts para stress/spike.
- O saldo final e conferido por assert, nao apenas por ausencia de erro.
- O total de movimentos no extrato e conferido.
- Logs ou evidencias ajudam a investigar falhas sob carga.

## 6. Modelagem e persistencia

- PostgreSQL e a fonte da verdade.
- Saldo atual fica materializado em tabela propria.
- Movimentacoes ficam em ledger separado.
- Existe constraint unica para `(user_id, operation_id)`.
- Existem indices coerentes para consulta de extrato.
- Tipos monetarios usam `decimal` no C# e `numeric(18,2)` no PostgreSQL.
- Limites de valor e escala estao validados ou documentados.
- O modelo evita recomputar saldo pela soma completa do ledger a cada consulta.
- A estrategia para grandes volumes esta documentada.
- A evolucao natural para particionamento/retencao esta descrita.

## 7. Contrato HTTP e Swagger

- Swagger mostra endpoints de sucesso e falha.
- Endpoints protegidos indicam Bearer JWT.
- Endpoint de token nao exige Bearer.
- Payloads estao documentados campo a campo.
- Campos obrigatorios e opcionais estao claros.
- Exemplos sao validos e podem ser colados no Swagger.
- `amount` usa ponto decimal em JSON.
- `occurredAt` usa `date-time` ISO 8601 realmente aceito pela API.
- Erros relevantes retornam status adequado: `400`, `401`, `404`, `409`, `422`, `500`.
- O envelope de resposta e consistente.

## 8. Validacoes

- `userId` e obrigatorio, nao vazio e possui limite.
- `operationId` e obrigatorio e nao pode ser `Guid.Empty`.
- `amount` nao aceita zero.
- `amount` aceita credito e debito.
- `amount` limita escala a 2 casas decimais.
- `amount` respeita a precisao persistida em `numeric(18,2)`.
- `description` possui limite.
- JSON invalido retorna `400`.
- Payload de negocio invalido retorna `422`.
- Testes cobrem casos invalidos principais.

## 9. Autenticacao

- JWT simples esta implementado.
- O fluxo local de token esta documentado.
- Credenciais locais estao documentadas.
- Endpoints de saldo exigem token.
- Teste cobre chamada sem Bearer retornando `401`.
- Documentacao deixa claro que credenciais em Compose sao apenas para ambiente local.

## 10. Observabilidade

- API produz logs estruturados.
- Logs sao visiveis no Seq.
- E possivel filtrar requisicoes por periodo.
- E possivel filtrar falhas por status HTTP.
- Eventos de request incluem rota, metodo, status, latencia e trace id.
- API expoe `/metrics`.
- Prometheus coleta metricas da API.
- Grafana tem dashboard provisionado.
- Documentacao explica URLs e credenciais locais.
- Existe caminho para capturar evidencias de logs/metricas apos smoke, stress ou spike.

## 11. Testes e qualidade da evidencia

- Testes unitarios cobrem regras de negocio e validadores.
- Testes de integracao exercitam PostgreSQL real ou Testcontainers.
- Testes funcionais exercitam HTTP real.
- Smoke cobre fluxo principal.
- Existe teste de replay/idempotencia.
- Existe teste de conflito idempotente.
- Existe teste de concorrencia.
- Existe gate de cobertura minimo.
- A cobertura do nucleo do exercicio fica acima de 80%.
- Scripts de teste falham com erro claro quando uma assertiva quebra.

## 12. Codigo e arquitetura

- Separacao entre API, Application, Domain, Infrastructure e Observability esta coerente.
- Domain nao depende de Infrastructure/API.
- Regras de negocio ficam fora do controller.
- Controller e fino e delega para service.
- Repository concentra persistencia e transacao.
- Nomes de classes e metodos comunicam intencao.
- Nao ha abstracoes antigas ou orfas confundindo a entrega.
- Nao ha logica duplicada desnecessaria.
- Excecoes e status HTTP sao tratados de forma previsivel.
- Cancelamento/timeout nao deixa request pendurada.

## 13. Infraestrutura local

- Docker Compose sobe tudo que o projeto precisa.
- Nomes de containers, rede e volumes sao previsiveis.
- PostgreSQL tem healthcheck.
- API depende do banco saudavel.
- Seq, Prometheus e Grafana sobem junto da stack.
- Portas locais estao documentadas.
- Reset de dados esta documentado.
- `.dockerignore` evita contexto de build desnecessario.

## 14. Diferenciais positivos

- JWT simples para simular consumo real.
- Observabilidade completa local.
- Script de spike com tres contas ultra movimentadas.
- Script explicito de idempotencia.
- Documentacao de payload campo a campo.
- How to run do clone ao login nas ferramentas.
- How to use com payloads prontos.
- Evidencias de stress/spike salvam requests e logs.
- README explica trade-offs, nao apenas comandos.

## 15. Roteiro de simulacao humana do zero

Execute esta simulacao como se o avaliador tivesse acabado de clonar o repositorio:

1. Ler apenas o `README.md`.
2. Ver se o README aponta para `docs/how-to-run.md` e `docs/how-to-use.md`.
3. Subir a stack com o comando documentado.
4. Conferir `docker compose ps`.
5. Abrir `http://localhost:9005/health`.
6. Abrir `http://localhost:9005/swagger`.
7. Gerar JWT pelo Swagger.
8. Autorizar Bearer no Swagger.
9. Rodar payload de credito.
10. Repetir o mesmo payload e validar `applied = false`.
11. Reusar `operationId` com payload diferente e validar `409`.
12. Rodar payload de debito.
13. Consultar saldo.
14. Consultar extrato.
15. Abrir Seq e filtrar requisicoes do periodo.
16. Abrir Prometheus e conferir target `UP`.
17. Abrir Grafana e conferir dashboard.
18. Conectar no PostgreSQL e consultar tabelas principais.
19. Rodar `scripts/smoke.ps1`.
20. Rodar `scripts/idempotency.ps1`.
21. Rodar `scripts/test.ps1`.
22. Rodar `scripts/coverage.ps1`.
23. Revisar o codigo procurando regras no lugar errado, acoplamento, residuos e inconsistencias.
24. Emitir relatorio com achados, riscos, evidencias e recomendacoes.
