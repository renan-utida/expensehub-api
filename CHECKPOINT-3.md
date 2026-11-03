# Checkpoint 3: ExpenseHub em operação

Registro do Checkpoint 3 (CP3) do ExpenseHub, feito por Renan Dias Utida (RM 558540). Prazo: 04/11/2026, às 23:59, no Teams. A nota-base é coletiva. O estado vivo do sistema (como rodar, endpoints, banco, testes e qualidade) fica no [README](README.md); este arquivo guarda o detalhe de cada issue, as decisões e as evidências, do mesmo modo que o [CHECKPOINT-2.md](CHECKPOINT-2.md) faz para o Checkpoint 2. Cada seção é preenchida quando a issue termina.

## Sumário

- [Ponto de partida](#ponto-de-partida)
- [Decisões do CP3](#decisões-do-cp3)
- [Fora de escopo do CP3](#fora-de-escopo-do-cp3)
- [Fluxo de trabalho do CP3](#fluxo-de-trabalho-do-cp3)
- [Detalhe da CP3-I01: Contrato e resultado do relatório](#detalhe-da-cp3-i01-contrato-e-resultado-do-relatório)
- [Detalhe da CP3-I02: Autorização e isolamento](#detalhe-da-cp3-i02-autorização-e-isolamento)
- [Detalhe da CP3-I03: RabbitMQ, worker e idempotência](#detalhe-da-cp3-i03-rabbitmq-worker-e-idempotência)
- [Detalhe da CP3-I04: Testes próprios significativos](#detalhe-da-cp3-i04-testes-próprios-significativos)
- [Detalhe da CP3-I05: Publicação e execução reproduzível](#detalhe-da-cp3-i05-publicação-e-execução-reproduzível)
- [Detalhe da CP3-I06: Experimentos e diagnóstico](#detalhe-da-cp3-i06-experimentos-e-diagnóstico)
- [Detalhe da CP3-I07: Qualidade objetiva das alterações](#detalhe-da-cp3-i07-qualidade-objetiva-das-alterações)
- [Detalhe da CP3-EXTRA-LINKEDIN: Publicação individual no LinkedIn](#detalhe-da-cp3-extra-linkedin-publicação-individual-no-linkedin)
- [Dossiê dos experimentos (E01 a E05)](#dossiê-dos-experimentos-e01-a-e05)
- [Entrega](#entrega)

## Ponto de partida

| Item | Valor |
|---|---|
| Commit do CP2 | `2e74619a311b38dbcc959888c8f1358b6877e9a1` (merge da PR 10 na `main`) |
| Testes unitários no fim do CP2 | 611 aprovados |
| Build conferido em 02/11/2026 | 0 avisos e 0 erros (SDK .NET 10.0.403) |
| Score do workflow `code-quality` no CP2 | 100/100 (registrado na PR 10) |
| Branch da preparação | `cp3-00-preparacao` |

## Decisões do CP3

| Data | Tema | Decisão | Issue |
|---|---|---|---|
| 02/11/2026 | Estrutura de projetos | O worker referencia `ExpenseHub.Api`. A lógica de relatório fica na Api e o worker é fino (host e adaptador do RabbitMQ). Ele não registra o seed do Admin, os controllers nem a autenticação | CP3-I03 |
| 02/11/2026 | Biblioteca de Excel | `DocumentFormat.OpenXml` 3.5.1 (MIT), com `DocumentFormat.OpenXml.Framework` 3.5.1 e `System.IO.Packaging` 10.0.2 (ambos MIT), conferidos no NuGet | CP3-I01 |
| 02/11/2026 | POST sem publicador real | Responde `503` até existir o publicador do RabbitMQ; nunca `202` sem confirmação. Os testes usam um fake do publicador | CP3-I01 |
| 02/11/2026 | Identificador | `Guid`, como `Expense.Id`, igual nas rotas, na mensagem e nas respostas | CP3-I01 |
| 02/11/2026 | Versão das imagens | O workflow publica a cada push na `main`, com tag igual ao SHA completo; o commit final gera as suas próprias imagens | CP3-I05 |
| 02/11/2026 | Arquivo `.http` | Nasce na CP3-I01 e é completado na CP3-I06; as credenciais vêm de um arquivo de ambiente ignorado pelo git | CP3-I01 e CP3-I06 |
| 02/11/2026 | Migrações no Compose | Decidido em 03/11/2026 no plano da CP3-I05: uma chave `Database:MigrateOnStartup`, desligada por padrão e ligada só no Compose, faz o web aplicar as migrations pendentes antes do seed do Admin. Só duas imagens publicadas, nenhum dado apagado, e o comportamento do CP2 (`dotnet ef database update`) não muda | CP3-I05 |
| 02/11/2026 | Resultado salvo | Uma coluna `ResultJson` na linha do pedido, com centavos inteiros; `Completed` e resultado numa só gravação | CP3-I01 |
| 02/11/2026 | Cálculo | `ReportCalculator.Select` (consulta com o período inclusivo e o escopo) e `Summarize` (centavos); projeção estreita no banco, sem agrupar no SQL | CP3-I01 |
| 02/11/2026 | Entrada e respostas | `scope` só pelo nome exato; `503` com `id` quando o broker não confirma (continua `Pending`) e `503` sem `id` quando o banco falha | CP3-I01 |
| 02/11/2026 | Autorização da CP3-I01 | Só autenticação e dono; a regra de escopo por papel fica para a CP3-I02 | CP3-I01 |
| 02/11/2026 | Matriz de role por escopo | Classe pura `ReportAccess`: `Mine` exige `Employee`, `Global` exige `Auditor`, quem tem as duas pede os dois, e Admin, Approver e Finance não somam nada; os nomes das roles são comparados exatamente | CP3-I02 |
| 02/11/2026 | Ordem das respostas do `POST` | `401`, `403` por role (atributo da rota, antes do corpo), `400`, `403` por escopo, depois `202` ou `503`; o serviço repete a ordem sozinho, sem depender do atributo | CP3-I02 |
| 02/11/2026 | Leitura sem role | Listar, detalhar e baixar o Excel exigem só autenticação e posse, mesmo para o Auditor; relatório alheio e inexistente dão o mesmo `404` | CP3-I02 |
| 02/11/2026 | Consulta do worker | `ReportQuery.From` monta a consulta só do pedido gravado (solicitante, escopo e período); a mensagem leva apenas o `reportId`, e o worker nunca relê as roles | CP3-I02 e CP3-I03 |
| 03/11/2026 | Cliente do RabbitMQ | `RabbitMQ.Client` 7.2.2 (Apache-2.0 OR MPL-2.0; dependências `System.IO.Pipelines` 8.0.0 e `System.Threading.RateLimiting` 8.0.0, MIT), só no `ExpenseHub.Api.csproj`; o worker e os testes o recebem por referência. O worker usa `Microsoft.NET.Sdk` e herda o framework da Api, sem outro pacote | CP3-I03 |
| 03/11/2026 | Fila e mensagem | Fila `expensehub.reports` durável, não exclusiva e sem exclusão automática; mensagem `{"reportId":"<guid>"}` persistente; publicação com confirmação e `mandatory`; consumo com confirmação manual e prefetch 1. Web e worker declaram a fila a partir da mesma classe (`ReportQueueTopology`) | CP3-I03 |
| 03/11/2026 | Idempotência no banco | A proteção contra duplicata é o estado gravado, não a memória do worker: `Completed` e `Failed` viram duplicata sem leitura nem gravação; as transições `Pending` para `Processing` e `Processing` para `Completed` são condicionais (o estado é *concurrency token*) e `Completed` e o resultado são gravados juntos | CP3-I03 |
| 03/11/2026 | Falha transitória | Banco ocupado, trancado ou inacessível não confirma a mensagem: o worker espera `TransientRetryDelaySeconds` (5 s) e devolve a mensagem, sem limite de tentativas e sem laço quente; nunca vira `Failed`. Falha definitiva grava `Failed` com código estável e mensagem fixa e não volta | CP3-I03 |
| 03/11/2026 | Retomada de `Processing` | Uma mensagem entregue de novo para um pedido `Processing` retoma: recalcula e tenta concluir, sem regravar o início; a gravação final só vale se o estado ainda for `Processing` | CP3-I03 |
| 03/11/2026 | SQLite compartilhado | `PRAGMA busy_timeout` e depois `PRAGMA journal_mode = WAL` em cada conexão, mais `Default Timeout` de 5 s, no mesmo método de registro usado pelo web e pelo worker; os dois apontam para o mesmo arquivo, por caminho absoluto em variável de ambiente | CP3-I03 |
| 03/11/2026 | Janela sem Outbox | Sem Transactional Outbox: uma queda entre gravar e publicar deixa `Pending` sem mensagem, e um `503` com `id` não garante que a mensagem não chegou ao broker. Quem recebe `503` consulta o `id` retornado | CP3-I03 |
| 03/11/2026 | Inicialização do banco no Compose | Flag em vez de bundle do EF: zero serviços novos, sem `dotnet-ef` no build, sem executável autocontido por arquitetura dentro da imagem web. `MigrateAsync` só aplica o que falta e não apaga nada. O worker nunca migra e só sobe depois que o web está saudável, porque o `/health` só responde depois das migrations e do seed (o servidor é o último a iniciar). Custo: com a flag ligada o processo web tem o poder de alterar o esquema | CP3-I05 |
| 03/11/2026 | Nomes e versão das imagens | `ghcr.io/renan-utida/expensehub-web` e `ghcr.io/renan-utida/expensehub-worker`, só com a tag do SHA completo do commit (nunca `latest`, nenhuma tag de branch). O dono sai de `GITHUB_REPOSITORY_OWNER` em minúsculas. A label `org.opencontainers.image.source` liga o pacote ao repositório | CP3-I05 |
| 03/11/2026 | Bases das imagens | As duas usam as mesmas bases pelos mesmos `ARG`: SDK `10.0.403` para compilar e `aspnet:10.0.14` para executar (o worker usa `aspnet` porque herda `Microsoft.AspNetCore.App` da Api). Usuário não root da própria imagem (UID 1654) nas duas, e `/data` (e `/keys` no web) entregues a esse usuário para os volumes nascerem com a posse certa | CP3-I05 |
| 03/11/2026 | Workflow de publicação | `.github/workflows/publish-images.yml`, novo (`build.yml` do professor intocado). Só Docker CLI e as ações de checkout e do .NET nas mesmas versões do `build.yml`. Job `test` (restore, build, test) e job `images` com `needs: test`; push na `main` sem filtro de caminho publica, e na PR os mesmos `docker build` rodam sem login nem push, para exercitar os Dockerfiles no GitHub antes do merge. `packages: write` só no job `images` | CP3-I05 |
| 03/11/2026 | Compose | `compose.yaml` na raiz, com `name: expensehub`, imagens por `${IMAGE_TAG:?...}` (sem o SHA dentro do arquivo), RabbitMQ `4.4.0-management` fixado por tag e digest (os da CP3-I03) com `hostname` fixo, volume e healthcheck, e três volumes nomeados: `rabbitmq-data`, `expensehub-data` (`/data`, nos dois contêineres, no mesmo caminho absoluto) e `expensehub-keys` (`/keys`, só no web). Portas só em localhost e o AMQP não é publicado. Todas as variáveis obrigatórias usam `${VAR:?mensagem}`; a de `RABBITMQ_PASSWORD` fica na linha `RabbitMq__Password`, e a do broker usa `${RABBITMQ_PASSWORD}` simples, porque o script de qualidade leria `PASSWORD:?` como valor literal | CP3-I05 |
| 03/11/2026 | Chaves do Data Protection | `DataProtection:KeysPath` (vazia por padrão, sem efeito): preenchida, as chaves vão para a pasta (volume `expensehub-keys` no Compose) com nome de aplicação fixo, e o token emitido antes de recriar o contêiner continua válido. Limite: as chaves ficam sem criptografia em repouso no volume | CP3-I05 |
| 03/11/2026 | Evidência da versão em execução | `GIT_COMMIT` vira `ENV Build__Commit`, a label `org.opencontainers.image.revision` e uma linha de log na partida dos dois processos (`Image commit: <sha>`); o valor só é aceito se for hexadecimal de 7 a 64 caracteres, senão aparece `unknown`. O `/health` não muda | CP3-I05 |
| 03/11/2026 | `publish` do worker | `dotnet publish` do worker falhava com NETSDK1152: o `appsettings.json` da Api (projeto web) chega ao destino de publicação pela referência e colide com o do worker. Um alvo no `.csproj` do worker remove só esse item da publicação; a checagem de duplicatas do SDK continua ligada e o worker publica o `appsettings.json` dele (sem connection string) | CP3-I05 |
| 03/11/2026 | Auditoria da suíte | Os 13 cenários mínimos já tinham teste de efeito por componente; a issue acrescenta o percurso encadeado (controller, serviço e processador reais sobre fakes, 15 casos em `ReportScenarioTests`), reforça 7 asserções e não muda código de produção. A tabela cenário por teste está na seção da CP3-I04 | CP3-I04 |
| 03/11/2026 | Verificação de mutação | Manual, numa cópia do repositório no scratchpad (nunca na árvore real, sem `git restore`), com 45 defeitos simples nas regras novas do CP3. Rodada 1: 42 de 44 mortas, com dois sobreviventes que eram lacunas reais (o estado decide o Excel e o envelope; a causa da exceção do serializador). Rodada 2, com a suíte final: 45 de 45. O EF e o RabbitMQ reais ficam fora da suíte unitária | CP3-I04 |

## Fora de escopo do CP3

- novo frontend;
- assistente;
- nova autenticação;
- pagamento assíncrono;
- nuvem e Kubernetes;
- escala obrigatória;
- migração geral de banco;
- Transactional Outbox, retentativas automáticas completas e fila de mensagens não processáveis (opcionais, sem bônus).

## Fluxo de trabalho do CP3

1. Uma branch por issue de implementação: `cp3-i01` a `cp3-i07`. A preparação usa `cp3-00-preparacao`. A publicação no LinkedIn não tem branch nem PR.
2. Commits coesos, no padrão convencional, em inglês, com o identificador da issue entre parênteses no fim da primeira linha, por exemplo `feat(reports): add report request endpoint (CP3-I01)`. Sem `#N` no texto do commit.
3. Pull request no repositório da equipe, com título como `CP3-I01: Contrato e resultado do relatório`.
4. A descrição da PR cita a issue central como `Racass/checkpoint-csharpracass-expensehub#11`, sem `Closes`, `Fixes` ou `Resolves`, porque a issue central permanece aberta.
5. O pipeline `code-quality` é analisado antes do merge, e os problemas são corrigidos.
6. Merge com "Create a merge commit"; as branches são mantidas.

## Detalhe da CP3-I01: Contrato e resultado do relatório

Issue central: [11](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/11). Branch: `cp3-i01`. Pontos: 20. Estado: Concluída, mergeada na `main` como PR 12 da equipe (merge commit `48810e3f69def12fdc391d5d6e440af75f481836`), com score oficial do workflow `code-quality` de 100/100 na execução 69. O fluxo real até `Completed` e o `202` com o RabbitMQ dependem da CP3-I03; nesta issue o `202` é provado por teste com um publicador falso que confirma, e o `Completed` por um registro gravado direto no banco.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Entrada, período e solicitação persistida conforme o contrato | 4 | Atendido | `CreateReportRequestTests` (22): só três campos, todos obrigatórios, período invertido, `scope` numérico e data `2026-02-30` recusados na leitura do JSON, `ownerId` e `requesterId` do corpo ignorados. `ReportRulesTests` (25). `ReportServiceRequestTests` (20): salva `Pending` para o usuário do token com `Guid` novo e instante do servidor, publica só depois de salvar, entrada inválida não salva nem publica, publicação sem confirmação mantém `Pending` e devolve o id, banco fora não devolve id. `ReportsControllerTests` (16): `202` com `Location`, `400`, `503`. HTTP real: 11 corpos inválidos dão `400` e a tabela fica com 0 linhas |
| Listagem, detalhe e estados observáveis | 4 | Atendido | `ReportResponseTests` (11): os quatro estados preenchem só os campos certos, chaves exatas do contrato, instantes terminando em `Z`. `ReportServiceQueryTests` (11): lista só do usuário, mais nova primeiro, alheio igual a inexistente. HTTP real: lista com os dois pedidos, detalhe `Pending`, `Completed` e `Failed`, `404` para alheio, inexistente e id que não é `Guid` |
| Resumo por estado e totais corretos, inclusive conjunto vazio | 8 | Atendido | `ReportCalculatorTests` (24): exemplo da apostila (`Mine` 3 e 175,00; `Global` 4 e 375,00), limites inclusivos, filtro por `ExpenseDate` e nunca pela criação, vazio com os cinco estados em zero, soma exata em centavos, cálculo somente leitura. SQLite real: o SQL tem o `WHERE` do período e do dono com projeção de `Status` e `AmountCents`, e o resultado é igual ao de um oráculo independente |
| Resultado e instante de geração salvos; consulta sem recálculo | 2 | Atendido | `ReportResultSerializerTests` (20): ida e volta, centavos inteiros, rejeita resultado incoerente. `ReportServiceQueryTests`: depois de alterar reembolsos, o detalhe devolve o resultado salvo e as leituras de reembolso ficam em zero. SQLite real: o `ResultJson` volta igual, e um `CHECK` impede `Completed` sem resultado |
| Excel válido, fiel ao resultado salvo e com o contrato de download | 2 | Atendido | `ReportExcelWriterTests` (9): pacote ZIP real, `OpenXmlValidator` sem erros, cabeçalhos, cinco estados em ordem, células numéricas iguais ao resultado salvo (zeros incluídos). `ReportServiceExcelTests` (11). HTTP real: `200` com o `Content-Type` do xlsx e `attachment; filename=report-{id}.xlsx`, ZIP válido; `409` para `Pending` e `Failed`; `404` para alheio |

Decisões:

- **Calculadora em duas peças e uma só regra.** `ReportCalculator.Select` é a consulta (período inclusivo por `ExpenseDate` e escopo): o SQLite a aplica antes de carregar qualquer despesa e os testes a aplicam a uma lista em memória. `Summarize` soma em centavos inteiros e converte para `decimal` só na saída. O repositório lê só `Status` e `Amount`, sem rastrear.
- **Resultado em uma coluna.** `ReportRequest.ResultJson` guarda os centavos inteiros e o instante de geração, lidos e escritos só por `ReportResultSerializer`, que recusa resultado sem os cinco estados, com valor negativo ou com totais que não são a soma. Uma linha e uma gravação: `Completed` e o resultado nascem juntos (CP3-I03). A tabela filha com cinco linhas foi descartada por ter mais peças e nenhum ganho.
- **Tabela `ReportRequests`.** O estado é *concurrency token*, como no `Expense`, e os três instantes usam o `UtcDateTimeOffsetConverter`. Três `CHECK`: período em ordem, `Completed` com resultado e `Failed` com código de erro. Índice por solicitante e criação.
- **Entrada.** `scope` é aceito só como o nome exato `Mine` ou `Global`; número, texto numérico, outra caixa ou vazio dão `400`. A comparação é por nome, nunca por `Enum.TryParse`, que aceitaria números. As datas são `DateOnly?` obrigatórias, para que uma data ausente nunca vire o primeiro dia do calendário.
- **Respostas.** `202` só com confirmação do broker. Sem confirmação: `503` com o `id` do pedido salvo, que continua `Pending` e nunca vira `Failed`. Banco indisponível ao registrar: `503` sem `id` e sem publicar. Relatório alheio e inexistente: o mesmo `404`. Excel de relatório próprio que não está `Completed`: `409`.
- **Instantes com `Z`.** O envelope usa `DateTime` UTC e não `DateTimeOffset`, que sairia com `+00:00`.
- **Publicação.** `IReportPublisher` devolve confirmado ou não confirmado, e a exceção `ReportPublishException` conta como não confirmado. Sem publicador real, `UnavailableReportPublisher` nunca confirma, então o `POST` responde `503` e nunca finge `202`. A CP3-I03 o substitui (registro com `TryAdd`).
- **Autorização desta issue.** Só autenticação e dono: listar, detalhar e baixar mostram apenas o que o próprio usuário pediu. Na CP3-I01 qualquer usuário autenticado ainda podia pedir `Global`; a regra de escopo por papel foi fechada na CP3-I02 (veja a seção abaixo).
- **Excel.** `DocumentFormat.OpenXml` 3.5.1 (MIT, com `DocumentFormat.OpenXml.Framework` e `System.IO.Packaging`, também MIT). Uma folha "Resumo": identificador, escopo, data inicial, data final e instante de geração, depois `Estado | Quantidade | Valor`, os cinco estados e `Total geral`, com formato `0.00` nos valores.

O que foi medido (SQLite real, em um experimento descartável fora do repositório, e HTTP real contra a aplicação em um banco temporário):

- Aplicar as 4 migrations a um arquivo novo cria `ReportRequests` com as colunas, o índice `IX_ReportRequests_RequesterId_CreatedAtUtc` e os três `CHECK`.
- Com 11 despesas semeadas (o exemplo da apostila, os limites de 31/08 e 01/10, os cinco estados e o maior valor possível), `Mine` dá 6 despesas e 235,00, e `Global` dá 9 e 2.147.484.082,01, iguais a um oráculo feito com laços simples. O SQL gerado é `SELECT "e"."Status", "e"."AmountCents" FROM "Expenses" AS "e" WHERE "e"."ExpenseDate" >= @fromDate AND "e"."ExpenseDate" <= @toDate AND "e"."OwnerId" = @requesterId`, e `Global` não tem o filtro de dono. Nenhuma entidade fica rastreada e a tabela de despesas é idêntica antes e depois do cálculo.
- O agrupamento no banco (`GroupBy` com `COUNT` e `SUM` do `Amount` convertido) também traduz e devolve valores corretos (`SUM("e"."AmountCents")`, que o EF converte de volta em reais). Não foi adotado: a produção usa a projeção estreita e `Summarize`, exatamente o caminho coberto pelos testes unitários.
- A lista sai da mais nova para a mais antiga pela coluna de ticks UTC, os instantes voltam com deslocamento zero, e o resultado salvo volta com os mesmos totais e o mesmo instante.
- Os três `CHECK` rejeitam `Completed` sem resultado, `Failed` sem código e período invertido; `AddAsync` transforma a gravação rejeitada, e também um banco que não abre, em `ReportStorageException`. O estado é *concurrency token*: a segunda transição de `Pending` para `Processing` falha.
- HTTP real (duas contas `Employee`): `401` sem token nas quatro rotas; `400` para corpo vazio, data ausente, data inexistente, formato errado, período invertido, `scope` `0`, `"0"`, `1`, desconhecido e `mine`, e corpo que não é JSON, sem nenhuma linha gravada; `POST` válido dá `503` com o `id` e o pedido fica `Pending`; `requesterId`, `ownerId` e `status` no corpo são ignorados (o solicitante gravado é o do token e o estado é `Pending`); o outro usuário recebe lista vazia e `404` no detalhe e no Excel; com o relatório gravado como `Completed`, o detalhe traz o resultado com instantes em `Z` e o Excel dá `200`; `Failed` dá `409` no Excel. O log tem o `reportId` e não tem senha nem token.

Como validar:

```shell
dotnet test ./sources/ExpenseHub.slnx
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
```

Para ver as rotas rodando, use `sources/ExpenseHub.Api/ExpenseHub.Api.http` com um usuário `Employee`; as credenciais ficam em `http-client.env.json.user`, que o git ignora.

## Detalhe da CP3-I02: Autorização e isolamento

Issue central: [12](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/12). Branch: `cp3-i02`. Pontos: 15. Estado: Concluída, mergeada na `main` como PR 13 da equipe (merge commit `6e476939514aa88710b5158ea3f92a9198d632fe`), com score oficial do workflow `code-quality` de 100/100 na execução 72. A regra de role por escopo e o isolamento entraram aqui; o worker que usa o pedido gravado chegou na CP3-I03. Nesta issue um `POST` autorizado e válido respondia `503` com o `id` e o pedido `Pending`, porque ainda não havia publicador real.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Autenticação e permissão por perfil e escopo | 5 | Atendido | `ReportAccessTests` (59): tabela escrita à mão das 32 combinações das cinco roles contra `Mine` e `Global` (64 decisões), conferência de que a tabela não esqueceu nenhuma combinação, Admin, Approver e Finance nunca mudam a decisão, nome de role exato, escopo indefinido negado. `ReportServiceAuthorizationTests` (35): sem role que possa pedir, `Forbidden` até com corpo inválido (role antes da validação); com role e corpo inválido, `ValidationFailed` (o `400` vem antes do `403` de escopo); Employee pedindo `Global` e Auditor pedindo `Mine`: `Forbidden`, nada gravado e nada publicado. `ReportEndpointRolesTests` (6): o `POST` pede exatamente `Employee` ou `Auditor` e as três rotas de leitura não pedem role. Duas mutações feitas e desfeitas: trocar `Employee` e `Auditor` na regra quebrou 42 testes; tirar o atributo do `POST` quebrou 2. HTTP real: 7 contas pedindo `Mine` e `Global` e a ordem das respostas (seção "O que foi medido") |
| Listagem, detalhe e Excel isolados por solicitante, inclusive Auditor | 5 | Atendido | `ReportIsolationTests` (36): oito conjuntos de roles (Employee, Auditor, Employee e Auditor, Admin, Approver, Finance, os três juntos, sem role) contra lista, detalhe e Excel de relatórios de outro usuário em quatro estados (`Pending`, `Processing`, `Completed` `Global`, `Failed`): lista vazia, detalhe nulo, Excel não encontrado e sem dados, nunca o `409`; um Auditor não lê o `Global` de outro Auditor; ler o próprio relatório não exige role; nenhuma despesa é lida. Os testes do Auditor da CP3-I01 continuam. HTTP real: oito contas contra o relatório `Mine` de uma Employee e o `Global` de um Auditor, ambos `Completed` |
| Solicitante e escopo controlados pelo servidor até o processamento | 5 | Atendido | `ReportServiceAuthorizationTests`: o pedido aceito grava o solicitante do token e o escopo pedido. `ReportQueryFromRequestTests` (10): `ReportQuery.From` copia solicitante, escopo e período do pedido gravado e não aceita mais nada; pelos fakes, do pedido ao resultado: Employee `Mine` soma 3 despesas e 175,00 sem a despesa de outro dono, outro Employee soma só a sua, Auditor `Global` soma 4 e 375,00, e quem tem as duas roles recebe cada escopo no pedido certo; o `IReportPublisher` leva só o `Guid`. O corpo com `ownerId`, `requesterId` e `status` continua ignorado (testes da CP3-I01 seguem passando). HTTP real: a linha gravada tem o `RequesterId` do token, o `Scope` pedido e `Pending`, mesmo com os três campos no corpo |

Decisões:

- **Matriz como classe pura.** `ReportAccess.HasRequestRole` (Employee ou Auditor) e `ReportAccess.CanRequest` (o escopo contra a role). Sem EF nem web, no estilo do `ExpenseAccess` do CP2. A constante `AppRoles.ReportRequesters` alimenta o atributo da rota, e um teste prova que atributo e regra aceitam as mesmas roles.
- **Ordem das respostas do `POST`**, a mesma do CP2: (1) `401` sem token, pelo middleware de autorização; (2) `403` quando o usuário não tem `Employee` nem `Auditor`, pelo atributo da rota; (3) `400`, pela validação do corpo (o `[ApiController]` e, de novo, o serviço); (4) `403` quando a role não permite o escopo, no serviço; (5) gravação e publicação. A autorização por atributo roda no middleware, antes do MVC ler o corpo, então um usuário sem role recebe `403` até com corpo vazio, período invertido ou texto que não é JSON (provado no HTTP real). O serviço refaz a mesma sequência, para a regra não depender só do atributo.
- **Um único resultado `Forbidden`** para role e escopo, que vira `403` com `ProblemDetails` sem `id`. O log da recusa leva só o escopo pedido, sem identidade, role, token ou corpo.
- **Leitura sem role.** Listar, detalhar e baixar o Excel continuam exigindo só autenticação e posse, e o filtro por solicitante está na própria consulta ao banco. Admin, Approver, Finance e quem não tem role listam os próprios (vazios) e recebem `404` nos alheios. O privilégio `Global` do Auditor é sobre os dados do relatório dele, não sobre os relatórios dos outros.
- **Junção com o worker.** `ReportQuery.From(ReportRequest)` é a única forma de o worker obter escopo, solicitante e período: do pedido gravado, sem reler roles e sem confiar na mensagem. A CP3-I03 só chama.
- **Testes da CP3-I01 ajustados.** Os testes de `ReportServiceRequestTests` pediam relatório com um usuário sem role; agora o usuário é `Employee` (e Employee com Auditor onde o escopo é `Global`), porque a regra mudou.

O que foi medido (HTTP real contra a aplicação e um SQLite temporário, com senhas geradas na hora e nunca impressas; 120 conferências, todas aprovadas):

- Contas: o Admin do seed (só Admin), duas Employee, um Auditor, uma conta Employee e Auditor, um Approver, um Finance e uma conta sem role, com as roles atribuídas pelo Admin e novo login depois.
- Sem token, `POST` válido, com corpo vazio ou com texto que não é JSON, e as três rotas de leitura: `401`, e a tabela `ReportRequests` com 0 linhas.
- Matriz de 7 contas contra `Mine` e `Global`, com `requesterId`, `ownerId` e `status` no corpo: Employee `Mine` `503` com `id` e `Global` `403`; Auditor `Mine` `403` e `Global` `503` com `id`; Employee e Auditor `503` nos dois; Admin, Approver, Finance e sem role `403` nos dois. Cada `503` gravou uma linha com o `RequesterId` da conta, o `Scope` pedido e `Pending`; cada `403` deixou a tabela como estava e não trouxe `id`. Corpo do `403` por role: `ProblemDetails` com o título padrão; por escopo: o título "Your roles do not allow you to request a report of this scope."
- Ordem: sem role, `403` com objeto vazio, período invertido com `Global`, `scope` numérico e texto que não é JSON (também para Admin, Approver e Finance); Employee com período invertido e `Global`: `400` (erro no campo `FromDate`); Auditor com período invertido e `Mine`: `400`; Employee com datas ausentes, escopo desconhecido, `scope` numérico ou texto que não é JSON: `400`; Employee com `Global` válido e Auditor com `Mine` válido: `403`. A tabela continuou com as mesmas 4 linhas depois de todos esses pedidos.
- Isolamento com os relatórios `Completed` da Employee (`Mine`) e do Auditor (`Global`), gravados direto no banco: o dono recebe `200` no detalhe e no Excel; as outras sete contas, inclusive o Auditor lendo o relatório da Employee e a conta com as duas roles, recebem `404` no detalhe e no Excel, com o mesmo título de um id que não existe. Relatório `Pending` de outra pessoa dá `404` e nunca `409`; o `409` só vale para o dono. As listas: Employee 1, Auditor 1, conta com as duas roles 2 (nenhum relatório alheio), os outros 0, todas com `200`.
- Log da aplicação: 3 recusas de `Global` e 2 de `Mine`, só com o escopo; nenhuma senha, nenhum token e nenhum e-mail de usuário.

Como validar:

```shell
dotnet test ./sources/ExpenseHub.slnx
```

Para ver as rotas rodando, use `sources/ExpenseHub.Api/ExpenseHub.Api.http` com um usuário `Employee` e um `Auditor`; as credenciais ficam em `http-client.env.json.user`, que o git ignora.

## Detalhe da CP3-I03: RabbitMQ, worker e idempotência

Issue central: [13](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/13). Branch: `cp3-i03`. Pontos: 20. Estado: Concluída, mergeada na `main` como PR 14 da equipe (merge commit `78bca7d00a785bb37bcedc5b40bcd14adffa2535`), com score oficial do workflow `code-quality` de 100/100 na execução 75. O web publica no RabbitMQ com confirmação, o worker (`sources/ExpenseHub.Worker`) consome, calcula e grava, e a proteção contra duplicata está no banco. As imagens e o Compose são da CP3-I05.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Publicação confirmada, fila durável e mensagens persistentes | 5 | Atendido | `ReportQueueTopologyTests` (8): fila durável, não exclusiva, sem exclusão automática, prefetch 1, mensagem `Persistent` e `application/json`. `ReportMessageTests` (26): a mensagem tem só o `reportId`; corpo ilegível, sem id, com id vazio ou com outros campos. `RabbitMqOptionsTests` (17) e `MessagingRegistrationTests` (9): configuração validada sem repetir valores, o publicador real vence o `UnavailableReportPublisher` em qualquer ordem de registro, e sem configuração ele lança antes de abrir qualquer conexão. Os testes da CP3-I01 seguem provando `503` com `id` e `Pending` quando o publicador não confirma. RabbitMQ 4.4.0 real: `202` com `Location` só depois da confirmação; o broker mostra a fila `durable` verdadeira, `auto_delete` e `exclusive` falsos; a mensagem guardada tem `delivery_mode` 2 e o corpo `{"reportId":"<guid>"}`; com o broker parado o `POST` responde `503` com o `id` em cerca de 2 s e o pedido fica `Pending` |
| Worker processa; consumo confirmado só depois de salvar | 5 | Atendido | `ReportMessageProcessorTests` (33) e `ReportDeliveryTests` (15): o desfecho de sucesso só existe depois de a gravação ser confirmada pelo armazenamento; uma indisponibilidade ao achar, iniciar, ler as despesas, concluir ou gravar a falha dá `TransientFailure`, e `ReportDelivery` nunca a transforma em confirmação. Mutação feita e desfeita: devolver "concluído" quando a gravação falha quebrou 10 testes. RabbitMQ real: com o pedido `Processing` visível a mensagem está sem confirmação (`ready 0, unacked 1, consumers 1`); o worker é morto com `kill`; o pedido continua `Processing` sem resultado e o broker devolve a mensagem (`ready 1, unacked 0`); o worker volta e o mesmo id termina `Completed` |
| Conclusão e resultado consistentes; falha definitiva registrada | 5 | Atendido | `Completed`, `CompletedAtUtc` e `ResultJson` são gravados por uma só chamada (teste do fake e `CHECK` do banco); o resultado é igual ao da `ReportCalculator` (`Mine` 3 despesas e 175,00; `Global` 4 e 375,00; vazio com os cinco estados em zero); `Mine` usa o solicitante gravado, e campos extras da mensagem são ignorados. Falha definitiva (solicitante em branco, período invertido, valor fora do limite, exceção inesperada) grava `Failed` com `InvalidRequest` ou `ProcessingFailed` e mensagem fixa, sem texto da exceção. SQLite real (spike): `Pending` para `Processing`, `Processing` para `Completed` com resultado na mesma linha, `Failed`, e 8 workers disputando: só 1 aplica. RabbitMQ real: o exemplo da apostila (`Mine` 3 e 175,00, `Global` 4 e 375,00, Excel válido); um pedido incoerente vira `Failed` com `InvalidRequest` e é confirmado uma só vez, sem voltar |
| Duplicata reconhecida sem novo efeito, com proteção persistente | 5 | Atendido | Testes: relatório `Completed` ou `Failed` é `Duplicate` sem nenhuma leitura de despesas nem gravação, com resultado e `CompletedAt` intactos; a mesma mensagem duas vezes tem um só efeito; processador e repositório novos sobre o mesmo estado (reinício) continuam `Duplicate` mesmo com as despesas alteradas; corrida perdida no início ou na conclusão vira `Duplicate`. Mutação feita e desfeita: remover a checagem inicial quebrou 5 testes. RabbitMQ real: com o relatório `Completed`, o worker é parado e iniciado de novo e a mesma mensagem é republicada pelo painel de gerenciamento: a linha do banco (estado, `StartedAt`, `CompletedAt`, `ResultJson`) fica idêntica byte a byte, o número de linhas não muda, o log mostra a duplicata e nenhuma nova conclusão, e a fila esvazia |

Fila e configuração:

- Fila `expensehub.reports` (`RabbitMq:QueueName`), declarada pelo web e pelo worker a partir da mesma classe (`ReportQueueTopology`): durável, não exclusiva, sem exclusão automática, exchange padrão com a chave de roteamento igual ao nome da fila. Mensagem `{"reportId":"<guid>"}`, `application/json`, persistente.
- Publicação com confirmação, `mandatory` e espera de 5 s; consumo com confirmação manual, prefetch 1 e reconexão automática.
- Configuração na seção `RabbitMq` (`Host`, `Port`, `VirtualHost`, `QueueName`, `PublishTimeoutSeconds`, `TransientRetryDelaySeconds`, `UserName`, `Password`). Usuário e senha não têm padrão e não ficam em arquivo versionado: vêm de variáveis de ambiente (`RabbitMq__UserName`, `RabbitMq__Password`) ou de user-secrets. A tabela completa e o `docker run` com volume nomeado estão no [README](README.md#mensageria-e-worker-cp3-i03). O broker guarda a fila e as mensagens persistentes em `/var/lib/rabbitmq`, que o volume nomeado mantém fora do contêiner.
- **Web e worker precisam apontar para o mesmo arquivo de banco, por caminho absoluto na connection string** (`ConnectionStrings__ExpenseHub`, variável de ambiente nos dois). Um caminho relativo daria dois bancos: o worker não acharia os pedidos e os relatórios ficariam `Pending`. O worker avisa no log quando o caminho é relativo.

Decisões:

- **Estrutura.** `ExpenseHub.Worker` referencia a Api e é fino (host e adaptador do RabbitMQ). Tudo o que ele usa já era `public` na Api; não foi preciso `InternalsVisibleTo`. O projeto usa `Microsoft.NET.Sdk` e herda o framework da Api: nenhum pacote além do `RabbitMQ.Client` 7.2.2 (Apache-2.0 OR MPL-2.0; traz `System.IO.Pipelines` e `System.Threading.RateLimiting`, MIT). O custo é que a Api é um projeto web, então o worker herda o framework `Microsoft.AspNetCore.App` (a CP3-I05 usa a base `aspnet`).
- **Processador testável.** `ReportMessageProcessor` devolve um desfecho (`Completed`, `Duplicate`, `PermanentFailure`, `Discarded` ou `TransientFailure`) e `ReportDelivery` decide a confirmação; o consumidor só executa. Um `false` de uma transição condicional só vira `Duplicate` se o estado gravado mostrar o relatório já terminado; do contrário nada está salvo e a mensagem volta.
- **Falha transitória.** Banco ocupado, trancado, com erro de disco ou inacessível (códigos `BUSY`, `LOCKED`, `NOMEM`, `IOERR`, `FULL` e `CANTOPEN` do SQLite, classificados pela classe pura `SqliteTransientErrors`) não confirma a mensagem: o worker espera `TransientRetryDelaySeconds` (5 s) e devolve com `nack`. Não há limite de tentativas, porque a indisponibilidade não prova falha do cálculo, e o canal segura a mensagem durante a espera, então não há laço quente. Nunca vira `Failed`.
- **Retomada de `Processing`.** Uma mensagem entregue de novo para um pedido `Processing` retoma: recalcula e tenta concluir, sem regravar o início (`StartedAt` fica o da primeira tentativa). A gravação final só vale se o estado ainda for `Processing`.
- **Registro inexistente.** Mensagem para um pedido que não existe é recusada sem voltar (não há o que marcar) e registrada em erro; sem fila de mensagens não processáveis ela some. A causa provável é web e worker em bancos diferentes.
- **SQLite compartilhado.** Em `AddExpenseHubPersistence`, usado pelos dois processos: `PRAGMA busy_timeout = 5000` e, **depois dele**, `PRAGMA journal_mode = WAL`, em cada conexão, e `Default Timeout` de 5 s quando a connection string não define. O WAL exige os dois processos no mesmo computador, com o arquivo em disco local.
- **Logs por `reportId`**, em `[LoggerMessage]`: publicação (confirmada, não confirmada, com falha), mensagem recebida (e se é reentrega), início ou retomada, conclusão, duplicata, falha definitiva, nova tentativa e confirmação. Sem e-mail, token, senha, connection string nem texto de exceção (só o nome do tipo).

Janela sem Transactional Outbox:

- Salvar o pedido e publicar a mensagem são operações separadas. Uma queda entre as duas deixa o pedido `Pending` sem mensagem. Reiniciar o worker **não** recria uma mensagem que nunca existiu, e um novo `POST` cria outro id.
- Um `503` com `id` **não garante que a mensagem não chegou ao broker**: a confirmação pode ter se perdido (o broker gravou a mensagem e a conexão caiu antes da resposta). Nesse caso o worker ainda conclui o relatório. Quem recebe `503` consulta o `id` retornado (`GET /api/reports/{id}`) em vez de supor falha.
- Mostrado na prática: com o RabbitMQ parado o `POST` deu `503` com `id`; depois que o broker voltou e o worker se reconectou, um novo `POST` deu `202` e `Completed`, e o relatório do `503` continuou `Pending` para sempre (nenhuma mensagem o representava).
- Transactional Outbox, retentativas completas e fila de mensagens não processáveis são opcionais e não foram feitos.

O que foi medido (RabbitMQ 4.4.0 em contêiner, web e worker como processos locais, um SQLite temporário em caminho absoluto, senhas e credenciais do broker geradas na hora e nunca impressas; 65 conferências, todas aprovadas):

- Fluxo normal: `POST` `Mine` dá `202` com `Location`, o relatório vai a `Completed` com 3 despesas e 175,00 (`Approved` 2 e 150,00, `Paid` 1 e 25,00, os outros três estados em zero), instantes em UTC com `Z` e erro nulo; o Excel baixa e é um pacote válido; `Global` do Auditor dá 4 despesas e 375,00; a fila volta a `0 0 1` (nada pronto, nada sem confirmação, um consumidor). O log do worker tem, para o relatório, a mensagem recebida, o início, a conclusão e a confirmação; o log do web, a confirmação do broker.
- Worker parado: o `POST` ainda dá `202`, o relatório fica `Pending` depois de 4 s, a mensagem fica pronta na fila (`1 0 0`) e é persistente com só o id; ao ligar o worker, o mesmo id vira `Completed`. A espiada pela API de gerenciamento devolve a mensagem à fila e a marca como reentrega, por isso o log mostra `redelivered: True` nesse relatório.
- Duplicata depois de reiniciar o worker: descrita no critério acima.
- `Processing` interrompido de verdade: um gatilho do banco temporário atrasa a gravação de `Completed` (consulta recursiva de cerca de 10 s) e abre a janela em que o `Processing` já é visível; o script espera o `Processing`, confere a mensagem sem confirmação (`0 1 1`) e mata o processo do worker; o pedido fica `Processing` sem resultado e a mensagem volta (`1 0 0`); com o gatilho removido e o worker de volta, o log mostra a reentrega, o início como retomada, a conclusão e a confirmação, o `StartedAt` é o da primeira tentativa e o resultado está certo.
- Falha definitiva: um pedido sem solicitante, inserido direto no banco, vira `Failed` com `InvalidRequest` e a mensagem fixa, sem resultado; a mensagem é confirmada uma só vez e não volta; republicada, é duplicata e não gera segunda falha. Uma mensagem que não é JSON e um id inexistente são recusados sem voltar, com log.
- Falha transitória: um processo segura a escrita do banco por 16 s; o worker não confirma (o pedido fica `Pending` e a mensagem continua no broker), registra a nova tentativa com o `reportId` e a recebe como reentrega; ao liberar o banco o mesmo id vira `Completed`.
- RabbitMQ parado: descrito na janela sem Outbox; o `503` trouxe o `id`, o pedido ficou `Pending` e o log do web registrou a falha com o `reportId`. O worker voltou ao consumidor sozinho (`0 0 1`).
- Logs: nenhuma linha dos dois processos tem a credencial do broker, a senha do Admin, a senha de uma conta, o cabeçalho `Bearer` nem um e-mail.
- SQLite real (spike descartável): o WAL fica gravado no arquivo e uma conexão do contexto tem `busy_timeout` 5000 e `journal_mode` `wal`; com o WAL, uma leitura não é bloqueada por quem grava; com outra conexão segurando a escrita, a gravação espera cerca de 5 s e vira `ReportStorageException` com a falha interna classificada como transitória, e depois que o bloqueio cai a mesma gravação aplica; um arquivo que não abre dá falha transitória na leitura das despesas. A espera de uma gravação é a maior entre o `busy_timeout` e o `Default Timeout`: (2 s, 5000 ms) esperou 5,1 s, (5 s, 1000 ms) esperou 5,7 s, (30 s, 5000 ms) esperou 31,0 s e (1 s, 1000 ms) esperou 1,0 s; por isso os dois ficam em 5 s.
- Dois processos abrindo juntos um banco ainda sem WAL, com a espera definida antes da troca de modo (ordem adotada): 0 erros em 220 rodadas (40 mais 60 mais 120), todas com os dois processos no modo `wal` ao fim, menos uma rodada da primeira bateria em que um dos processos leu outro modo logo depois da troca, sem erro e sem causa registrada (a instrumentação era menor naquela rodada; as 180 seguintes, com o detalhe de cada resposta, não repetiram). Com a ordem invertida (WAL antes da espera): 0 erros em 220 rodadas (40, mais 120, mais 60 com `Default Timeout` zero). Neste computador não foi possível mostrar diferença entre as duas ordens: as duas abriram sem erro. A ordem com a espera antes foi mantida por segurança, porque a troca de modo precisa de um bloqueio e a conexão já deve saber quanto esperar por ele quando web e worker abrem o banco ao mesmo tempo.

Como validar:

```shell
dotnet test ./sources/ExpenseHub.slnx
```

Para rodar o fluxo real, suba o RabbitMQ, o web e o worker como no [README](README.md#mensageria-e-worker-cp3-i03), com a mesma connection string absoluta nos dois, e use `sources/ExpenseHub.Api/ExpenseHub.Api.http` com um `Employee` e um `Auditor`.

## Detalhe da CP3-I04: Testes próprios significativos

Issue central: [14](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/14). Branch: `cp3-i04`. Pontos: 15. Estado: Concluída na branch, aguardando a PR. A issue audita a suíte contra a tabela de 13 cenários mínimos, cria só o que faltava, prova por mutação que os testes pegam defeitos nas regras novas do CP3 e documenta o comando. **Nenhum código de produção mudou**: só testes e documentação.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Cálculo, conjunto vazio e validação de período | 5 | Atendido | `ReportCalculatorTests` (exemplo da apostila estado por estado, limites inclusivos, vazio com cinco estados), `ReportRulesTests`, `CreateReportRequestTests`, `ReportQueryFromRequestTests`; no percurso encadeado, `Scenario3_...` (400 sem gravar nem publicar); mutações C1 a C14 todas mortas |
| Negação por perfil e escopo; isolamento por solicitante | 5 | Atendido | `ReportAccessTests` (as 32 combinações das cinco roles), `ReportServiceAuthorizationTests`, `ReportIsolationTests` (36 casos), `ReportEndpointRolesTests`; no percurso encadeado, `Scenario4_...`, `Scenario5_...` (detalhe e Excel) e `Scenario9_...`; mutações A1 a A15 todas mortas |
| Duplicata, resultado persistido e Excel, com asserções de comportamento | 5 | Atendido | `ReportMessageProcessorTests` (duplicata, reinício, corrida entre workers), `ReportDeliveryTests`, `ReportResultSerializerTests`, `ReportExcelWriterTests`, `ReportServiceQueryTests` e `ReportServiceExcelTests`; no percurso encadeado, `Scenario6_...`, `Scenario7_...`, `Scenario8_...`, `Scenario10_...` e `Scenario12_...`; mutações D1 a D16 todas mortas |

**Como a auditoria foi feita.** Li os testes (não só os nomes) contra cada linha da tabela de cenários mínimos da apostila, olhando se a asserção é de efeito (o que o cliente recebe e o que ficou gravado) e não só de chamada. Resultado: **nenhum cenário estava sem teste**, e os testes de serviço, isolamento e Excel já afirmavam efeito (resultado, arquivo e conteúdo vazios, o mesmo `NotFound` de um id inexistente, leitura de despesas em zero, nada salvo nem publicado). Faltava profundidade em três pontos: o **percurso encadeado** (o que o cliente recebe quando o controller, o serviço e o processador reais trabalham juntos), o **corpo das respostas de erro** (os testes do controller só conferiam o código) e a **prova** de que os testes pegam defeito nas regras do CP3, que nunca tinha sido medida.

**Tabela cenário por teste.** A coluna da direita diz o que a issue acrescentou para o cenário.

| # | Cenário mínimo | Testes que o provam | O que a issue acrescentou |
|---|---|---|---|
| 1 | Dados conhecidos e limites inclusivos do período | `ReportCalculatorTests`: `Calculate_ExampleOfTheAssignmentForMine_GivesThreeExpensesAnd175` e `..._ForGlobal_GivesFourExpensesAnd375` (quantidade e soma por estado e no total), `Calculate_ExpensesOnTheFirstAndOnTheLastDay_AreCounted`, `Calculate_ExpensesTheDayBeforeAndTheDayAfterThePeriod_AreNotCounted`, `Calculate_PeriodOfOneDay_CountsOnlyThatDay`, `Calculate_FiltersTheDateOfTheExpenseAndNotTheCreationDate`, `Calculate_OneExpenseInEachState_CountsOneInEachLine`, `Calculate_AmountsThatBreakFloatingPoint_AreExact`; pelo worker, `ReportMessageProcessorTests.HandleAsync_PendingMine_CompletesWithTheResultOfTheCalculator` | nada (já coberto) |
| 2 | Nenhum reembolso elegível | `Calculate_NoExpenses_GivesFiveStatesAndTotalsAtZero`, `Calculate_NoEligibleExpense_GivesFiveStatesAndTotalsAtZero`, `HandleAsync_NoExpenses_CompletesWithFiveZeros`, `ReportResultSerializerTests.RoundTrip_EmptyResult_KeepsTheFiveStatesAtZero`, `ReportExcelWriterTests.Build_EmptyResult_HasNumericZeros` | nada (já coberto) |
| 3 | Período invertido, datas ausentes ou inválidas: 400, nada salvo nem publicado | `ReportRulesTests` (`ValidatePeriodOrder_*`, `Validate_*`), `CreateReportRequestTests.Json_InvalidDate_IsRefused` e `Validate_*`, `ReportServiceRequestTests` (`RequestAsync_StartAfterEnd_IsRejectedWithoutSavingOrPublishing`, `..._MissingDates_...`, `..._InvalidScope_...`), `ReportsControllerTests.RequestAsync_ServiceReportsInvalidData_Returns400WithTheFieldErrors` | `ReportScenarioTests.Scenario3_RejectedRequest_Is400AndNothingIsSavedOrPublished` (3 casos): o 400 sai do controller real, com o erro no campo certo, e o repositório e o publicador ficam vazios |
| 4 | Employee solicita `Global` | `ReportServiceAuthorizationTests` (`RequestAsync_EmployeeAsksForGlobal_CreatesNoRequestAndNoMessage`, `RequestAsync_ScopeFollowsTheRoles`), `ReportAccessTests` (`Matrix_ListsEveryCombinationOfTheFiveRolesOnce`, `CanRequest_FollowsTheMatrixOfTheAssignment`), `ReportsControllerTests.RequestAsync_Forbidden_Returns403WithoutAnIdentifier` | `Scenario4_EmployeeAsksForGlobal_Is403AndNothingIsCreated`: 403 do controller real e nada que um worker pudesse processar |
| 5 | Solicitação de outro usuário: não expõe detalhe nem resultado | `ReportIsolationTests` (36 casos: lista, detalhe e Excel, oito conjuntos de roles, quatro estados), `ReportServiceQueryTests` (`GetAsync_RequestOfAnotherUserOrMissing_GivesTheSameAnswer`, `GetAsync_AuditorDoesNotReadTheRequestOfAnotherUser`), `ReportsControllerTests.GetAsync_NotFound_Returns404` | `Scenario5_DetailOfAnotherUsersReport_Is404WithTheBodyOfAMissingReport` e `Scenario5_ExcelOfAnotherUsersReport_Is404WithoutAFile`: o corpo do 404 é igual ao de um id inexistente e não traz id, solicitante, valor nem `result`; a lista do chamador não inclui o relatório alheio, mesmo com `Employee` e `Auditor` |
| 6 | Mesma mensagem novamente: nenhum novo resultado ou efeito | `ReportMessageProcessorTests`: `HandleAsync_SameMessageTwice_HasOneEffect`, `HandleAsync_CompletedReport_IsADuplicateAndChangesNothing`, `HandleAsync_FailedReport_IsADuplicateAndChangesNothing`, `HandleAsync_AfterARestart_TheCompletedReportIsStillADuplicate`; `ReportDeliveryTests` (a confirmação) | `Scenario6_SameMessageAgain_AfterTheExpenseChanged_LeavesTheSavedResultUntouched`: mensagem repetida depois de alterar um reembolso e de reiniciar o worker é `Duplicate`, o estado gravado fica idêntico, nada é escrito nem lido, e o que o web devolve não muda |
| 7 | Consulta depois de alterar um reembolso: resultado persistido, sem recálculo | `ReportServiceQueryTests.GetAsync_AfterAnExpenseChanges_ReturnsTheSavedResultWithoutRecalculating`, `ReportServiceExcelTests.GetExcelAsync_AfterAnExpenseChanges_StillHasTheSavedResultAndReadsNoExpense`, `ReportServiceQueryTests.ListAsync_DoesNotReadAnyExpense`, `ReportIsolationTests.Reading_NeverReadsAnExpense` | `Scenario7_WorkerResult_SurvivesAnExpenseChange_ForTheDetailAndTheExcel`: o resultado agora é o que o **processador** gravou (nos testes antigos era montado por um ajudante), e o detalhe e o Excel continuam iguais depois da alteração, sem nenhuma leitura de despesa |
| 8 | Download concluído: planilha válida, cabeçalhos corretos, números iguais ao salvo | `ReportExcelWriterTests` (`Build_ProducesARealXlsxPackage`, `Build_OpensAndIsValid`, `Build_HasTheHeadersTheFiveStatesInOrderAndTheGrandTotal`, `Build_CountsAndAmountsAreNumericCellsEqualToTheSavedResult`), `ReportsControllerTests.ExcelAsync_Ready_ReturnsTheFileWithTheContract`, `ReportServiceExcelTests.GetExcelAsync_OwnCompletedReport_GivesTheFile` | `Scenario8_CompletedExcel_HasTheContractAndTheCellsOfTheSavedResult`: o arquivo entregue pelo controller tem o tipo e o nome do contrato e, aberto, cada contagem e valor é uma célula numérica igual ao `ResultJson` gravado; e `GetExcelAsync_OwnCompletedReport_GivesTheFile` passou a abrir o arquivo (antes só conferia `PK`) |
| 9 | Download pendente, `Failed` ou alheio: 409 / 404 sem arquivo nem dados | `ReportServiceExcelTests` (`GetExcelAsync_OwnReportNotCompleted_IsAConflict` com os três estados, `GetExcelAsync_ReportOfAnotherUser_IsNotFound`), `ReportIsolationTests.GetExcelAsync_NoUserDownloadsTheFileOfSomeoneElse` e `GetExcelAsync_Auditor_OwnNotCompletedIsAConflictAndSomeoneElsesIsNotFound`, `ReportsControllerTests.ExcelAsync_NotCompleted_Returns409` e `ExcelAsync_NotFound_Returns404` | `Scenario9_OwnReportNotCompleted_Is409WithoutAFile` (três estados): 409 do controller real, resultado que não é arquivo e corpo sem resultado; dois testes de serviço ganharam a asserção de que não há nome de arquivo nem conteúdo; `GetExcelAsync_OwnReportNotCompleted_GivesNoFileEvenWithAResultSaved` (veja os sobreviventes) |
| 10 | Publicação sem confirmação: 503 com o id, registro `Pending`, nunca `Failed` | `ReportServiceRequestTests` (`RequestAsync_BrokerDoesNotConfirm_KeepsTheSavedReportPendingAndGivesItsIdentifier`, `RequestAsync_BrokerCannotBeReached_...`), `ReportServiceAuthorizationTests.RequestAsync_AllowedScopeAndBrokerDoesNotConfirm_StillKeepsTheSavedReportPending`, `ReportsControllerTests.RequestAsync_NotPublished_Returns503WithTheIdentifierOfTheSavedReport` | `Scenario10_BrokerDoesNotConfirm_Is503WithTheIdAndTheReportStaysPending` (broker sem confirmar e broker fora do ar): o 503 do controller real traz o `id` do relatório gravado, o relatório está `Pending` e o detalhe consultado por esse id não tem erro nem resultado |
| 11 | Cálculo somente leitura | `ReportCalculatorTests.Calculate_IsReadOnly_NoExpenseChanges` e `Summarize_SameLinesTwice_GiveTheSameResult` (a prova em SQLite real foi manual, na CP3-I01) | nada (já coberto) |
| 12 | Corpo com `ownerId` ou `requesterId`: ignorado | `CreateReportRequestTests` (`Json_RequesterAndOwnerInTheBody_AreIgnored`, `CreateReportRequest_HasOnlyTheDatesAndTheScope`), `ReportServiceRequestTests.RequestAsync_TheRequesterIsAlwaysTheAuthenticatedUser`, `ReportsControllerTests.RequestAsync_ForwardsTheUserOfTheClaimsAndTheFieldsOfTheBody` | `Scenario12_BodyWithOwnerIdAndRequesterId_IsIgnored_TheSavedRequesterIsTheCaller`: do JSON bruto ao pedido gravado, o solicitante é o do token e o nome do corpo não aparece em lugar nenhum |
| 13 | Decisão ack/nack numa classe testável | `ReportDeliveryTests` (`For_FollowsTheTable`, `For_UnknownOutcome_PutsTheMessageBack`, `UnavailableDatabase_AtAnyStep_PutsTheMessageBack`, `Acknowledgement_IsGivenOnlyWhenTheWorkIsSaved`), `ReportMessageProcessorTests` (os desfechos) | nada (já coberto) |

**O que a issue criou e reforçou.** A suíte passa de 1149 para 1170 testes (21 novos), todos sem banco, rede, RabbitMQ nem Docker, em fakes escritos à mão.
- **`ReportScenarioTests` (15 casos).** Usam o `ReportsController`, o `ReportService` e o `ReportMessageProcessor` **reais** sobre os fakes que já existiam, e conferem o que o cliente recebe e o que ficou gravado. O percurso web e worker é: `POST` pelo controller, o worker (processador sobre o fake do estado) trata a mensagem, e o web lê o que o worker gravou.
- **6 testes que o primeiro passe de mutação mostrou faltar:** `GetExcelAsync_OwnReportNotCompleted_GivesNoFileEvenWithAResultSaved` (3 estados) e `ReportResponseTests.From_ReportNotCompletedWithAResultSaved_HasNoResult` (3 estados). Veja os sobreviventes abaixo.
- **7 testes antigos reforçados** (sem teste novo): três de `ReportServiceExcelTests` (arquivo e conteúdo vazios no 404 do Auditor e do inexistente; o arquivo concluído passa a ser aberto e conferido) e quatro de `ReportResultSerializerTests` (a exceção passa a ter a causa certa, `five states` ou `cannot be read`, e não só o tipo).

### Verificação de mutação da CP3-I04

Mesmo método da I09 do CP2 (quebrar a regra de propósito, ver o teste falhar, desfazer), com duas diferenças: **nenhuma mutação toca a árvore real** e **não se usa `git restore`**, que é proibido nesta sessão. Um script local de Python, fora do repositório, não versionado e fora do CI:
- copia o repositório (sem `.git`, `bin` nem `obj`) para o scratchpad; cada rodada tem a sua cópia, e os três grupos rodam em paralelo, cada um na sua. As cópias ficam fisicamente no scratchpad e são acessadas por uma letra de unidade curta (`subst`), porque o caminho do scratchpad faz alguns arquivos do repositório passarem dos 260 caracteres do Windows;
- antes da primeira mutação, confirma que a cópia compila e passa inteira, sem nenhuma mutação (1149 testes na rodada 1, 1170 na 2);
- aplica **uma** mutação por vez, só na cópia, conferindo que o trecho existe uma vez só; roda `dotnet test`; classifica o resultado (morta, sobrevivente, não compilou ou erro de execução) e anota o primeiro teste que falhou; e restaura o arquivo a partir dos bytes originais, conferindo que voltou idêntico;
- ao fim, roda a suíte de novo na cópia restaurada (passou inteira nas seis cópias).

A rodada 1 usou a suíte como estava antes desta issue, para descobrir sobreviventes; a rodada 2 usou a suíte final. Cada mutação é um defeito simples em código de produção das regras novas: limites do período, filtro de dono, estados, soma em centavos, matriz de roles, ordem das respostas, ordem de gravar e publicar, estados do Excel, duplicata, retomada, confirmação, tipo das células, tipo e nome do arquivo, envelope.

| Grupo | Mutações | Mortas na rodada 1 (1149 testes) | Mortas na rodada 2 (1170 testes) |
|---|---:|---:|---:|
| C. Cálculo, vazio e período (`ReportCalculator`, `ReportRules`, `ReportQuery`) | 14 | 14 | 14 |
| A. Negação, escopo e isolamento (`ReportAccess`, `ReportService`, `ReportsController`, `AppRoles`) | 15 | 14 (sobreviveu A10) | 15 |
| D. Duplicata, resultado salvo e Excel (`ReportMessageProcessor`, `ReportDelivery`, `ReportService`, `ReportExcelWriter`, `ReportResultSerializer`, `ReportResponse`, `ReportMessage`) | 16 | 14 de 15 (sobreviveu D13; D16 ainda não existia) | 16 |
| **Total** | **45** | **42 de 44** | **45 de 45** |

**A história, com honestidade.** A rodada 1 deixou **dois sobreviventes**, e os dois eram lacunas reais dos testes, não defeitos inventados:
- **A10** (o Excel deixa de checar que o relatório é `Completed` e só olha se existe resultado). O `CHECK` da tabela só exige resultado quando o estado é `Completed`; ele não proíbe um resultado em outro estado, então o caso não é impossível por desenho. Nenhum teste dizia que **o estado decide**. Novo: `GetExcelAsync_OwnReportNotCompleted_GivesNoFileEvenWithAResultSaved`. O mesmo padrão no envelope virou a mutação **D16**, acrescentada depois da rodada 1 (por isso 45 e não 44), e `From_ReportNotCompletedWithAResultSaved_HasNoResult`.
- **D13** (a leitura do resultado salvo deixa de exigir os cinco estados uma vez cada). Os testes só pediam `InvalidOperationException`, e sem a checagem uma exceção do mesmo tipo vinha de um passo posterior, por acidente. Os quatro testes do serializador passaram a afirmar a causa da exceção.

Depois disso, a rodada 2 matou as 45. As mutações que dependiam de **um único teste** passaram de 9 na rodada 1 (C13, A11, A12, A15, D2, D3, D6, D7, D11) para 3 na rodada 2 (C13, D2 e D3): apagar ou enfraquecer esses três testes deixaria o defeito passar.

**Qual é o valor dos 15 testes de cenário.** Para não supor, rodei uma passada só com eles contra as mesmas 45 mutações: **os 15 sozinhos matam 29 das 45**. Não pegam o que é regra de um componente só (por exemplo, o período de um dia ser válido, `mine` em minúsculas, os valores sem centavos, o `Processing` retomado), que continua nos testes por componente. Também não foram eles que fecharam os dois sobreviventes. O valor deles é outro: mostram que as peças, juntas, dão as respostas do contrato, e dão um segundo detector a seis mutações que na rodada 1 dependiam de um teste só (A11, A12, A15, D6, D7 e D11).

<details>
<summary>As 45 mutações da rodada 2: defeito simples, testes que falharam e o primeiro teste que pegou</summary>

| Id | Defeito simples | Falhas | Primeiro teste que pegou |
|---|---|---:|---|
| C1 | o primeiro dia do período deixa de entrar (`>=` vira `>`) | 25 | `Calculate_PeriodOfOneDay_CountsOnlyThatDay` |
| C2 | o último dia do período deixa de entrar (`<=` vira `<`) | 21 | `Scenario7_WorkerResult_SurvivesAnExpenseChange_ForTheDetailAndTheExcel` |
| C3 | o período filtra a data de criação e não a data da despesa | 23 | `Calculate_ExampleOfTheAssignmentForGlobal_GivesFourExpensesAnd375` |
| C4 | `Mine` soma as despesas de todos os donos | 19 | `Scenario7_WorkerResult_SurvivesAnExpenseChange_ForTheDetailAndTheExcel` |
| C5 | escopo invertido (`Mine` lê todos, `Global` lê só o solicitante) | 22 | `AuditorGlobal_TheSavedRequestAddsUpTheExpensesOfEveryone` |
| C6 | só quatro dos cinco estados (sem `Rejected`) | 93 | `Build_CountsAndAmountsAreNumericCellsEqualToTheSavedResult` |
| C7 | valores somados sem os centavos | 4 | `Calculate_AmountsThatBreakFloatingPoint_AreExact` |
| C8 | total de itens é o maior estado, não a soma | 94 | `GetExcelAsync_AuditorForTheReportOfAnotherUser_IsNotFound` |
| C9 | período de um dia passa a ser inválido (`>` vira `>=`) | 2 | `Validate_PeriodOfOneDay_HasNoErrors` |
| C10 | a ordem do período nunca é checada | 7 | `RequestAsync_SameEmployeeAndScope_Is400WithBadDataAnd403WithGoodData` |
| C11 | a data inicial deixa de ser obrigatória | 3 | `Validate_MissingFields_ReportEachField` |
| C12 | o escopo aceita `mine` em minúsculas | 3 | `TryParseScope_AnythingElse_IsNotAScope` |
| C13 | a consulta do worker recusa período de um dia | 1 | `Calculate_PeriodOfOneDay_CountsOnlyThatDay` |
| C14 | a consulta do worker ignora o escopo gravado e lê todos | 10 | `EmployeeMine_TheSavedRequestAddsUpOnlyTheExpensesOfTheRequester` |
| A1 | `Global` exige `Employee` em vez de `Auditor` | 29 | `CanRequest_FollowsTheMatrixOfTheAssignment` |
| A2 | `Mine` também aberto ao `Auditor` | 10 | `CanRequest_FollowsTheMatrixOfTheAssignment` |
| A3 | o `Admin` pode pedir relatório | 5 | `AdminApproverAndFinance_NeverChangeTheDecision` |
| A4 | o serviço deixa de checar o escopo contra as roles | 10 | `RequestAsync_ScopeFollowsTheRoles` |
| A5 | o 403 de escopo vem antes do 400 de validação | 2 | `RequestAsync_InvalidDataWithAnAllowedRole_IsValidationFailedBeforeTheScopeIsChecked` |
| A6 | o solicitante gravado não é o usuário autenticado | 13 | `Scenario10_BrokerDoesNotConfirm_Is503WithTheIdAndTheReportStaysPending` |
| A7 | publica antes de gravar o pedido | 17 | `RequestAsync_Accepted_RecordsTheRequesterAndTheScopeOnTheSavedRequest` |
| A8 | sem confirmação do broker o relatório vira `Failed` | 5 | `RequestAsync_AllowedScopeAndBrokerDoesNotConfirm_StillKeepsTheSavedReportPending` |
| A9 | Excel de relatório alheio ou inexistente dá 409 em vez de 404 | 14 | `GetExcelAsync_NoUserDownloadsTheFileOfSomeoneElse` |
| A10 | o Excel não checa o estado `Completed` (só se há resultado) | 3 | `GetExcelAsync_OwnReportNotCompleted_GivesNoFileEvenWithAResultSaved` (sobrevivente da rodada 1) |
| A11 | pedido recusado responde 401 em vez de 403 | 2 | `Scenario4_EmployeeAsksForGlobal_Is403AndNothingIsCreated` |
| A12 | Excel de relatório não concluído responde 404 em vez de 409 | 4 | `ExcelAsync_NotCompleted_Returns409` |
| A13 | publicação sem confirmação responde 202 em vez de 503 | 4 | `RequestAsync_NotPublished_Returns503WithTheIdentifierOfTheSavedReport` |
| A14 | a rota do pedido também aberta ao `Admin` | 2 | `RequestRoute_AsksForExactlyEmployeeOrAuditor` |
| A15 | o controller descarta as roles do token | 9 | `Scenario3_RejectedRequest_Is400AndNothingIsSavedOrPublished` |
| D1 | mensagem de relatório já terminado deixa de ser duplicata | 6 | `HandleAsync_FailedReport_IsADuplicateAndChangesNothing` |
| D2 | perder a corrida de conclusão ainda responde `Completed` | 1 | `HandleAsync_AnotherWorkerCompletedDuringTheCalculation_TheSavedResultIsKept` |
| D3 | relatório `Processing` é iniciado de novo (o início é regravado) | 1 | `HandleAsync_ProcessingReport_IsResumedAndCompleted` |
| D4 | falha definitiva é confirmada sem ser gravada | 9 | `UnavailableDatabase_AtAnyStep_PutsTheMessageBack` |
| D5 | falha transitória é confirmada (a mensagem se perde) | 9 | `UnavailableDatabase_AtAnyStep_PutsTheMessageBack` |
| D6 | consultar um relatório o recalcula (lê as despesas) | 2 | `GetAsync_AfterAnExpenseChanges_ReturnsTheSavedResultWithoutRecalculating` |
| D7 | o Excel sai de um novo cálculo e não do resultado salvo | 4 | `Scenario7_WorkerResult_SurvivesAnExpenseChange_ForTheDetailAndTheExcel` |
| D8 | contagens da tabela como texto | 3 | `Scenario8_CompletedExcel_HasTheContractAndTheCellsOfTheSavedResult` |
| D9 | valores da tabela como texto | 3 | `Scenario8_CompletedExcel_HasTheContractAndTheCellsOfTheSavedResult` |
| D10 | falta a linha do total geral | 6 | `GetExcelAsync_AfterAnExpenseChanges_StillHasTheSavedResultAndReadsNoExpense` |
| D11 | tipo do download é `text/csv` | 2 | `ExcelAsync_Ready_ReturnsTheFileWithTheContract` |
| D12 | nome do download termina em `.csv` | 4 | `FileName_FollowsTheContract` |
| D13 | a leitura do resultado deixa de exigir os cinco estados | 3 | `Deserialize_StateMissing_Throws` (sobrevivente da rodada 1) |
| D14 | o envelope `Completed` não traz o resultado | 7 | `From_CompletedReportWithAnUnreadableResult_Throws` |
| D15 | mensagem com o identificador vazio é aceita | 2 | `HandleAsync_InvalidMessage_IsDiscardedWithoutTouchingTheStore` |
| D16 | o envelope mostra resultado sempre que há um salvo, em qualquer estado (acrescentada depois da rodada 1) | 3 | `From_ReportNotCompletedWithAResultSaved_HasNoResult` |

</details>

**O que a verificação não cobre.** O filtro por solicitante do repositório de produção (`ReportRepository`, em EF), as transições condicionais do `ReportProcessingStore`, os adaptadores do RabbitMQ e o host do worker não têm teste unitário, porque os testes não podem tocar banco nem broker. Uma mutação ali sobreviveria por desenho, e não a contei como morta nem como sobrevivente. O isolamento da suíte vem do `FakeReportRepository`, que imita esse filtro, e a prova no SQLite real foi feita à mão (CP3-I01 a CP3-I03). As regras da entrega (CP3-I05) têm a sua própria verificação, com 4 mutações mortas. A verificação é manual e não faz parte do CI.

**Como validar.**

```shell
dotnet build ./sources/ExpenseHub.slnx --no-incremental
dotnet test ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx --filter "FullyQualifiedName~ExpenseHub.UnitTests.Reports.ReportScenarioTests"
dotnet test ./sources/ExpenseHub.slnx --list-tests
```

O primeiro comando roda a suíte unitária inteira (1170 testes aprovados, 0 falhas), sem banco, rede, RabbitMQ nem Docker; o segundo, só os 15 cenários. Os testes operacionais (E01 a E05) ficam na CP3-I06, e quantidade de testes e cobertura percentual não dão pontos.

## Detalhe da CP3-I05: Publicação e execução reproduzível

Issue central: [15](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/15). Branch: `cp3-i05`. Pontos: 15. Estado: Concluída, mergeada na `main` como PR 15 da equipe (merge commit `fb89a4f1e115287df60e9cff7075edd2bfda7a0d`), com score oficial do workflow `code-quality` de 100/100 na execução 78. Os Dockerfiles, o workflow de publicação e o Compose foram exercitados aqui no Docker com imagens construídas localmente. Depois do merge, o workflow `publish-images` terminou na `main` com as duas imagens publicadas com o SHA completo do merge, os pacotes `expensehub-web` e `expensehub-worker` ficaram públicos e o `docker pull` anônimo por SHA completo foi conferido pelo Renan (informado por ele). O `docker compose pull` com o `IMAGE_TAG` final continua pendente até a conferência final (CP3-I06 e CP3-I07), como está em "O que aconteceu depois do merge", mais abaixo.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Duas imagens publicadas e identificadas pelo commit, com testes antes da publicação | 5 | Atendido | `sources/ExpenseHub.Api/Dockerfile` e `sources/ExpenseHub.Worker/Dockerfile` (mesmas bases, usuário não root, commit na variável `Build__Commit` e na label `revision`); `.github/workflows/publish-images.yml` (job `images` com `needs: test`, tag `${GITHUB_SHA}` completa, sem `latest`, login e push só em push na `main`); `DeliveryFilesTests` (22) e quatro mutações que quebraram testes; as duas imagens construídas aqui (415 MB cada, label e variável iguais ao commit); depois do merge, o workflow terminou na `main` com as duas imagens publicadas com o SHA completo do merge, os pacotes ficaram públicos e o `docker pull` anônimo por SHA completo foi conferido pelo Renan (informado por ele) |
| Compose executa as imagens publicadas e o RabbitMQ com configuração documentada | 5 | Atendido na execução local; `docker compose pull` pendente até a conferência final com o `IMAGE_TAG` final (CP3-I06 e CP3-I07) | `compose.yaml` com `${IMAGE_TAG:?...}`, RabbitMQ 4.4.0 por tag e digest, volume nomeado, hostname fixo e healthcheck; README "Rodar com Docker Compose"; subida real aqui com as imagens construídas com os mesmos nomes (71 conferências, nenhuma falha) |
| Banco e armazenamento compatíveis, inicialização reproduzível e segredos externos | 5 | Atendido | Um volume `expensehub-data` em `/data` nos dois contêineres, com a mesma connection string absoluta (WAL entre dois contêineres, `-wal` e `-shm` vistos pelo worker); `Database:MigrateOnStartup` (migrations aplicadas na primeira partida, 4 pendentes, e 0 na segunda); chaves do Data Protection em `expensehub-keys` (o token anterior continuou valendo depois de recriar web e worker); `.env.example` com os segredos em branco; script de qualidade sem FIAP1002; logs dos três serviços sem as credenciais geradas, sem o token e sem a connection string |

**O que foi entregue.**

- **Imagens.** Dois Dockerfiles (contexto: a raiz do repositório). Compilam no SDK `10.0.403` e executam em `aspnet:10.0.14`, os mesmos nos dois, porque o worker herda `Microsoft.AspNetCore.App` da Api. Rodam como o usuário `app` (UID 1654), e `/data` (e `/keys`, no web) são criados e entregues a esse usuário na imagem, para o volume nascer com a posse certa e os dois processos poderem gravar no mesmo arquivo. O `.dockerignore` mantém `bin`, `obj`, bancos e arquivos `.env` fora do contexto.
- **Identificação.** O commit entra por `ARG GIT_COMMIT` em três lugares: `ENV Build__Commit`, a label `org.opencontainers.image.revision` e uma linha de log na partida dos dois processos (`ExpenseHub web started. Image commit: <sha>.`, e `worker`). O valor só é aceito se for hexadecimal de 7 a 64 caracteres, senão aparece `unknown`. O `/health` não mudou.
- **Workflow.** `publish-images.yml`: job `test` (restore, build e test) e job `images` com `needs: test`. Em push na `main`, sem filtro de caminho, constrói as duas imagens, confere pela label que carregam o commit, entra no GHCR com o `GITHUB_TOKEN` e envia `ghcr.io/renan-utida/expensehub-web:<SHA completo>` e `ghcr.io/renan-utida/expensehub-worker:<SHA completo>`. Escreve as referências e os digests no resumo da execução. Nas PRs para a `main` os mesmos builds rodam sem login e sem push. `packages: write` só no job `images`. O `build.yml` do professor não foi tocado.
- **Compose.** `rabbitmq`, `web` e `worker`, com `depends_on` por `service_healthy` (o worker espera o broker e o web), `restart: unless-stopped`, portas só em localhost e AMQP sem porta no host. Variáveis obrigatórias com `${VAR:?mensagem}` (`IMAGE_TAG`, `RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `ADMIN_EMAIL` e `ADMIN_PASSWORD`). O hostname do RabbitMQ é fixo porque o nome do nó nomeia a pasta de dados: sem isso recriar o contêiner perderia a fila.
- **Banco.** A flag `Database:MigrateOnStartup` (desligada por padrão) faz o web aplicar as migrations antes do seed do Admin. O worker nunca migra e espera o web saudável.
- **Chaves.** `DataProtection:KeysPath` (vazia por padrão, sem efeito) leva as chaves para o volume `expensehub-keys`.
- **`.http`.** Ambiente `compose` em `http-client.env.json` (`http://localhost:8080`), sem tokens nem credenciais.

**Decisões.** Estão na tabela "Decisões do CP3" (03/11/2026): flag em vez de bundle, nomes e tag das imagens, bases, workflow, Compose, chaves, evidência da versão e o ajuste do `publish` do worker. Três pontos merecem destaque:

- **O `publish` do worker falhava** com NETSDK1152, porque o `appsettings.json` da Api chega à saída de publicação pela referência de projeto e colide com o do worker. O `build` convivia com isso, o `publish` não. Um alvo no `.csproj` do worker remove só esse item; a checagem de duplicatas do SDK continua ligada, e a imagem do worker traz o `appsettings.json` dele (só RabbitMQ, sem connection string), conferido dentro da imagem.
- **`RABBITMQ_PASSWORD` no Compose.** O script de qualidade lê `...PASSWORD:?...` como `chave: valor` e acusaria FIAP1002 em qualquer linha cuja chave não tenha a palavra senha. A verificação com `:?` fica na linha `RabbitMq__Password`, e a do broker (`RABBITMQ_DEFAULT_PASS`) usa `${RABBITMQ_PASSWORD}` simples; como o Compose interpola o arquivo inteiro, a variável é obrigatória do mesmo jeito (testado com um `.env` vazio).
- **Gatilho de PR no workflow.** É um acréscimo meu: a publicação só roda na `main`, e sem ele os Dockerfiles só seriam exercitados no GitHub depois do merge. Se não for desejado, basta apagar o bloco `pull_request`.

**O que foi medido** (Docker 29.9.2 no Windows, projeto Compose `expensehub-i05`, volumes novos, credenciais geradas na hora e nunca impressas, imagens construídas com os mesmos nomes do Compose):

- `docker build` das duas imagens: o do web passou de primeira; o do worker acusou o NETSDK1152 acima, foi corrigido e passou. Usuário `1654`, `/data` e `/keys` do `app`, `bash` presente e `curl` e `wget` ausentes (por isso o healthcheck do web usa `bash` e `/dev/tcp`), label e `Build__Commit` iguais ao commit, mesma primeira camada nas duas imagens.
- Primeira subida: `rabbitmq` e `web` saudáveis, `worker` iniciado depois; 4 migrations aplicadas, seed do Admin, linha de versão nos dois logs, fila durável (`durable` verdadeiro, `auto_delete` e `exclusive` falsos), RabbitMQ 4.4.0.
- Fluxo: `POST` `Mine` responde `202`, o worker (outro contêiner) leva o relatório a `Completed` com 3 despesas e 175,00; Excel válido; `Global` com 4 e 375,00. `/data` com `expensehub.db`, `-wal` e `-shm`, igual nos dois contêineres, e chaves em `/keys`.
- E02 (worker parado): `202`, `Pending`, mensagem pronta na fila; `up -d worker` e o mesmo id vira `Completed`.
- E05 (`up -d --force-recreate web worker`): contêineres novos, broker intacto; o token emitido antes continuou valendo; o resultado, o `startedAt` e o `completedAt` anteriores ficaram idênticos; label, variável e log de versão iguais ao commit; migrations não reaplicadas (0 pendentes); um relatório novo com o token antigo concluiu.
- Broker recriado (`up -d --force-recreate rabbitmq`) com uma mensagem na fila: a fila durável e a mensagem sobreviveram (efeito do hostname fixo) e o mesmo id concluiu; o web voltou a publicar sem precisar de reinício.
- Logs do web, do worker e do RabbitMQ: nenhuma ocorrência da senha do broker, da senha do Admin, do token nem de `Data Source`.
- Ao final só `docker compose stop`: os contêineres parados e os três volumes do projeto `expensehub-i05` continuam no Docker.

**O que aconteceu depois do merge** (informado pelo Renan; não verifiquei na rede).

1. O workflow `publish-images` terminou na `main` e publicou as duas imagens com o SHA completo do merge (`fb89a4f1e115287df60e9cff7075edd2bfda7a0d`) como tag.
2. Os pacotes `expensehub-web` e `expensehub-worker` foram tornados públicos, e o `docker pull` anônimo por SHA completo foi conferido pelo Renan.
3. **Pendente até a conferência final:** o `docker compose pull` com o `IMAGE_TAG` final. A tag é o SHA completo do último commit da `main`, e cada push na `main` gera imagens novas, então essa conferência só vale com o commit de entrega (CP3-I06 e CP3-I07). O dossiê da CP3-I06 informa o SHA usado nos experimentos, e as referências completas da entrega saem na CP3-I07. Depois do commit final, nada muda.
4. O número da PR (15) e o score oficial do workflow `code-quality` desta issue (100/100, execução 78) já estão aqui e no README.

**Como validar.**

```shell
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
cp .env.example .env     # preencher IMAGE_TAG, RABBITMQ_USER, RABBITMQ_PASSWORD, ADMIN_EMAIL e ADMIN_PASSWORD
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail 80 worker
docker compose stop worker
docker compose up -d worker
docker compose up -d --force-recreate web worker
```

Sem puxar do registry (por exemplo, para testar uma alteração local): construir as duas imagens à mão com os comandos do README ("Construir as imagens à mão"), usar `IMAGE_TAG=local` no `.env` e pular o `docker compose pull`.

**Limites.** Imagens `linux/amd64`; o WAL exige web e worker no mesmo host e disco local (volume nomeado, nunca pasta do Windows); as chaves do Data Protection ficam sem criptografia em repouso no volume; o `RABBITMQ_DEFAULT_*` só vale quando o volume do broker nasce; não há Transactional Outbox.

## Detalhe da CP3-I06: Experimentos e diagnóstico

Issue central: [16](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/16). Branch: `cp3-i06`. Pontos: 10. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| E01 com Excel e E02 correlacionados na mesma solicitação | 3 | Pendente | A preencher ao concluir a issue |
| E03 duplicata depois de reiniciar o worker, sem novo efeito | 2 | Pendente | A preencher ao concluir a issue |
| E04 cenários de autorização reproduzíveis | 2 | Pendente | A preencher ao concluir a issue |
| E05 persistência depois de recriar web e worker | 2 | Pendente | A preencher ao concluir a issue |
| Diagnóstico correto de aceitação, publicação e janela sem Outbox | 1 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. As evidências ficam no [Dossiê dos experimentos](#dossiê-dos-experimentos-e01-a-e05).

## Detalhe da CP3-I07: Qualidade objetiva das alterações

Issue central: [17](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/17). Branch: `cp3-i07`. Pontos: 5. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Sem novos warnings ou diagnósticos atribuíveis ao trabalho | 2 | Pendente | A preencher ao concluir a issue |
| Naming e formatação seguem as regras existentes | 1 | Pendente | A preencher ao concluir a issue |
| Analisadores e verificações não foram enfraquecidos | 1 | Pendente | A preencher ao concluir a issue |
| Higiene da entrega: só fontes, sem binários nem arquivos locais | 1 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. Inclui a comparação com o commit do CP2.

## Detalhe da CP3-EXTRA-LINKEDIN: Publicação individual no LinkedIn

Issue central: [18](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/18). Sem branch nem PR. Pontos: +1 individual. Estado: Pendente.

Atividade opcional e manual: publicação própria no LinkedIn, com texto próprio, sobre a aplicação, e envio do link no Teams, identificando o autor.

## Dossiê dos experimentos (E01 a E05)

| Experimento | Situação | Evidência |
|---|---|---|
| E01 Fluxo normal | Pendente | A preencher na CP3-I06 |
| E02 Worker parado | Pendente | A preencher na CP3-I06 |
| E03 Duplicata | Pendente | A preencher na CP3-I06 |
| E04 Isolamento | Pendente | A preencher na CP3-I06 |
| E05 Persistência e versão | Pendente | A preencher na CP3-I06 |

### Diagnóstico

A preencher na CP3-I06, explicando:

- aceitação e conclusão;
- worker parado e broker indisponível;
- duplicata e novo POST;
- a janela sem Transactional Outbox.

## Entrega

A preencher no fim do checkpoint:

- SHA do commit do CP2 e SHA do commit final do CP3;
- referências completas das duas imagens e a forma de acesso para a correção;
- comando dos testes unitários e comandos para subir o ambiente.
