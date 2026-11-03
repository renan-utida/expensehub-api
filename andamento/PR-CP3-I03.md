# CP3-I03: RabbitMQ, worker e idempotência

## Issue implementada

Racass/checkpoint-csharpracass-expensehub#13 (CP3-I03, 20 pontos). A PR liga o fluxo real dos relatórios: o web grava o pedido, publica `{"reportId": "<guid>"}` no RabbitMQ e só responde `202` depois da confirmação do broker; um worker C# separado consome, calcula, grava e só então confirma o consumo; a proteção contra duplicata está no banco. As imagens e o Compose são da CP3-I05.

## Resumo técnico

- **Pacote novo, aprovado no plano:** `RabbitMQ.Client` 7.2.2 (`Apache-2.0 OR MPL-2.0`; traz `System.IO.Pipelines` e `System.Threading.RateLimiting`, MIT), só no `ExpenseHub.Api.csproj`. Nenhum outro pacote: o worker usa `Microsoft.NET.Sdk` e herda o framework da Api.
- **Fila e mensagem** (`ReportQueueTopology`, `ReportMessage`): fila `expensehub.reports`, durável, não exclusiva, sem exclusão automática, declarada pelo web e pelo worker a partir da mesma classe. Mensagem `{"reportId":"<guid>"}`, `application/json`, persistente. Qualquer outro campo recebido é ignorado.
- **Publicador** (`RabbitMqReportPublisher`): conexão aberta na primeira publicação (o web inicia com o broker fora do ar), canal com confirmação de publicação, `mandatory` e espera de 5 s. Mensagem recusada, devolvida, sem confirmação a tempo ou broker fora: o `POST` responde `503` com o `id` e o pedido fica `Pending`; nunca finge `202`. Registrado por `AddExpenseHubMessaging`, que vence o `UnavailableReportPublisher` em qualquer ordem de registro.
- **Configuração** (`RabbitMqOptions`, seção `RabbitMq`): `Host`, `Port`, `VirtualHost`, `QueueName` e dois tempos. `UserName` e `Password` não têm padrão e nunca ficam em arquivo versionado (variáveis de ambiente ou user-secrets). A validação lista só os nomes dos campos.
- **Worker** (`sources/ExpenseHub.Worker`): host e adaptador do RabbitMQ (confirmação manual, prefetch 1, reconexão automática). Sem seed do Admin, controllers nem autenticação. Entra em `ExpenseHub.slnx`. A regra toda fica na Api, e nada precisou de `InternalsVisibleTo`.
- **Processador testável** (`ReportMessageProcessor`): devolve `Completed`, `Duplicate`, `PermanentFailure`, `Discarded` ou `TransientFailure`; `ReportDelivery` decide a confirmação e o consumidor só executa. Lê período, escopo e solicitante do pedido gravado (`ReportQuery.From`), calcula com a `ReportCalculator` e grava `Completed` e o resultado numa só gravação (`IReportProcessingStore`, transições condicionais sobre o estado, que já é *concurrency token*).
- **Idempotência no banco**: pedido `Completed` ou `Failed` é duplicata, sem leitura de despesas nem gravação; sobrevive a reinício. `Processing` é retomado sem regravar o início; se outro worker concluir primeiro, a gravação final não se aplica e vira duplicata.
- **Falha definitiva**: `Failed` com `InvalidRequest` ou `ProcessingFailed` e mensagem fixa (nunca texto de exceção); a mensagem é confirmada e não volta. Se gravar a falha também falhar, nada é confirmado.
- **Falha transitória**: banco ocupado, trancado ou inacessível (`SqliteTransientErrors`) não confirma; o worker espera 5 s e devolve a mensagem, sem limite de tentativas e sem laço quente. Nunca vira `Failed`.
- **SQLite compartilhado** (`AddExpenseHubPersistence`, usado pelos dois processos): `PRAGMA busy_timeout = 5000` e, **depois dele**, `PRAGMA journal_mode = WAL` em cada conexão, mais `Default Timeout` de 5 s. Web e worker precisam apontar para o mesmo arquivo, por caminho absoluto na connection string (variável de ambiente); o worker avisa quando o caminho é relativo.
- **Logs** por `reportId` em todas as etapas, sem e-mail, token, senha, connection string nem texto de exceção.
- **Documentação**: README (seção "Mensageria e worker", com a fila, a tabela de configuração, o `docker run` com volume nomeado e onde o broker guarda os dados, a janela sem Outbox, a estrutura e a solução de problemas), seção da CP3-I03 e decisões no `CHECKPOINT-3.md`, `.http` com as frases do `202` e do `503`, e a CP3-I02 marcada como concluída (PR #13, merge `6e47693`, workflow `code-quality` 100/100 na execução #72).

## Decisões e concessões

- **Janela sem Transactional Outbox, documentada**: salvar e publicar são operações separadas. Uma queda entre as duas deixa o pedido `Pending` sem mensagem, e reiniciar o worker não a recria. Um `503` com `id` também não garante que a mensagem não chegou ao broker (a confirmação pode ter se perdido); nesse caso o worker ainda conclui, e quem recebe `503` consulta o `id` retornado antes de pedir outro. Outbox, retentativas completas e fila de mensagens não processáveis não foram feitos.
- **Mensagem para pedido inexistente é recusada sem voltar** (não há o que marcar). Sem fila de mensagens não processáveis ela some; a causa provável é web e worker em bancos diferentes, por isso o worker avisa quando o caminho do banco é relativo.
- A Api é um projeto web, então o worker herda o framework `Microsoft.AspNetCore.App`; a imagem do worker (CP3-I05) usa a base `aspnet`.
- Nada muda no CP2: autenticação, autorização das despesas e pagamento seguem como estavam. Uma única mudança de persistência vale para o web: o modo WAL e a espera por bloqueio.

## Como validar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

Para o fluxo real, siga o README (seção "Mensageria e worker"): suba o RabbitMQ com `docker run` e volume nomeado, defina `ConnectionStrings__ExpenseHub` (caminho absoluto, igual nos dois), `RabbitMq__UserName` e `RabbitMq__Password`, e rode o web e o worker. Depois de `dotnet ef database update` (a CP3-I03 não tem migration nova), um `POST /api/reports` com um `Employee` responde `202` e o relatório chega a `Completed`.

## Evidências relevantes

- Build com 0 avisos e 0 erros nos três projetos. `dotnet test`: **1086 aprovados** (936 da base e 150 novos), 0 falhas.
- Testes novos, sem banco, rede nem RabbitMQ: mensagem 26, fila e mensagem persistente 8, opções 17, registro do publicador 9, processador 33, logs 9, decisão de confirmação 15, erros transitórios do SQLite 25, comandos de WAL e espera 8.
- Mutações feitas e desfeitas: devolver "concluído" quando a gravação falha quebrou 10 testes; remover a checagem inicial de duplicata quebrou 5.
- SQLite real (spike descartável): `Pending`, `Processing`, `Completed` (resultado na mesma linha) e `Failed`; 8 workers disputando uma conclusão, só 1 aplica; o exemplo da apostila pelo processador sobre o repositório real (`Mine` 3 e 175,00, `Global` 4 e 375,00); repetir a mensagem não muda a linha; WAL ativo; uma gravação com o banco trancado espera cerca de 5 s e vira transitória; arquivo que não abre vira transitório. Dois processos abrindo juntos um banco ainda sem WAL: 0 erros nas duas ordens dos comandos.
- RabbitMQ 4.4.0 em contêiner, web e worker locais, SQLite temporário, credenciais geradas na hora: 65 conferências aprovadas.
  - Fluxo normal: `202` com `Location`, `Completed` com 3 e 175,00 (`Mine`) e 4 e 375,00 (`Global`), Excel válido, fila vazia.
  - Worker parado: `202`, `Pending`, mensagem pronta e persistente só com o id; ao ligar o worker o mesmo id conclui.
  - Duplicata depois de reiniciar o worker: a linha do banco fica idêntica byte a byte e o log mostra a duplicata.
  - `Processing` interrompido por `kill` com a mensagem sem confirmação (`0 1 1`), devolvida pelo broker, retomada e concluída com o `StartedAt` da primeira tentativa.
  - Falha definitiva: `Failed` com `InvalidRequest`, confirmada uma só vez; mensagem ilegível e id inexistente recusados sem voltar.
  - Falha transitória: banco trancado por 16 s, mensagem nunca confirmada, nova tentativa com o `reportId` e conclusão depois do desbloqueio.
  - Broker parado: `503` com `id`, `Pending`; o worker reconecta sozinho e o relatório do `503` continua `Pending` (a janela sem Outbox, mostrada).
  - Logs dos dois processos sem credencial do broker, senha, `Bearer` nem e-mail.
- Script de qualidade (`-SkipGitleaks`): **100/100**, 20 em cada categoria, sem bloqueantes. Achado informativo `FIAP0002` (Gitleaks ignorado; roda no CI).

## Impactos em segurança e autorização

- O solicitante e o escopo vêm do pedido gravado, nunca da mensagem: a mensagem leva só o `reportId`, e qualquer outro campo é ignorado (testado). O worker nunca relê as roles, e a autorização da CP3-I02 não muda.
- Nenhuma credencial do RabbitMQ está em arquivo versionado: usuário e senha vêm de variáveis de ambiente ou de user-secrets, a validação não repete valores e os logs não os trazem.
- O `error.message` de um relatório `Failed` é texto fixo, sem caminho, connection string nem texto de exceção.
- Nenhum comportamento do CP2 (autenticação, autorização, pagamento) foi alterado.

## Checklist

- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
