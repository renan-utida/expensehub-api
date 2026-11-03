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

Issue central: [14](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/14). Branch: `cp3-i04`. Pontos: 15. Estado: Concluída, mergeada na `main` como PR 16 da equipe (merge commit `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`), com score oficial do workflow `code-quality` de 100/100 na execução 81 (da PR) e execução 82 na `main` concluída com sucesso, e `publish-images` concluído na `main` (execução 4), tudo informado pelo Renan. A issue audita a suíte contra a tabela de 13 cenários mínimos, cria só o que faltava, prova por mutação que os testes pegam defeitos nas regras novas do CP3 e documenta o comando. **Nenhum código de produção mudou**: só testes e documentação.

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

Mesmo método da I09 do CP2 (quebrar a regra de propósito, ver o teste falhar, desfazer), com duas diferenças: **nenhuma mutação toca a árvore real** e **não se usa `git restore`**: a mutação roda numa cópia para que a árvore real nunca receba um mutante nem corra risco de perder alterações ainda não commitadas. Um script local de Python, fora do repositório, não versionado e fora do CI:
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

Issue central: [15](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/15). Branch: `cp3-i05`. Pontos: 15. Estado: Concluída, mergeada na `main` como PR 15 da equipe (merge commit `fb89a4f1e115287df60e9cff7075edd2bfda7a0d`), com score oficial do workflow `code-quality` de 100/100 na execução 78. Os Dockerfiles, o workflow de publicação e o Compose foram exercitados aqui no Docker com imagens construídas localmente. Depois do merge, o workflow `publish-images` terminou na `main` com as duas imagens publicadas com o SHA completo do merge, os pacotes `expensehub-web` e `expensehub-worker` ficaram públicos e o `docker pull` anônimo por SHA completo foi conferido pelo Renan (informado por ele). A conferência do `docker compose pull` com o `IMAGE_TAG` final é feita pelo Renan depois do merge da CP3-I07, com o SHA final, e fica registrada no texto de entrega do Teams, não no repositório (veja [Entrega](#entrega)); os experimentos da CP3-I06 já rodaram com as imagens publicadas do SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Duas imagens publicadas e identificadas pelo commit, com testes antes da publicação | 5 | Atendido | `sources/ExpenseHub.Api/Dockerfile` e `sources/ExpenseHub.Worker/Dockerfile` (mesmas bases, usuário não root, commit na variável `Build__Commit` e na label `revision`); `.github/workflows/publish-images.yml` (job `images` com `needs: test`, tag `${GITHUB_SHA}` completa, sem `latest`, login e push só em push na `main`); `DeliveryFilesTests` (22) e quatro mutações que quebraram testes; as duas imagens construídas aqui (415 MB cada, label e variável iguais ao commit); depois do merge, o workflow terminou na `main` com as duas imagens publicadas com o SHA completo do merge, os pacotes ficaram públicos e o `docker pull` anônimo por SHA completo foi conferido pelo Renan (informado por ele) |
| Compose executa as imagens publicadas e o RabbitMQ com configuração documentada | 5 | Atendido na execução local; a conferência do `docker compose pull` com o `IMAGE_TAG` final é feita pelo Renan depois do merge da CP3-I07, com o SHA final, e fica no texto de entrega do Teams, não no repositório (veja [Entrega](#entrega)); os experimentos da CP3-I06 já rodaram com as imagens publicadas do SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` | `compose.yaml` com `${IMAGE_TAG:?...}`, RabbitMQ 4.4.0 por tag e digest, volume nomeado, hostname fixo e healthcheck; README "Rodar com Docker Compose"; subida real aqui com as imagens construídas com os mesmos nomes (71 conferências, nenhuma falha) |
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
3. **Conferência com o `IMAGE_TAG` final:** a tag é o SHA completo do último commit da `main`, e cada push na `main` gera imagens novas, então o `docker compose pull` com o `IMAGE_TAG` final só vale com o commit de entrega. Essa conferência é feita pelo Renan depois do merge da CP3-I07, com o SHA final, e fica registrada no texto de entrega do Teams, não no repositório (veja [Entrega](#entrega)). Os experimentos da CP3-I06 já rodaram com as imagens publicadas do SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` (dossiê da CP3-I06), e as referências completas da entrega estão na seção Entrega. Depois do commit final, nada muda.
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

Issue central: [16](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/16). Branch: `cp3-i06`. Pontos: 10. Estado: Concluída, mergeada na `main` como PR 17 da equipe (merge commit `a41b22071f65ef81b536dd9a6b9d1a28b3c3add2`), com score oficial do workflow `code-quality` de 100/100 na execução 84 (da PR) e execução 85 na `main` concluída com sucesso, e `publish-images` com sucesso na PR (execução 5) e na `main` (execução 6), tudo informado pelo Renan. O arquivo `.http` está completo, os cinco experimentos foram executados pelo Renan em 03/11/2026 no Docker dele, com as imagens publicadas do SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`, e as saídas reais e o diagnóstico estão no [Dossiê](#dossiê-dos-experimentos-e01-a-e05). O commit final de entrega gera imagens próprias (outra tag); os experimentos não foram repetidos com elas.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| E01 com Excel e E02 correlacionados na mesma solicitação | 3 | Atendido | E01: relatório `2e1a02c1-aac4-40ca-b2c9-2e4043209fa3`, `202` com `Location`, `Completed` com 3 itens e 175, Excel `.xlsx` com as mesmas células do JSON e logs do web e do worker pelo id. E02: worker parado, `202`, relatório `56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f` `Pending` com a fila em `1 0`, e o mesmo id `Completed` depois de religar o worker ([E01](#e01-fluxo-normal), [E02](#e02-worker-parado)) |
| E03 duplicata depois de reiniciar o worker, sem novo efeito | 2 | Atendido | Worker reiniciado às 18:18:52Z, mesmo `reportId` republicado pelo painel às 18:24:34Z, log `duplicate message ignored, nothing was changed`, hash do JSON, listagem e hash das células do Excel idênticos, fila `0 0` e uma só conclusão no log ([E03](#e03-duplicata-depois-de-reiniciar-o-worker)) |
| E04 cenários de autorização reproduzíveis | 2 | Atendido | Oito casos com `OK`: `403` do Employee em `Global`, `404` ao consultar e ao baixar o Excel alheio (Employee e Auditor), `202` e 4 itens e 375 do Auditor em `Global`, `404` do Employee para o relatório do Auditor e listagem do Auditor só com o dele ([E04](#e04-isolamento)) |
| E05 persistência depois de recriar web e worker | 2 | Atendido | Web e worker com contêineres novos, RabbitMQ intacto, mesma versão (rótulo `revision` e linha `Image commit` com o SHA `f18cadbc…`), mesmo token ainda válido e hashes do JSON e do Excel iguais antes e depois, sem remover volume ([E05](#e05-persistência-e-versão)) |
| Diagnóstico correto de aceitação, publicação e janela sem Outbox | 1 | Atendido | Quatro partes no [Diagnóstico](#diagnóstico): aceitação e conclusão com a linha do tempo do E01; worker parado, broker indisponível e janela sem Outbox como falhas diferentes; duplicata e novo `POST`; `Ready` e `Unacked` contra `Pending` e `Processing` |

Decisões:

- **Versão dos experimentos.** São usadas as imagens publicadas do SHA completo `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` (merge da CP3-I04, PR 16), com `IMAGE_TAG` igual a esse SHA. Esta branch só traz documentação e o arquivo `.http`; a [prova de que só a documentação mudou depois dos experimentos](#prova-de-que-só-a-documentação-mudou-depois-dos-experimentos), feita na CP3-I07, mostra que a diferença até a entrega é só documentação.
- **Ambiente real.** Projeto Compose `expensehub` do `compose.yaml`, com os serviços reais `web`, `worker` e `rabbitmq`, rodando no Docker do Renan (Git Bash no Windows). Nenhum passo remove volume ou banco: não há `docker compose down`, nem `docker volume rm`, nem `prune`; a recriação de web e worker do E05 troca os contêineres e preserva os volumes.
- **Senhas.** A do Admin não é digitada: o bloco 1 a gera com `openssl` (`Aa1!` mais 32 caracteres hexadecimais, o que cumpre a política padrão do ASP.NET Core Identity usada pelo projeto) e a grava no `.env` sem imprimir. Só usa letras, dígitos e `!`, porque `$` ou `#` seriam alterados pela interpolação do Compose e o Admin só é criado na primeira subida (sem remover volume não haveria como corrigir). A senha das contas de teste é digitada com `read -rs` e validada antes de seguir. Tokens e senhas ficam só em variáveis do shell, nunca em arquivo; o diretório temporário guarda apenas as planilhas baixadas.
- **Dados mínimos e conhecidos.** Quatro contas e cinco despesas (tabela no Dossiê), para que os totais sejam conferíveis à mão: `Mine` da ana = 3 itens e 175,00; `Global` do Auditor = 4 itens e 375,00.
- **Comparação do Excel.** O pacote `.xlsx` é um zip e guarda o horário de gravação das entradas, então dois downloads do mesmo relatório diferem em bytes. A comparação usa o hash do conteúdo de `xl/worksheets/sheet1.xml`.
- **Republicação (E03).** Pelo painel de gerenciamento do RabbitMQ, como a apostila sugere, sem endpoint de negócio e sem alterar o banco.
- **Arquivo `.http`.** Completo (50 requisições nomeadas, sem token nem senha: as credenciais vêm de `http-client.env.json.user`, ignorado pelo git, e os tokens vêm das respostas dos logins). Foi executado de verdade contra uma pilha de ensaio por um executor em Python fora do repositório, que conferiu o código HTTP esperado de cada requisição (50 de 50 sem divergência); não foi aberto no Visual Studio. O `Global` soma as despesas de todos os usuários, então o `.http` só deve rodar depois dos experimentos.
- **Ensaio do roteiro (observação do assistente, não é evidência dos experimentos).** Antes de entregar o roteiro, o assistente o executou de ponta a ponta em um projeto Compose separado (`expensehub-i06dry`), com credenciais descartáveis, portas diferentes e as mesmas imagens publicadas, para corrigir o texto dos blocos. Nesse ensaio o `docker compose pull` das duas imagens com a tag do SHA acima funcionou sem login no GHCR (Docker configurado sem credenciais). No ensaio o passo do painel foi substituído pela API HTTP de gerenciamento do RabbitMQ, que publica a mesma mensagem; o painel em si não foi exercitado pelo assistente (o Renan o usou no E03, e os rótulos da tela bateram com o roteiro).
- **Tentativas que falharam.** Três falhas de colagem no terminal (bloco 1 e bloco 5 por causa da barra de continuação, bloco 3a por uma linha vazia) foram refeitas, com a causa e a ordem real em [Tentativas que falharam](#tentativas-que-falharam-e-foram-refeitas-ordem-real). O roteiro passou a ter a regra "nenhum comando com barra de continuação", e o bloco 1 ganhou uma trava (`cmp -s`) e a conferência `docker compose config --quiet`. Nenhuma das falhas criou contêiner, rede, volume ou relatório.
- **Resultado dos experimentos.** Nenhum experimento revelou defeito de produção. As duas observações que valem registro (o intervalo de 1 a 2 s entre `message received` e `processing started` na primeira mensagem de cada processo do worker, e por que o token continuou valendo depois de recriar o web) foram conferidas no código e estão em [Observações que os experimentos levantaram](#observações-que-os-experimentos-levantaram).
- **Limites.** O E04 não cobre o `409` do Excel de um relatório próprio ainda não `Completed` (não faz parte dos cinco experimentos; está nos testes unitários, por exemplo `ReportServiceExcelTests.GetExcelAsync_OwnReportNotCompleted_IsAConflict` e `ReportsControllerTests.ExcelAsync_NotCompleted_Returns409`); o painel do RabbitMQ não tem captura de tela no dossiê, só a descrição do passo e o log do worker que confirma a hora da publicação; o `.http` não foi aberto no Visual Studio.

As evidências ficam no [Dossiê dos experimentos](#dossiê-dos-experimentos-e01-a-e05).

## Detalhe da CP3-I07: Qualidade objetiva das alterações

Issue central: [17](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/17). Branch: `cp3-i07`. Pontos: 5. Estado: Concluída, PR 18 da equipe. A issue não muda código de produção, pacotes, migrations, workflows nem configuração de qualidade. Ela mede o CP3 contra o commit do CP2 (`2e74619a311b38dbcc959888c8f1358b6877e9a1`), documenta o resultado, remove a pasta temporária `andamento/` (e as regras que a citavam no `CLAUDE.md`) e preenche a seção [Entrega](#entrega).

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Sem novos warnings ou diagnósticos atribuíveis ao trabalho | 2 | Atendido | Mesmos comandos nas duas árvores (tabela abaixo): `dotnet build --no-incremental` com 0 avisos e 0 erros no CP2 e na entrega, e o script de qualidade com a mesma lista de achados (só o informativo `FIAP0002`, Gitleaks ignorado), nenhum ID novo. A varredura do diff contra o CP2 não acha supressão no código da equipe; as únicas linhas são `#nullable disable` e `#pragma warning disable/restore 612, 618` na migration `AddReportRequests`, que o `dotnet ef` gera e que o `.editorconfig` trata como código gerado (`[**/Migrations/*.cs]`, `generated_code = true`); as quatro migrations do CP2 têm o mesmo cabeçalho. |
| Naming e formatação seguem as regras existentes | 1 | Atendido | `dotnet format ./sources/ExpenseHub.slnx --verify-no-changes` termina sem alterações (saída 0) no CP2 e na entrega; categoria `style` do script em 20/20; build com `EnforceCodeStyleInBuild` sem `IDE1006` nem outro `IDE`. Conferência dos nomes dos 96 arquivos `.cs` adicionados desde o CP2 contra o padrão do CP2: nome do arquivo igual ao tipo declarado (96 de 96; no CP2, 110 de 111), namespace igual à pasta (94 de 96; as duas exceções são as migrations geradas sem namespace, como as sete do CP2), classes de teste terminando em `Tests` (32 de 32; no CP2, 29 de 29) e métodos de teste no padrão `Metodo_Condicao_Resultado` (321 de 321; no CP2, 288 de 288). As pastas novas (`Reports` e `Deployment` na Api e nos testes, e o projeto `ExpenseHub.Worker`) seguem a mesma organização das pastas do CP2. |
| Analisadores e verificações não foram enfraquecidos | 1 | Atendido | `git diff --stat 2e74619a311b38dbcc959888c8f1358b6877e9a1 HEAD -- .editorconfig Directory.Build.props scripts docs .github/workflows/build.yml dotnet-tools.json` sai vazio (o workflow do professor, o script, os analisadores, o `Directory.Build.props` e as regras de estilo são idênticos aos do CP2). Nos `csproj`, a única mudança foi acrescentar `DocumentFormat.OpenXml` 3.5.1 e `RabbitMQ.Client` 7.2.2 na Api; o `ExpenseHub.Worker.csproj` novo herda o `Directory.Build.props` e não tem `NoWarn`, `TreatWarningsAsErrors` nem supressão, e os Dockerfiles só passam `-p:UseAppHost=false`. O workflow novo `publish-images.yml` é aditivo e não relaxa nada: o job que publica depende (`needs: test`) do job que roda restore, build e `dotnet test`. |
| Higiene da entrega: só fontes, sem binários nem arquivos locais | 1 | Atendido | `git ls-files` não lista `bin/`, `obj/`, `.vs/`, `artifacts/`, bancos (`.db`, `.sqlite`, `-wal`, `-shm`), chaves (`.pem`, `.pfx`, `.key`), planilhas, `.dll`/`.exe`/`.pdb` nem `.env` (o único arquivo `.env*` rastreado é o `.env.example`, com os segredos em branco); nenhum arquivo rastreado passa de 500 KB (o maior é este `CHECKPOINT-3.md`) e todos os rastreados são texto (`git grep -I`). O `.gitignore` cobre `bin/`, `obj/`, `.vs/`, `*.env`, `*.db`, `*.db-wal`, `*.db-shm`, `*.sqlite*`, `artifacts/`, `*.user` e `*.log` (conferido com `git check-ignore` em um caminho de cada tipo). `git status` não mostra arquivo local. |

Comparação com o commit do CP2, medida em cópias limpas (CP2 exportado com `git archive`; entrega copiada só com os arquivos rastreados, sem `.env`):

| Medida | CP2 (`2e74619`) | Entrega |
|---|---|---|
| Projetos compilados | `ExpenseHub.Api` e `ExpenseHub.UnitTests` | `ExpenseHub.Api`, `ExpenseHub.UnitTests` e `ExpenseHub.Worker` |
| `dotnet build ./sources/ExpenseHub.slnx --no-incremental` | 0 avisos, 0 erros | 0 avisos, 0 erros |
| `dotnet test ./sources/ExpenseHub.slnx` | 611 aprovados, 0 falhas | 1170 aprovados, 0 falhas |
| `dotnet format ./sources/ExpenseHub.slnx --verify-no-changes` | sem alterações (saída 0) | sem alterações (saída 0) |
| `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks` | 100/100 (build, security, hygiene, style e best-practices em 20) | 100/100 (build, security, hygiene, style e best-practices em 20) |
| Achados do script | `FIAP0002` (informativo) | `FIAP0002` (informativo) |

Decisões e como validar:

- **Cópias limpas.** As medições usam cópias só dos arquivos rastreados porque na pasta do Renan o `.env` real, ignorado pelo git e criado para os experimentos, faz o script local marcar 98 pelo `FIAP2102` (possível segredo em arquivo local). O CI não vê esse arquivo, e o score geral não é uma nota diferencial: o que conta aqui é a lista de achados comparada com a do CP2.
- **O que não foi feito para "melhorar" a medição.** Nenhum analisador, severidade, regra de estilo, `NoWarn` ou arquivo foi removido ou alterado; o `.editorconfig`, o `Directory.Build.props`, o `scripts/`, o `docs/` e o `build.yml` seguem idênticos aos do CP2.
- **Pasta `andamento/`.** Os arquivos de trabalho (estado, textos de PR e as fontes da especificação) foram removidos nesta issue, junto com as regras do `CLAUDE.md` que os citavam. A linha `andamento` do `.dockerignore` fica: é inofensiva e mantém a diferença até a entrega só em documentação.
- **Prova de que só a documentação mudou depois dos experimentos:** veja a subseção abaixo.

Comandos para repetir a comparação (a partir da raiz do repositório; `N:` e `M:` são pastas curtas só para as cópias):

```shell
git diff --stat 2e74619a311b38dbcc959888c8f1358b6877e9a1 HEAD -- .editorconfig Directory.Build.props scripts docs .github/workflows/build.yml dotnet-tools.json
git archive 2e74619a311b38dbcc959888c8f1358b6877e9a1 | tar -x -C N:/    # CP2 numa pasta vazia; depois: git init && git add -A
git ls-files -z | tar --null -T - -cf - | tar -xf - -C M:/              # entrega, só os rastreados; depois: git init && git add -A
dotnet build ./sources/ExpenseHub.slnx --no-incremental                  # em cada pasta
dotnet test ./sources/ExpenseHub.slnx
dotnet format ./sources/ExpenseHub.slnx --verify-no-changes
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

### Prova de que só a documentação mudou depois dos experimentos

Os experimentos rodaram com as imagens publicadas do SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` (merge da CP3-I04), e o commit final de entrega gera imagens próprias, com outra tag. Para mostrar que a diferença entre os dois é só documentação, o `git diff` abaixo compara a entrega com esse SHA **por exclusão**: o caminho exclui os arquivos `.md`, o arquivo `.http` de requisições e a pasta de trabalho `andamento/` (removida nesta issue). Se a saída vier vazia, nenhum outro arquivo mudou: nem código, nem testes, nem Dockerfiles, nem `compose.yaml`, nem workflows, nem configuração.

Comando executado na entrega (índice da branch `cp3-i07`, depois da remoção de `andamento/`); a saída é vazia:

```shell
git diff --cached --stat f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f -- . ':(exclude)*.md' ':(exclude)sources/ExpenseHub.Api/ExpenseHub.Api.http' ':(exclude)andamento'
```

Saída:

```
(vazia)
```

Caminhos que mudaram desde o SHA dos experimentos (`git diff --cached --name-status f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`):

```
M	CHECKPOINT-3.md
M	CLAUDE.md
M	README.md
D	andamento/PR-CP3-00.md
D	andamento/PR-CP3-I01.md
D	andamento/PR-CP3-I02.md
D	andamento/PR-CP3-I03.md
D	andamento/PR-CP3-I04.md
D	andamento/PR-CP3-I05.md
D	andamento/PROGRESSO.md
D	andamento/requisitos/apostila-cp3.html
D	andamento/requisitos/issues-cp3.md
D	andamento/requisitos/slides-cp3.html
M	sources/ExpenseHub.Api/ExpenseHub.Api.http
```

Só documentação (`.md`), o arquivo `.http` e a remoção da pasta de trabalho `andamento/`. A linha `andamento` do `.dockerignore` continua lá e não conta como mudança. As linhas de cada `.md` não são contadas aqui porque mudam a cada commit.

Para repetir depois do merge, com o SHA final de entrega (o merge commit da PR da CP3-I07 na `main`, informado no texto do Teams), a partir de um clone com a `main` atualizada:

```shell
git diff --stat f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f <SHA final completo> -- . ':(exclude)*.md' ':(exclude)sources/ExpenseHub.Api/ExpenseHub.Api.http' ':(exclude)andamento'
git diff --name-status f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f <SHA final completo>
```

O primeiro deve sair vazio e o segundo deve listar os mesmos caminhos acima.

## Detalhe da CP3-EXTRA-LINKEDIN: Publicação individual no LinkedIn

Issue central: [18](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/18). Sem branch nem PR. Pontos: +1 individual. Estado: Pendente (manual).

Atividade opcional e manual: publicação própria no LinkedIn, com texto próprio, sobre a aplicação, e envio do link no Teams, identificando o autor.

## Dossiê dos experimentos (E01 a E05)

Os cinco experimentos foram executados pelo Renan no Docker dele, em 03/11/2026, com o roteiro abaixo (objetivo, pré-condições, comandos, o que observar e o resultado esperado pelo contrato). Cada campo **"Saída real colada pelo Renan"** traz o texto do terminal como veio: ficaram de fora só as linhas de prompt, os comandos ecoados (que estão nos blocos de comando acima de cada saída) e as linhas em branco que a colagem duplicou. Os trechos que o Renan escreveu entre colchetes no arquivo de saídas eram resumos dele, não saída de terminal, e não são citados como saída. O arquivo de saídas original não foi copiado para o repositório.

| Experimento | Situação | Evidência |
|---|---|---|
| E01 Fluxo normal | Executado, resultado e Excel conferidos | [E01](#e01-fluxo-normal): relatório `2e1a02c1-aac4-40ca-b2c9-2e4043209fa3`, 3 itens e 175.00, planilha igual ao JSON |
| E02 Worker parado | Executado, mesmo id retomado | [E02](#e02-worker-parado): relatório `56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f`, fila `1 0` com o worker parado e `Completed` depois |
| E03 Duplicata | Executado, sem novo efeito | [E03](#e03-duplicata-depois-de-reiniciar-o-worker): três hashes e a listagem iguais, log de duplicata, uma só conclusão |
| E04 Isolamento | Executado, 8 de 8 | [E04](#e04-isolamento): `403`, `404` e `202` como esperado |
| E05 Persistência e versão | Executado, mesmo token ainda valeu | [E05](#e05-persistência-e-versão): contêineres novos, mesma versão, mesmos hashes |

Houve tentativas que falharam por causa da colagem no terminal e foram refeitas (os blocos 1 e 5, o bloco 2 como consequência do 1, e uma leitura de senha recusada no 3a); a causa e a ordem real estão em [Tentativas que falharam](#tentativas-que-falharam-e-foram-refeitas-ordem-real). Nenhuma delas alterou o sistema além do que está dito ali. Nenhum experimento revelou defeito de produção.

### Ambiente e versão dos experimentos

| Item | Valor |
|---|---|
| SHA completo das imagens (`IMAGE_TAG`) | `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` (merge da CP3-I04, PR 16) |
| Imagem do web | `ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` |
| Imagem do worker | `ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` |
| Projeto Compose | `expensehub` (nome do `compose.yaml`), serviços `web`, `worker` e `rabbitmq` |
| Máquina, ferramentas e versões | Git Bash no Windows; Docker 29.9.2 (build a82e528), Docker Compose v5.6.1, Python 3.12.2; `docker`, `curl`, `python`, `sha256sum`, `unzip`, `openssl`, `cygpath`, `netstat` e `git` presentes (bloco 0). RabbitMQ 4.4.0 em Erlang 29.1.2 (lidos no painel pelo Renan, sem captura de tela) |
| Branch e HEAD do repositório na execução | `cp3-i06`, HEAD `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` (igual ao SHA das imagens, porque a branch ainda não tinha commits próprios) |
| Data e hora da execução | 03/11/2026, de cerca de 14:40 a 15:35 em horário de Brasília (o arquivo de saídas informa "14:4x a 15:35"); os logs estão em UTC, e os contêineres subiram às 17:51:54Z (RabbitMQ), 17:52:00Z (web) e 17:52:06Z (worker) |

**Versão dos experimentos e commit final.** Os experimentos rodaram com as imagens publicadas do SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`. O commit final de entrega gera imagens próprias: o workflow `publish-images` publica web e worker com a tag do SHA completo de cada commit da `main`, então as imagens finais têm outra tag e os experimentos não foram repetidos com elas. A branch `cp3-i06` só traz documentação e o `.http`, e a [prova de que só a documentação mudou depois dos experimentos](#prova-de-que-só-a-documentação-mudou-depois-dos-experimentos), feita na CP3-I07, mostra do SHA dos experimentos até a entrega que a diferença é só documentação.

### Regras de sanitização

- Os blocos foram escritos para não imprimir senha, token, cabeçalho de autorização nem connection string. Mesmo assim, antes de colar uma saída, o Renan confere se não há nada disso nela.
- Se uma colagem trouxer um segredo, ela não é gravada no dossiê: o assistente avisa e pede para refazer a execução do bloco sanitizada.
- O arquivo `.env` nunca é lido nem colado. Os comandos de `docker inspect` usam sempre `--format` com campos específicos (nunca a lista de variáveis de ambiente), e `docker compose config` só roda com `--quiet`.
- Imagens são identificadas pela tag (o SHA completo), nunca por digest.
- Instantes ficam em UTC como a API devolve. Identificadores de relatório são GUIDs gerados pela aplicação e podem aparecer.
- Antes de gravar, o arquivo de saídas do Renan foi varrido por `Data Source`, `eyJ` (início de token em base64), `Bearer ` seguido de valor, textos longos em base64 ou hexadecimal e valores literais depois de `password` ou `senha`: nada foi encontrado (as únicas ocorrências são os nomes das variáveis nas mensagens de erro do Compose, os `traceId` dos erros `404` e o texto do comando que gera a senha do Admin). A senha das contas de teste não é registrada. Uma tentativa que imprimiu um caminho de usuário do Windows em um erro do Python foi resumida em vez de copiada.

### Dados de teste conhecidos

Quatro contas, criadas por registro (o `/register` nunca recebe papéis) e com os papéis dados pelo Admin; todas usam a mesma senha, digitada no bloco 3a e nunca versionada.

| Conta | Papel | Uso |
|---|---|---|
| Admin do `.env` | Admin | concede os papéis |
| `ana.i06@example.test` | Employee | E01, E02, E03, E05 e alvo do E04 |
| `bruno.i06@example.test` | Employee | segundo Employee do E04, dono de um relatório alheio e de uma despesa |
| `caio.i06@example.test` | Auditor | E04 (`Global`) |
| `gestor.i06@example.test` | Approver e Finance | aprova e paga as despesas dos outros (ninguém aprova nem paga a própria) |

| Dono | Valor | Data da despesa | Estado final |
|---|---:|---|---|
| ana | 100,00 | 2026-09-01 | Approved |
| ana | 50,00 | 2026-09-15 | Approved |
| ana | 25,00 | 2026-09-30 | Paid |
| bruno | 200,00 | 2026-09-10 | Approved |
| ana | 500,00 | 2026-10-01 | Paid (fora do período) |

Período dos relatórios: `fromDate` 2026-09-01 e `toDate` 2026-09-30, com os dois limites inclusivos. Esperado pelo contrato:

- `Mine` da ana: 3 itens e 175,00 (Approved 2 e 150,00; Paid 1 e 25,00; Draft, Submitted e Rejected com 0 e 0,00).
- `Global` do caio: 4 itens e 375,00 (Approved 3 e 350,00; Paid 1 e 25,00; os demais com zeros). Esses números só valem num banco sem outras despesas de setembro de 2026, porque o `Global` soma todos os usuários.

### Preparação (blocos 0 a 3d)

Todos os blocos rodam no Git Bash, na raiz do repositório, **no mesmo terminal** (as variáveis passam de um bloco para o outro). Cada bloco começa com `set +x; set +H` para que o `!` não dispare expansão de histórico. Cole um bloco por vez e espere terminar.

**Regra do roteiro: nenhum comando usa barra de continuação (`\`) no fim da linha.** Cada comando cabe em uma única linha. A colagem no terminal pode inserir linhas em branco entre as linhas, e uma barra invertida seguida de linha em branco corta o comando ao meio: foi o que quebrou o bloco 1 (o `sed -i \`) e o bloco 5 (o `curl ... \`) na primeira tentativa. Comandos compostos (`if`, `for`, `while`, funções) podem ocupar várias linhas, porque o Bash espera o `fi`, o `done` ou a chave de fechamento, e todos os que foram colados assim funcionaram. As versões dos blocos 1 e 5 abaixo são as de uma linha por comando que funcionaram.

**Bloco 0: ferramentas, versões, portas e arquivo de ambiente.**

```bash
set +x; set +H
SHA=f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
ENVFILE="${ENVFILE:-.env}"
BASE="${BASE:-http://localhost:8080}"
PORTAS="${PORTAS:-8080 15672}"
echo "== ferramentas =="
faltou=0
for t in docker curl python sha256sum unzip openssl cygpath netstat git; do
  if command -v "$t" > /dev/null 2>&1; then echo "ok     $t"; else echo "FALTA  $t"; faltou=1; fi
done
[ "$faltou" = 0 ] && echo "todas as ferramentas existem" || echo "ATENCAO: instale o que falta antes de seguir"
echo "== versões =="
docker --version
docker compose version
python --version
echo "== repositório =="
if [ -f compose.yaml ] && [ -f .env.example ]; then echo "ok     compose.yaml e .env.example na pasta atual"; else echo "ERRO   rode a partir da raiz do repositório"; fi
echo "branch: $(git branch --show-current)"
echo "HEAD:   $(git rev-parse HEAD)"
echo "SHA das imagens dos experimentos: $SHA"
echo "== portas (0 linhas = livre) =="
for p in $PORTAS; do echo "porta $p: $(netstat -ano | grep -E ":$p[[:space:]]" | wc -l) linha(s) no netstat"; done
echo "== arquivo de ambiente =="
if [ "$ENVFILE" = ".env" ]; then git check-ignore -v .env; fi
if [ -f "$ENVFILE" ]; then echo "$ENVFILE já existe (o bloco 1 não vai sobrescrever)"; else echo "$ENVFILE ainda não existe"; fi
```

**Bloco 1: cria o `.env` a partir do `.env.example`, sem digitar nada e sem imprimir valores.** Recusa se o `.env` já existir, porque a senha do Admin só vale na primeira subida. As duas senhas (Admin e broker) são geradas com `openssl` e gravadas direto no arquivo. A primeira linha de lógica é uma trava: ela apaga o `.env` **somente** se ele for byte a byte igual ao `.env.example` (a cópia em branco que sobrou de uma tentativa que falhou) e nunca apaga um `.env` preenchido. No fim, `docker compose config --quiet` confirma que o Compose aceita o arquivo antes do bloco 2.

```bash
set +x; set +H
SHA=f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
ENVFILE="${ENVFILE:-.env}"
if [ -f "$ENVFILE" ] && cmp -s .env.example "$ENVFILE"; then rm "$ENVFILE"; echo "removido: $ENVFILE era a cópia em branco do exemplo (sobra da tentativa anterior)"; fi
if [ -f "$ENVFILE" ]; then echo "RECUSADO: $ENVFILE já existe e não será sobrescrito (a senha do Admin só vale na primeira subida)."; else cp .env.example "$ENVFILE"; novo_admin="$(printf '%s%s' 'Aa1!' "$(openssl rand -hex 16)")"; novo_broker="$(openssl rand -hex 16)"; sed -i -e "s/^IMAGE_TAG=.*/IMAGE_TAG=$SHA/" -e "s/^RABBITMQ_USER=.*/RABBITMQ_USER=expensehub/" -e "s/^RABBITMQ_PASSWORD=.*/RABBITMQ_PASSWORD=$novo_broker/" -e "s/^ADMIN_EMAIL=.*/ADMIN_EMAIL=admin@expensehub.local/" -e "s/^ADMIN_PASSWORD=.*/ADMIN_PASSWORD=$novo_admin/" "$ENVFILE"; unset novo_admin novo_broker; echo "criado: $ENVFILE (nenhum valor foi impresso)"; fi
echo "== campos obrigatórios =="
for k in IMAGE_TAG RABBITMQ_USER RABBITMQ_PASSWORD ADMIN_EMAIL ADMIN_PASSWORD; do if grep -q "^$k=." "$ENVFILE"; then echo "ok     $k preenchido"; else echo "FALTA  $k em branco"; fi; done
if [ "$(sed -n 's/^IMAGE_TAG=//p' "$ENVFILE")" = "$SHA" ]; then echo "ok     IMAGE_TAG é o SHA dos experimentos"; else echo "ERRO   IMAGE_TAG diferente do SHA dos experimentos"; fi
if [ "$ENVFILE" = ".env" ]; then git check-ignore -v .env; fi
if docker compose config --quiet 2>/dev/null; then echo "ok     o Compose aceitou o .env (config --quiet)"; else echo "ERRO   o Compose recusou o .env: não rode o bloco 2"; fi
```

O arquivo de saídas do Renan mostra a execução desta versão com os comandos compostos abreviados por `...`; o texto acima é a reconstrução dessas partes abreviadas: junta, com `;`, os mesmos comandos do bloco original, mais três acréscimos que a saída da segunda tentativa mostra por inteiro (a trava `cmp -s`, a definição de `SHA` e de `ENVFILE` no início e o `docker compose config --quiet` no fim). Se o texto exato digitado pelo Renan for necessário, ele está no histórico do terminal dele. Antes de entregar esta versão, o assistente a executou em um arquivo de ambiente descartável fora do repositório: ela recriou o `.env` a partir de uma cópia em branco, recusou sobrescrever na segunda vez, o Compose aceitou o arquivo e a saída não trouxe nenhum valor gerado. Esse teste não é evidência dos experimentos.

**Bloco 2: baixa as imagens publicadas, sobe a pilha, confere versão e saúde.** O progresso das camadas fica de fora da saída; linhas de erro continuam aparecendo.

```bash
set +x; set +H
echo "== pull (uma linha por imagem; o progresso das camadas fica de fora, os erros não) =="
docker compose pull 2>&1 | grep -E '^ Image |[Ee]rror|denied|unauthorized|not found|required variable' | sed 's/@sha256:[0-9a-f]*//'
echo "== subir =="
docker compose up -d 2>&1 | sed 's/@sha256:[0-9a-f]*//'
echo "== aguardando o web ficar saudável (até 3 minutos) =="
fim=$((SECONDS+180)); saude=""
while [ $SECONDS -lt $fim ]; do
  saude=$(docker inspect --format '{{.State.Health.Status}}' "$(docker compose ps -q web)" 2>/dev/null)
  [ "$saude" = "healthy" ] && break
  sleep 3
done
echo "web: $saude"
echo "== serviços =="
docker compose ps -a --format '{{.Service}} | estado={{.State}} | saude={{.Health}} | imagem={{.Image}}' | sed 's/@sha256:[0-9a-f]*//'
echo "== versão em execução (label, imagem, início) =="
for s in web worker; do
  docker inspect --format "$s | {{.Config.Image}} | revision={{index .Config.Labels \"org.opencontainers.image.revision\"}} | iniciado={{.State.StartedAt}}" "$(docker compose ps -q $s)"
done
echo "== linha de log da versão =="
docker compose logs web worker 2>&1 | grep "Image commit"
echo "== health =="
curl -s "$BASE/health"; echo
```

**Bloco 3a: senha das contas de teste.** Cole **só este bloco**, digite a senha (ela não aparece na tela) e dê Enter. O bloco valida a senha e pede de novo, sem ecoar, se ela não servir. Como a senha não fica em arquivo, pode ser digitada de novo se o terminal for fechado.

```bash
set +x; set +H
echo "Regras: 12 ou mais caracteres, com maiúscula, minúscula, dígito e símbolo; sem cifrão, jogo da velha, aspas, barra invertida nem espaço."
while :; do
  read -rs -p "Senha das contas de teste: " TEST_PW; echo
  motivo=""
  [ "${#TEST_PW}" -ge 12 ] || motivo="tem menos de 12 caracteres"
  printf '%s' "$TEST_PW" | LC_ALL=C grep -q '[A-Z]' || motivo="${motivo:-não tem letra maiúscula}"
  printf '%s' "$TEST_PW" | LC_ALL=C grep -q '[a-z]' || motivo="${motivo:-não tem letra minúscula}"
  printf '%s' "$TEST_PW" | LC_ALL=C grep -q '[0-9]' || motivo="${motivo:-não tem dígito}"
  printf '%s' "$TEST_PW" | LC_ALL=C grep -q '[^A-Za-z0-9]' || motivo="${motivo:-não tem símbolo}"
  case $TEST_PW in *[\$\#\"\'\\\ ]*) motivo="tem um caractere proibido (cifrão, jogo da velha, aspas, barra invertida ou espaço)";; esac
  if [ -z "$motivo" ]; then echo "senha aceita (${#TEST_PW} caracteres)"; break; fi
  echo "recusada: $motivo. Digite de novo."
done
```

**Bloco 3b: funções auxiliares e login do Admin.** A senha do Admin é lida do arquivo de ambiente para uma variável do shell, sem imprimir. O token fica em variável e nunca é impresso. O diretório temporário guarda só as planilhas baixadas.

```bash
set +x; set +H
ADMIN_EMAIL="$(sed -n 's/^ADMIN_EMAIL=//p' "$ENVFILE")"
ADMIN_PW="$(sed -n 's/^ADMIN_PASSWORD=//p' "$ENVFILE")"
W="$(mktemp -d)"; WW="$(cygpath -m "$W")"
PERIODO='"fromDate":"2026-09-01","toDate":"2026-09-30"'

# api MÉTODO CAMINHO TOKEN [CORPO]: resposta em RESP, cabeçalhos em HDRS, código em CODE (nada vai para arquivo)
api() {
  local m=$1 p=$2 t=$3 b=${4:-} out
  local args=(-s -i -w '\n%{http_code}' -X "$m" "$BASE$p")
  [ -n "$t" ] && args+=(-H "Authorization: Bearer $t")
  [ -n "$b" ] && args+=(-H 'Content-Type: application/json' -d "$b")
  out=$(curl "${args[@]}")
  CODE=${out##*$'\n'}
  out=${out%$'\n'*}
  HDRS=${out%%$'\r\n\r\n'*}
  RESP=${out#*$'\r\n\r\n'}
}
# jf CAMPO: primeiro valor de texto do campo na última resposta
jf() { printf '%s' "$RESP" | grep -o "\"$1\":\"[^\"]*\"" | head -1 | cut -d'"' -f4; }
# login E-MAIL SENHA: o token fica em TOK (nunca é impresso)
login() { api POST /login '' "{\"email\":\"$1\",\"password\":\"$2\"}"; TOK=$(jf accessToken); }
# wait_status TOKEN ID ESTADO SEGUNDOS: espera o estado e imprime o último visto
wait_status() {
  local fim=$((SECONDS+$4)) atual=""
  while [ $SECONDS -lt $fim ]; do
    api GET "/api/reports/$2" "$1"; atual=$(jf status)
    [ "$atual" = "$3" ] && break
    sleep 1
  done
  printf '%s' "$atual"
}
# chk DESCRIÇÃO ESPERADO OBTIDO
chk() {
  local r=DIVERGE; [ "$2" = "$3" ] && r=OK
  printf '%-66s esperado=%-6s obtido=%-6s %s\n' "$1" "$2" "$3" "$r"
}
# hash curto das células de uma planilha (o zip tem horário de gravação, então não se compara o arquivo)
sheet_hash() { unzip -p "$1" xl/worksheets/sheet1.xml | sha256sum | cut -c1-16; }
# número de relatórios na última resposta de listagem
count_ids() { printf '%s' "$RESP" | grep -o '"id":"' | wc -l; }

PY_SHOW=$(cat <<'PY'
import json, sys
from decimal import Decimal
d = json.load(sys.stdin)
r = d.get('result') or {}
print('status=%s scope=%s createdAt=%s startedAt=%s completedAt=%s' % (d['status'], d['scope'], d['createdAt'], d.get('startedAt'), d.get('completedAt')))
if d.get('error'):
    print('error=%s' % d['error'])
if r:
    print('totalCount=%s totalAmount=%s generatedAt=%s' % (r['totalCount'], r['totalAmount'], r['generatedAt']))
    for s in r['byStatus']:
        print('  %-9s count=%s amount=%s' % (s['status'], s['count'], s['amount']))
    if len(sys.argv) > 2:
        ok = r['totalCount'] == int(sys.argv[1]) and Decimal(str(r['totalAmount'])) == Decimal(sys.argv[2])
        print('esperado %s itens e %s: %s' % (sys.argv[1], sys.argv[2], 'OK' if ok else 'DIVERGE'))
PY
)
PY_XLSX=$(cat <<'PY'
import sys, zipfile
import xml.etree.ElementTree as ET
ns = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
root = ET.fromstring(zipfile.ZipFile(sys.argv[1]).read('xl/worksheets/sheet1.xml'))
for row in root.iter(ns + 'row'):
    out = []
    for c in row.findall(ns + 'c'):
        inline = c.find(ns + 'is')
        value = c.find(ns + 'v')
        if inline is not None:
            out.append('%s=%s[texto]' % (c.get('r'), inline.find(ns + 't').text))
        else:
            out.append('%s=%s[numero]' % (c.get('r'), value.text if value is not None else ''))
    print(' | '.join(out))
PY
)

login "$ADMIN_EMAIL" "$ADMIN_PW"; T_ADMIN=$TOK
echo "login do Admin: HTTP $CODE"
```

**Bloco 3c: cadastro das quatro contas, papéis pelo Admin e login de cada uma.** Cole **uma vez só**. Se for repetido, o cadastro devolve `409` (esperado), mas nunca repita o bloco 3d.

```bash
set +x; set +H
for u in ana bruno caio gestor; do
  api POST /register '' "{\"email\":\"$u.i06@example.test\",\"password\":\"$TEST_PW\"}"
  printf 'cadastro %-24s HTTP %s\n' "$u.i06@example.test" "$CODE"
done
api GET /api/admin/users "$T_ADMIN"; echo "lista de usuários (Admin): HTTP $CODE"
USERS="$RESP"
uid() { printf '%s' "$USERS" | grep -o "\"id\":\"[^\"]*\",\"email\":\"$1\"" | cut -d'"' -f4; }
grant() { api PUT "/api/admin/users/$(uid "$1")/roles" "$T_ADMIN" "{\"roles\":$2}"; printf 'papéis   %-24s %-26s HTTP %s\n' "$1" "$2" "$CODE"; }
grant ana.i06@example.test '["Employee"]'
grant bruno.i06@example.test '["Employee"]'
grant caio.i06@example.test '["Auditor"]'
grant gestor.i06@example.test '["Approver","Finance"]'
for u in ana bruno caio gestor; do
  login "$u.i06@example.test" "$TEST_PW"
  case $u in ana) T_ANA=$TOK;; bruno) T_BRUNO=$TOK;; caio) T_CAIO=$TOK;; gestor) T_GESTOR=$TOK;; esac
  printf 'login    %-24s HTTP %s\n' "$u.i06@example.test" "$CODE"
done
```

**Bloco 3d: cria, envia, aprova e paga as cinco despesas de teste.** Cole **uma vez só**: repetir duplicaria as despesas e mudaria os totais esperados.

```bash
set +x; set +H
# despesa DONO TOKEN-DO-DONO VALOR DATA ESTADO-FINAL (Approved ou Paid); o gestor aprova e paga
despesa() {
  local c1 c2 c3 c4=- id
  api POST /api/expenses "$2" "{\"description\":\"Despesa de teste I06 $3\",\"amount\":$3,\"expenseDate\":\"$4\"}"; c1=$CODE
  id=$(jf id)
  api POST "/api/expenses/$id/submit" "$2"; c2=$CODE
  api POST "/api/expenses/$id/approve" "$T_GESTOR"; c3=$CODE
  if [ "$5" = Paid ]; then api POST "/api/expenses/$id/pay" "$T_GESTOR"; c4=$CODE; fi
  printf '%-6s %8s %s %-8s criar=%s enviar=%s aprovar=%s pagar=%s\n' "$1" "$3" "$4" "$5" "$c1" "$c2" "$c3" "$c4"
}
despesa ana   "$T_ANA"   100.00 2026-09-01 Approved
despesa ana   "$T_ANA"    50.00 2026-09-15 Approved
despesa ana   "$T_ANA"    25.00 2026-09-30 Paid
despesa bruno "$T_BRUNO" 200.00 2026-09-10 Approved
despesa ana   "$T_ANA"   500.00 2026-10-01 Paid
```

Esperado pelo contrato: `cadastro` com `201`, `papéis` com `200`, `login` com `200` e, nas despesas, `criar=201 enviar=200 aprovar=200` e `pagar=200` nas duas pagas (`pagar=-` nas aprovadas, que não são pagas).

**Saída real colada pelo Renan, blocos 0 a 3d** (as execuções que passaram; as que falharam estão em [Tentativas que falharam](#tentativas-que-falharam-e-foram-refeitas-ordem-real)).

Bloco 0:

```
== ferramentas ==
ok     docker
ok     curl
ok     python
ok     sha256sum
ok     unzip
ok     openssl
ok     cygpath
ok     netstat
ok     git
todas as ferramentas existem
== versões ==
Docker version 29.9.2, build a82e528
Docker Compose version v5.6.1
Python 3.12.2
== repositório ==
ok     compose.yaml e .env.example na pasta atual
branch: cp3-i06
HEAD:   f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
SHA das imagens dos experimentos: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
== portas (0 linhas = livre) ==
porta 8080: 0 linha(s) no netstat
porta 15672: 0 linha(s) no netstat
== arquivo de ambiente ==
.gitignore:49:*.env     .env
.env ainda não existe
```

Bloco 1 (segunda tentativa, a que passou):

```
removido: .env era a cópia em branco do exemplo (sobra da tentativa anterior)
criado: .env (nenhum valor foi impresso)
== campos obrigatórios ==
ok     IMAGE_TAG preenchido
ok     RABBITMQ_USER preenchido
ok     RABBITMQ_PASSWORD preenchido
ok     ADMIN_EMAIL preenchido
ok     ADMIN_PASSWORD preenchido
ok     IMAGE_TAG é o SHA dos experimentos
.gitignore:49:*.env     .env
ok     o Compose aceitou o .env (config --quiet)
```

Bloco 2 (segunda tentativa, a que passou). As linhas repetidas do `up -d` estão como vieram:

```
== pull (uma linha por imagem; o progresso das camadas fica de fora, os erros não) ==
 Image ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f Pulling
 Image rabbitmq:4.4.0-management Pulling
 Image ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f Pulling
 Image ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f Pulled
 Image ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f Pulled
 Image rabbitmq:4.4.0-management Pulled
== subir ==
 Volume expensehub_rabbitmq-data Creating
 Volume expensehub_expensehub-keys Creating
 Network expensehub_default Creating
 Volume expensehub_expensehub-data Creating
 Volume expensehub_rabbitmq-data Creating
 Volume expensehub_expensehub-data Creating
 Network expensehub_default Creating
 Volume expensehub_expensehub-keys Creating
 Volume expensehub_expensehub-data Created
 Volume expensehub_expensehub-data Created
 Volume expensehub_rabbitmq-data Created
 Volume expensehub_rabbitmq-data Created
 Volume expensehub_expensehub-keys Created
 Volume expensehub_expensehub-keys Created
 Network expensehub_default Created
 Network expensehub_default Created
 Container expensehub-rabbitmq-1 Creating
 Container expensehub-rabbitmq-1 Created
 Container expensehub-web-1 Creating
 Container expensehub-web-1 Created
 Container expensehub-worker-1 Creating
 Container expensehub-worker-1 Created
 Container expensehub-rabbitmq-1 Starting
 Container expensehub-rabbitmq-1 Started
 Container expensehub-rabbitmq-1 Waiting
 Container expensehub-rabbitmq-1 Healthy
 Container expensehub-web-1 Starting
 Container expensehub-web-1 Started
 Container expensehub-rabbitmq-1 Waiting
 Container expensehub-web-1 Waiting
 Container expensehub-rabbitmq-1 Healthy
 Container expensehub-web-1 Healthy
 Container expensehub-worker-1 Starting
 Container expensehub-worker-1 Started
== aguardando o web ficar saudável (até 3 minutos) ==
web: healthy
== serviços ==
rabbitmq | estado=running | saude=healthy | imagem=rabbitmq:4.4.0-management
web | estado=running | saude=healthy | imagem=ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
worker | estado=running | saude= | imagem=ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
== versão em execução (label, imagem, início) ==
web | ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f | revision=f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f | iniciado=2026-11-03T17:52:00.653824754Z
worker | ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f | revision=f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f | iniciado=2026-11-03T17:52:06.331068374Z
== linha de log da versão ==
web-1     |       ExpenseHub web started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
worker-1  |       ExpenseHub worker started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
== health ==
{"status":"ok"}
```

Sobre o `pull`: a saída está como veio (`Pulling` e `Pulled`). Ela não diz se as camadas foram baixadas naquele momento ou já estavam no Docker local, e este dossiê não afirma onde o download aconteceu. O serviço `worker` não tem healthcheck no `compose.yaml`, por isso a coluna `saude` dele está vazia. Observação do assistente, não do Renan: no ensaio do roteiro (03/11/2026, projeto separado, Docker configurado sem credenciais do GHCR) o `docker compose pull` dessas duas imagens funcionou, o que indica que os pacotes são públicos.

Bloco 3a (a primeira leitura recebeu uma linha vazia da colagem e foi recusada por tamanho; a segunda foi aceita; a senha não aparece em lugar nenhum):

```
Regras: 12 ou mais caracteres, com maiúscula, minúscula, dígito e símbolo; sem cifrão, jogo da velha, aspas, barra invertida nem espaço.
Senha das contas de teste:
recusada: tem menos de 12 caracteres. Digite de novo.
Senha das contas de teste:
senha aceita (16 caracteres)
```

Bloco 3b:

```
login do Admin: HTTP 200
```

Bloco 3c:

```
cadastro ana.i06@example.test     HTTP 201
cadastro bruno.i06@example.test   HTTP 201
cadastro caio.i06@example.test    HTTP 201
cadastro gestor.i06@example.test  HTTP 201
lista de usuários (Admin): HTTP 200
papéis   ana.i06@example.test     ["Employee"]               HTTP 200
papéis   bruno.i06@example.test   ["Employee"]               HTTP 200
papéis   caio.i06@example.test    ["Auditor"]                HTTP 200
papéis   gestor.i06@example.test  ["Approver","Finance"]     HTTP 200
login    ana.i06@example.test     HTTP 200
login    bruno.i06@example.test   HTTP 200
login    caio.i06@example.test    HTTP 200
login    gestor.i06@example.test  HTTP 200
```

Bloco 3d (executado uma vez só):

```
ana      100.00 2026-09-01 Approved criar=201 enviar=200 aprovar=200 pagar=-
ana       50.00 2026-09-15 Approved criar=201 enviar=200 aprovar=200 pagar=-
ana       25.00 2026-09-30 Paid     criar=201 enviar=200 aprovar=200 pagar=200
bruno    200.00 2026-09-10 Approved criar=201 enviar=200 aprovar=200 pagar=-
ana      500.00 2026-10-01 Paid     criar=201 enviar=200 aprovar=200 pagar=200
```

**Resultado da preparação.** Tudo como o contrato esperado: quatro cadastros `201`, quatro trocas de papéis `200`, quatro logins `200` (o Admin do `.env` entrou, o que mostra que a senha gerada foi a gravada e que o seed rodou na primeira subida), as cinco despesas com `201`, `200` e `200` e o pagamento `200` nas duas pagas. O web ficou `healthy` e o `/health` respondeu `{"status":"ok"}`. Os dois contêineres da aplicação têm o rótulo `org.opencontainers.image.revision` igual ao SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` e a linha de log `Image commit` com o mesmo SHA. Os volumes `expensehub_rabbitmq-data`, `expensehub_expensehub-keys` e `expensehub_expensehub-data` foram criados nesta subida (o Compose imprimiu `Creating` e `Created` para cada um).

### Tentativas que falharam e foram refeitas (ordem real)

Nesta ordem (o texto de cada saída está resumido pelas linhas que o terminal imprimiu; as marcações entre colchetes do Renan não são citadas):

1. **Bloco 0:** passou na primeira vez.
2. **Bloco 1, primeira vez: falhou.** A colagem trouxe linhas em branco entre as linhas, a continuação do `sed -i \` quebrou e o `.env` ficou como cópia em branco do `.env.example`. O terminal imprimiu o texto de uso do `sed` e, depois, `bash: -e: command not found` (cinco vezes) e `bash: .env: command not found`. O bloco ainda imprimiu `criado: .env (nenhum valor foi impresso)`, porque essa mensagem não depende do resultado do `sed` (limite do texto do bloco, que não foi corrigido); quem acusou o problema foi a conferência seguinte: `FALTA  IMAGE_TAG em branco` e o mesmo para `RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `ADMIN_EMAIL` e `ADMIN_PASSWORD`, e `ERRO   IMAGE_TAG diferente do SHA dos experimentos`. Nenhum valor gerado foi impresso nem gravado.
3. **Bloco 2, primeira vez: falhou, sem criar nada.** O `docker compose pull` e o `up -d` recusaram com 11 mensagens do tipo `error while interpolating services.web.image: required variable IMAGE_TAG is missing a value: Defina IMAGE_TAG com o SHA completo do commit` (e as equivalentes para `RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `ADMIN_EMAIL` e `ADMIN_PASSWORD`). O laço de espera terminou sem achar o web (`web:` vazio), o `docker compose ps -a` não listou serviço algum, o `docker inspect` respondeu `invalid container name or ID: value is empty`, e o `grep "Image commit"` e o `curl` ficaram sem saída. Nenhum contêiner, rede ou volume existia depois dessa tentativa: na segunda tentativa o `up -d` imprimiu `Volume ... Creating` e `Network ... Creating` para todos eles.
4. **Bloco 1, segunda vez: passou**, na versão de uma linha por comando, com a trava `cmp -s` (apagou só a cópia em branco do exemplo: `removido: .env era a cópia em branco do exemplo`) e o `docker compose config --quiet` no fim (saída acima).
5. **Bloco 2, segunda vez: passou** (saída acima).
6. **Bloco 3a:** a primeira leitura recebeu uma linha vazia da colagem e foi recusada por tamanho (`recusada: tem menos de 12 caracteres`); a seguinte foi aceita.
7. **Blocos 3b, 3c, 3d e 4 (E01-a):** passaram na primeira vez.
8. **Bloco 5 (E01-b), primeira vez: falhou.** O `curl ... \` seguido de linha em branco rodou sem URL (`curl: (2) no URL specified`), a linha seguinte foi executada como comando (`bash: -H: command not found`) e o `| tr ...` deu `bash: syntax error near unexpected token '|'`. Como o arquivo não foi baixado, o `file` respondeu `cannot open ... (No such file or directory)`, o `unzip -Z1` não achou o pacote, o Python terminou com `FileNotFoundError` (o caminho do usuário que ele imprimiu não foi copiado para cá) e o `sheet_hash` imprimiu `e3b0c44298fc1c14`, que são os 16 primeiros caracteres do SHA-256 de uma entrada vazia; não é resultado de relatório. Nada foi alterado no sistema.
9. **Bloco 5, segunda vez: passou**, com o `curl` em uma linha. **O E01-b foi feito antes do E02**, ou seja, com o worker ainda no primeiro processo (iniciado às 17:52:06Z).
10. **Blocos 6 a 13 (E02 a Fim):** passaram na primeira vez.

O que mudou no roteiro por causa das falhas: os blocos 1 e 5 passaram para uma linha por comando, o bloco 1 ganhou a trava e a conferência do Compose, e a regra "nenhum comando com barra de continuação" ficou escrita na [Preparação](#preparação-blocos-0-a-3d). No bloco 3a, uma primeira leitura recusada por tamanho logo depois de colar é a linha vazia da colagem; basta digitar de novo.

**Retomada se o terminal fechar.** Cole de novo os blocos 0, 3a e 3b (o 1 recusa sobrescrever o `.env` e o 2 é inofensivo), depois o 3c (os cadastros repetidos devolvem `409`, o que é esperado, e ele refaz os papéis e os tokens). **Nunca** cole o 3d de novo. Os identificadores já impressos voltam por atribuição, por exemplo `REPORT_E01=<id impresso no E01>`.

### E01: Fluxo normal

**Objetivo.** Autenticar, solicitar, guardar o id, acompanhar até `Completed`, baixar o Excel e conferir resultado e planilha contra dados conhecidos.

**Pré-condições.** Blocos 0 a 3d executados no mesmo terminal; pilha no ar; os dados de teste acima.

**Comandos.**

```bash
set +x; set +H
echo "== E01-a: a ana solicita o relatório Mine de setembro =="
api POST /api/reports "$T_ANA" "{$PERIODO,\"scope\":\"Mine\"}"
echo "POST /api/reports: HTTP $CODE"
printf '%s\n' "$HDRS" | grep -i '^location:' | tr -d '\r'
REPORT_E01=$(jf id)
echo "id do relatório (E01): $REPORT_E01"
echo "status depois de acompanhar: $(wait_status "$T_ANA" "$REPORT_E01" Completed 60)"
api GET "/api/reports/$REPORT_E01" "$T_ANA"
echo "GET /api/reports/{id}: HTTP $CODE"
printf '%s' "$RESP" | python -I -c "$PY_SHOW" 3 175.00
echo "== linhas de log com o id (web e worker, com horário) =="
docker compose logs --timestamps web worker 2>&1 | grep "$REPORT_E01"
```

```bash
set +x; set +H
echo "== E01-b: Excel do relatório $REPORT_E01 =="
curl -s -o "$W/e01.xlsx" -D - -w 'download: HTTP %{http_code}, %{size_download} bytes\n' -H "Authorization: Bearer $T_ANA" "$BASE/api/reports/$REPORT_E01/excel" | tr -d '\r' | grep -i -E '^(HTTP|content-type|content-disposition|download)'
file "$W/e01.xlsx"
echo "entradas do pacote:"; unzip -Z1 "$W/e01.xlsx" | sed 's/^/  /'
echo "== células da planilha (linhas 1 a 5: identificador, escopo, período, geração; linhas 7 a 13: a tabela) =="
python -I -c "$PY_XLSX" "$WW/e01.xlsx"
echo "hash das células (sheet1.xml): $(sheet_hash "$W/e01.xlsx")"
```

**O que observar (esperado pelo contrato).**

- `POST /api/reports`: `202` com cabeçalho `Location` apontando para o relatório; o id é guardado em `REPORT_E01`.
- O status chega a `Completed` para o mesmo id. O detalhe traz `result` com `totalCount` 3 e `totalAmount` 175.00; `byStatus` com os cinco estados: Approved 2 e 150.00, Paid 1 e 25.00, e Draft, Submitted e Rejected com 0 e 0.00. A linha final da conferência diz `OK`.
- Logs do web e do worker com o mesmo id: publicação, início e conclusão.
- Excel: `200`, `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `Content-Disposition: attachment` com o nome `report-{id}.xlsx`; `file` reconhece um pacote Zip/OOXML (não é CSV); a planilha traz identificador, escopo, período e instante de geração e uma tabela com os cinco estados, contagens, valores e o total geral, em células numéricas, iguais ao JSON.

**Saída real colada pelo Renan, E01-a** (a ordem das linhas de log é a do `docker compose logs`, que agrupa por serviço; a ordem no tempo está na tabela do [Diagnóstico](#diagnóstico)):

```
== E01-a: a ana solicita o relatório Mine de setembro ==
POST /api/reports: HTTP 202
Location: /api/reports/2e1a02c1-aac4-40ca-b2c9-2e4043209fa3
id do relatório (E01): 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3
status depois de acompanhar: Completed
GET /api/reports/{id}: HTTP 200
status=Completed scope=Mine createdAt=2026-11-03T18:09:44.076158Z startedAt=2026-11-03T18:09:45.96755Z completedAt=2026-11-03T18:09:46.276163Z
totalCount=3 totalAmount=175 generatedAt=2026-11-03T18:09:46.1549253Z
  Draft     count=0 amount=0
  Submitted count=0 amount=0
  Approved  count=2 amount=150
  Rejected  count=0 amount=0
  Paid      count=1 amount=25
esperado 3 itens e 175.00: OK
== linhas de log com o id (web e worker, com horário) ==
worker-1  | 2026-11-03T18:09:44.404132798Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 message received (redelivered: False).
worker-1  | 2026-11-03T18:09:46.128546753Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 processing started (resumed: False).
worker-1  | 2026-11-03T18:09:46.290841734Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 completed with 3 expenses.
worker-1  | 2026-11-03T18:09:46.299946498Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 message acknowledged after the result was saved or found saved.
web-1     | 2026-11-03T18:09:44.343243735Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 message confirmed by the broker.
web-1     | 2026-11-03T18:09:44.344289397Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 registered and published.
```

**Saída real colada pelo Renan, E01-b** (segunda tentativa, a que passou; a primeira está em [Tentativas que falharam](#tentativas-que-falharam-e-foram-refeitas-ordem-real)):

```
== E01-b: Excel do relatório 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 ==
HTTP/1.1 200 OK
Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Content-Disposition: attachment; filename=report-2e1a02c1-aac4-40ca-b2c9-2e4043209fa3.xlsx; filename*=UTF-8''report-2e1a02c1-aac4-40ca-b2c9-2e4043209fa3.xlsx
download: HTTP 200, 2457 bytes
/tmp/tmp.hePTViOUuz/e01.xlsx: Microsoft Excel 2007+
entradas do pacote:
  xl/workbook.xml
  xl/styles.xml
  xl/worksheets/sheet1.xml
  _rels/.rels
  xl/_rels/workbook.xml.rels
  [Content_Types].xml
== células da planilha (linhas 1 a 5: identificador, escopo, período, geração; linhas 7 a 13: a tabela) ==
A1=Identificador[texto] | B1=2e1a02c1-aac4-40ca-b2c9-2e4043209fa3[texto]
A2=Escopo[texto] | B2=Mine[texto]
A3=Data inicial[texto] | B3=2026-09-01[texto]
A4=Data final[texto] | B4=2026-09-30[texto]
A5=Gerado em (UTC)[texto] | B5=2026-11-03T18:09:46Z[texto]
A7=Estado[texto] | B7=Quantidade[texto] | C7=Valor[texto]
A8=Draft[texto] | B8=0[numero] | C8=0.00[numero]
A9=Submitted[texto] | B9=0[numero] | C9=0.00[numero]
A10=Approved[texto] | B10=2[numero] | C10=150.00[numero]
A11=Rejected[texto] | B11=0[numero] | C11=0.00[numero]
A12=Paid[texto] | B12=1[numero] | C12=25.00[numero]
A13=Total geral[texto] | B13=3[numero] | C13=175.00[numero]
hash das células (sheet1.xml): ae3d22e2d17ba4e1
```

**Resultado do E01: atendido.** O `POST` respondeu `202` com `Location` apontando para o relatório; o mesmo id foi acompanhado até `Completed` e o detalhe trouxe 3 itens e 175 (Approved 2 e 150, Paid 1 e 25, os outros três estados em zero), exatamente o esperado para os dados conhecidos, com a conferência do bloco dizendo `OK`. Os logs do web e do worker correlacionam a mesma solicitação pelo id. O Excel veio com `200`, o `Content-Type` de planilha, `Content-Disposition: attachment` com `report-{id}.xlsx`, o `file` o reconheceu como `Microsoft Excel 2007+` (um pacote `.xlsx`, não CSV), e as células repetem o JSON: identificador, escopo, período, o instante de geração do resultado salvo (`2026-11-03T18:09:46Z`, que é o `generatedAt` do JSON sem as frações de segundo) e a tabela com os cinco estados e o total geral em células numéricas (`[numero]`). O hash das células (`ae3d22e2d17ba4e1`) é a referência que o E03 e o E05 vão comparar.

### E02: Worker parado

**Objetivo.** Parar só o worker, solicitar, observar a mensagem disponível na fila e o relatório não concluído, retomar e acompanhar o **mesmo** id até `Completed`.

**Pré-condições.** E01 concluído (a pilha tem uma conclusão anterior para comparar); `T_ANA` válido (se o token expirar, cole o bloco 3b e o 3c de novo).

**Comandos.**

```bash
set +x; set +H
echo "== E02: parar só o worker =="
docker compose stop worker
docker compose ps -a --format '{{.Service}} | estado={{.State}}'
echo "== a ana solicita outro relatório Mine, com o worker parado =="
api POST /api/reports "$T_ANA" "{$PERIODO,\"scope\":\"Mine\"}"
echo "POST /api/reports: HTTP $CODE"
REPORT_E02=$(jf id)
echo "id do relatório (E02): $REPORT_E02"
sleep 5
echo "== fila no RabbitMQ (Ready e Unacked não são Pending e Processing) =="
docker compose exec -T rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged
api GET "/api/reports/$REPORT_E02" "$T_ANA"
echo "GET /api/reports/{id}: HTTP $CODE"
printf '%s' "$RESP" | python -I -c "$PY_SHOW"
echo "== religar o worker =="
docker compose start worker
echo "status do MESMO id depois de acompanhar: $(wait_status "$T_ANA" "$REPORT_E02" Completed 90)"
api GET "/api/reports/$REPORT_E02" "$T_ANA"
printf '%s' "$RESP" | python -I -c "$PY_SHOW" 3 175.00
echo "== fila depois =="
docker compose exec -T rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged
echo "== linhas de log com o id (web e worker, com horário) =="
docker compose logs --timestamps web worker 2>&1 | grep "$REPORT_E02"
```

**O que observar (esperado pelo contrato).**

- Com o worker parado, o `POST` ainda devolve `202` (o web persistiu o pedido e o broker confirmou a publicação).
- Fila `expensehub.reports` com **1 mensagem Ready e 0 Unacked**; o relatório continua `Pending`, sem `startedAt` nem `result`.
- Depois de religar o worker, o **mesmo id** chega a `Completed` com 3 itens e 175.00; a fila volta a 0 e 0.
- Logs correlacionados pelo id: a publicação no web (antes de o worker voltar) e o início e a conclusão no worker (depois).

**Saída real colada pelo Renan:**

```
== E02: parar só o worker ==
[+] stop 1/1
 ✔ Container expensehub-worker-1 Stopped                                                                               0.5s
rabbitmq | estado=running
web | estado=running
worker | estado=exited
== a ana solicita outro relatório Mine, com o worker parado ==
POST /api/reports: HTTP 202
id do relatório (E02): 56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f
== fila no RabbitMQ (Ready e Unacked não são Pending e Processing) ==
Timeout: 60.0 seconds ...
Listing queues for vhost / ...
name    messages_ready  messages_unacknowledged
expensehub.reports      1       0
GET /api/reports/{id}: HTTP 200
status=Pending scope=Mine createdAt=2026-11-03T18:17:27.6971126Z startedAt=None completedAt=None
== religar o worker ==
[+] start 3/3
 ✔ Container expensehub-rabbitmq-1 Healthy                                                                             1.0s
 ✔ Container expensehub-web-1      Healthy                                                                             0.5s
 ✔ Container expensehub-worker-1   Started                                                                             0.3s
status do MESMO id depois de acompanhar: Completed
status=Completed scope=Mine createdAt=2026-11-03T18:17:27.6971126Z startedAt=2026-11-03T18:17:39.8944265Z completedAt=2026-11-03T18:17:40.0700856Z
totalCount=3 totalAmount=175 generatedAt=2026-11-03T18:17:40.033436Z
  Draft     count=0 amount=0
  Submitted count=0 amount=0
  Approved  count=2 amount=150
  Rejected  count=0 amount=0
  Paid      count=1 amount=25
esperado 3 itens e 175.00: OK
== fila depois ==
Timeout: 60.0 seconds ...
Listing queues for vhost / ...
name    messages_ready  messages_unacknowledged
expensehub.reports      0       0
== linhas de log com o id (web e worker, com horário) ==
worker-1  | 2026-11-03T18:17:38.705565839Z       Report 56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f message received (redelivered: False).
worker-1  | 2026-11-03T18:17:40.019166755Z       Report 56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f processing started (resumed: False).
worker-1  | 2026-11-03T18:17:40.083491704Z       Report 56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f completed with 3 expenses.
worker-1  | 2026-11-03T18:17:40.090224827Z       Report 56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f message acknowledged after the result was saved or found saved.
web-1     | 2026-11-03T18:17:27.706505470Z       Report 56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f message confirmed by the broker.
web-1     | 2026-11-03T18:17:27.706509255Z       Report 56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f registered and published.
```

**Resultado do E02: atendido.** Com o worker parado (`estado=exited`, web e RabbitMQ no ar), o `POST` ainda respondeu `202` e o relatório `56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f` ficou `Pending` (sem `startedAt`, sem `completedAt`, sem `result`) enquanto a fila `expensehub.reports` mostrava **1 mensagem Ready e 0 Unacked**. Depois de religar só o worker, o **mesmo id** chegou a `Completed` com 3 itens e 175, a fila voltou a `0 0` e o log traz o ciclo inteiro por id: o web confirmou a publicação às 18:17:27.706Z, e o worker recebeu a mensagem só às 18:17:38.705Z, cerca de 11 s depois, logo depois de o worker ser religado, com `redelivered: False` (era a primeira entrega daquela mensagem, que esperou na fila). O relatório do E01 continuou com os mesmos instantes e o mesmo resultado nos blocos seguintes.

### E03: Duplicata depois de reiniciar o worker

**Objetivo.** Depois de concluir, reiniciar o worker e republicar a mensagem com o mesmo `reportId`; comparar resultado e registros e mostrar que não houve novo efeito.

**Pré-condições.** E01 concluído; `T_ANA` válido; terminal aberto desde o E01 (as variáveis `REPORT_E01` e `ANTES_*` precisam sobreviver entre os blocos E03-a e E03-b).

**Comandos, parte 1 (E03-a): estado antes e reinício do worker.**

```bash
set +x; set +H
echo "== E03-a: estado do relatório $REPORT_E01 ANTES de reiniciar o worker e republicar a mensagem =="
api GET "/api/reports/$REPORT_E01" "$T_ANA"; ANTES_DETALHE=$(printf '%s' "$RESP" | sha256sum | cut -c1-16)
printf '%s' "$RESP" | python -I -c "$PY_SHOW" 3 175.00
api GET /api/reports "$T_ANA"; ANTES_LISTA=$(count_ids)
curl -s -o "$W/e03-antes.xlsx" -H "Authorization: Bearer $T_ANA" "$BASE/api/reports/$REPORT_E01/excel"; ANTES_XLSX=$(sheet_hash "$W/e03-antes.xlsx")
ANTES_DUP=$(docker compose logs worker 2>&1 | grep -c "duplicate message ignored")
echo "hash do JSON do detalhe : $ANTES_DETALHE"
echo "relatórios na listagem  : $ANTES_LISTA"
echo "hash das células (Excel): $ANTES_XLSX"
echo "linhas 'duplicate message ignored' no log do worker: $ANTES_DUP"
echo "fila:"; docker compose exec -T rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged
echo "== reiniciar só o worker =="
docker compose restart worker
fim=$((SECONDS+60)); consumidores=""
while [ $SECONDS -lt $fim ]; do
  consumidores=$(docker compose exec -T rabbitmq rabbitmqctl list_queues name consumers 2>/dev/null | grep '^expensehub.reports' | tr -s '[:space:]' ' ' | cut -d' ' -f2)
  [ "$consumidores" = "1" ] && break
  sleep 2
done
echo "consumidores na fila depois do reinício: $consumidores"
docker inspect --format 'worker reiniciado | iniciado={{.State.StartedAt}}' "$(docker compose ps -q worker)"
echo
echo "Agora faça o passo do painel (republicar a mensagem) e só depois cole o bloco E03-b."
echo "Payload a publicar: {\"reportId\":\"$REPORT_E01\"}"
```

**Passo do painel: republicar a mensagem do E01.** O bloco E03-a imprime o payload pronto.

1. Abrir `http://localhost:15672` no navegador e entrar com o usuário `expensehub` e a senha do broker, que está no `.env` (abra o arquivo no editor; não cole essa senha em lugar nenhum).
2. Aba **Queues and Streams**, clicar na fila `expensehub.reports`.
3. Descer até a seção **Publish message**.
4. **Delivery mode**: `2 - Persistent`.
5. **Properties**: `content_type` = `application/json` (o worker lê só o corpo da mensagem, mas assim ela fica igual à que o web publica).
6. **Payload**: colar exatamente `{"reportId":"<id do E01>"}`, com o id impresso pelo bloco E03-a.
7. Clicar em **Publish message**. A página mostra **Message published.**

Se algum rótulo da tela for diferente, avisar o assistente, que ajusta o texto.

**Comandos, parte 2 (E03-b): comparação depois da republicação.**

```bash
set +x; set +H
echo "== E03-b: depois da republicação do mesmo reportId =="
fim=$((SECONDS+60)); dup=0
while [ $SECONDS -lt $fim ]; do
  dup=$(docker compose logs worker 2>&1 | grep "$REPORT_E01" | grep -c "duplicate message ignored")
  [ "$dup" -ge 1 ] && break
  sleep 2
done
echo "linhas de duplicata para este id no log do worker: $dup"
echo "== linhas de log do worker com o id (com horário) =="
docker compose logs --timestamps worker 2>&1 | grep "$REPORT_E01"
echo "== comparação =="
api GET "/api/reports/$REPORT_E01" "$T_ANA"; DEPOIS_DETALHE=$(printf '%s' "$RESP" | sha256sum | cut -c1-16)
printf '%s' "$RESP" | python -I -c "$PY_SHOW" 3 175.00
api GET /api/reports "$T_ANA"; DEPOIS_LISTA=$(count_ids)
curl -s -o "$W/e03-depois.xlsx" -H "Authorization: Bearer $T_ANA" "$BASE/api/reports/$REPORT_E01/excel"; DEPOIS_XLSX=$(sheet_hash "$W/e03-depois.xlsx")
chk "hash do JSON do detalhe" "$ANTES_DETALHE" "$DEPOIS_DETALHE"
chk "relatórios na listagem" "$ANTES_LISTA" "$DEPOIS_LISTA"
chk "hash das células do Excel" "$ANTES_XLSX" "$DEPOIS_XLSX"
echo "fila:"; docker compose exec -T rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged
echo "linhas 'Report $REPORT_E01 completed with' no log do worker (a conclusão original): $(docker compose logs worker 2>&1 | grep "$REPORT_E01" | grep -c ' completed with ')"
```

**O que observar (esperado pelo contrato).**

- O log do worker traz uma linha de duplicata (`duplicate message ignored`) com o `reportId` do E01 depois do reinício e da republicação.
- As três comparações dizem `OK`: o JSON do detalhe (inclusive `completedAt` e `result`), a quantidade de relatórios na listagem e as células do Excel ficam idênticos. Nenhum relatório novo aparece na listagem.
- A fila volta a 0 Ready e 0 Unacked (a mensagem foi consumida e confirmada).
- A conclusão original (`completed with`) continua aparecendo uma vez só para esse id: não houve segunda conclusão.

**Saída real colada pelo Renan, parte 1 (E03-a):**

```
== E03-a: estado do relatório 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 ANTES de reiniciar o worker e republicar a mensagem ==
status=Completed scope=Mine createdAt=2026-11-03T18:09:44.076158Z startedAt=2026-11-03T18:09:45.96755Z completedAt=2026-11-03T18:09:46.276163Z
totalCount=3 totalAmount=175 generatedAt=2026-11-03T18:09:46.1549253Z
  Draft     count=0 amount=0
  Submitted count=0 amount=0
  Approved  count=2 amount=150
  Rejected  count=0 amount=0
  Paid      count=1 amount=25
esperado 3 itens e 175.00: OK
hash do JSON do detalhe : 225393dcc6004d6b
relatórios na listagem  : 2
hash das células (Excel): ae3d22e2d17ba4e1
linhas 'duplicate message ignored' no log do worker: 0
fila:
Timeout: 60.0 seconds ...
Listing queues for vhost / ...
name    messages_ready  messages_unacknowledged
expensehub.reports      0       0
== reiniciar só o worker ==
[+] restart 0/1
 - Container expensehub-worker-1 Restarting                                                                            0.4s
consumidores na fila depois do reinício: 1
worker reiniciado | iniciado=2026-11-03T18:18:52.843633336Z

Agora faça o passo do painel (republicar a mensagem) e só depois cole o bloco E03-b.
Payload a publicar: {"reportId":"2e1a02c1-aac4-40ca-b2c9-2e4043209fa3"}
```

**Passo do painel (bloco 8), feito no navegador, sem saída de terminal.** Descrição do Renan (não há captura de tela no dossiê): RabbitMQ 4.4.0 em Erlang 29.1.2, usuário `expensehub` (a senha não aparece em nenhum registro). Caminho: **Queues and Streams**, fila `expensehub.reports` (virtual host `/`, com Consumers (1) e Bindings (1) visíveis na página), seção **Publish message**. Campos: Delivery mode `2 - Persistent`; Properties `content_type` = `application/json`; Payload encoding `String (default)`; Payload `{"reportId":"2e1a02c1-aac4-40ca-b2c9-2e4043209fa3"}` (o id do E01). Resultado: janela com o texto **Message published.** (botão Close). A página foi atualizada às 15:24:20 antes e às 15:24:35 depois do clique (horário de Brasília), e a publicação foi às 15:24:34, que são 18:24:34Z, instante que o log do worker confirma logo abaixo. Os rótulos da tela eram iguais aos do roteiro, sem nenhuma diferença.

**Saída real colada pelo Renan, parte 2 (E03-b):**

```
== E03-b: depois da republicação do mesmo reportId ==
linhas de duplicata para este id no log do worker: 1
== linhas de log do worker com o id (com horário) ==
worker-1  | 2026-11-03T18:09:44.404132798Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 message received (redelivered: False).
worker-1  | 2026-11-03T18:09:46.128546753Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 processing started (resumed: False).
worker-1  | 2026-11-03T18:09:46.290841734Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 completed with 3 expenses.
worker-1  | 2026-11-03T18:09:46.299946498Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 message acknowledged after the result was saved or found saved.
worker-1  | 2026-11-03T18:24:34.432351339Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 message received (redelivered: False).
worker-1  | 2026-11-03T18:24:35.324575544Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 is already Completed: duplicate message ignored, nothing was changed.
worker-1  | 2026-11-03T18:24:35.331069330Z       Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 message acknowledged after the result was saved or found saved.
== comparação ==
status=Completed scope=Mine createdAt=2026-11-03T18:09:44.076158Z startedAt=2026-11-03T18:09:45.96755Z completedAt=2026-11-03T18:09:46.276163Z
totalCount=3 totalAmount=175 generatedAt=2026-11-03T18:09:46.1549253Z
  Draft     count=0 amount=0
  Submitted count=0 amount=0
  Approved  count=2 amount=150
  Rejected  count=0 amount=0
  Paid      count=1 amount=25
esperado 3 itens e 175.00: OK
hash do JSON do detalhe                                            esperado=225393dcc6004d6b obtido=225393dcc6004d6b OK
relatórios na listagem                                            esperado=2      obtido=2      OK
hash das células do Excel                                         esperado=ae3d22e2d17ba4e1 obtido=ae3d22e2d17ba4e1 OK
fila:
Timeout: 60.0 seconds ...
Listing queues for vhost / ...
name    messages_ready  messages_unacknowledged
expensehub.reports      0       0
linhas 'Report 2e1a02c1-aac4-40ca-b2c9-2e4043209fa3 completed with' no log do worker (a conclusão original): 1
```

**Resultado do E03: atendido.** O worker foi reiniciado (novo `StartedAt` 18:18:52Z, um consumidor na fila) e, depois da republicação do mesmo `reportId` às 18:24:34Z, o log do worker mostra a mensagem recebida, a linha `is already Completed: duplicate message ignored, nothing was changed.` e a confirmação 6,5 ms depois, sem nova linha de início nem de conclusão. O JSON do detalhe (inclusive `startedAt`, `completedAt` e `generatedAt`), a quantidade de relatórios na listagem (2: o do E01 e o do E02) e as células do Excel ficaram idênticos antes e depois (`225393dcc6004d6b`, `2` e `ae3d22e2d17ba4e1`, e o hash das células é o mesmo do E01-b), a fila terminou em `0 0` e a conclusão original (`completed with`) aparece uma só vez para esse id. A mensagem republicada pelo painel chegou com `redelivered: False`, porque para o broker é uma mensagem nova; quem a reconhece como duplicata é o estado gravado no banco (o relatório já estava `Completed`), que sobreviveu ao reinício do worker.

### E04: Isolamento

**Objetivo.** Employee não solicita `Global` nem consulta ou baixa Excel alheio; Auditor solicita `Global`, mas não consulta nem baixa relatório criado por outro usuário.

**Pré-condições.** E01 concluído (o relatório da ana é o alvo do Auditor); `T_ANA`, `T_BRUNO` e `T_CAIO` válidos; banco sem outras despesas de setembro de 2026 (para o `Global` valer 4 itens e 375,00).

**Comandos.**

```bash
set +x; set +H
echo "== E04: um relatório de outro usuário para servir de alvo (bruno, Employee) =="
api POST /api/reports "$T_BRUNO" "{$PERIODO,\"scope\":\"Mine\"}"; echo "POST do bruno: HTTP $CODE"
REPORT_BRUNO=$(jf id); echo "id do relatório do bruno: $REPORT_BRUNO"
echo "status: $(wait_status "$T_BRUNO" "$REPORT_BRUNO" Completed 60)"
echo "== Employee (ana) =="
api POST /api/reports "$T_ANA" "{$PERIODO,\"scope\":\"Global\"}"
chk "1. Employee pede Global" 403 "$CODE"
api GET "/api/reports/$REPORT_BRUNO" "$T_ANA"
chk "2. Employee consulta o relatório do bruno" 404 "$CODE"
echo "   corpo da resposta: $RESP"
api GET "/api/reports/$REPORT_BRUNO/excel" "$T_ANA"
chk "3. Employee baixa o Excel do relatório do bruno" 404 "$CODE"
echo "   $(printf '%s\n' "$HDRS" | grep -i '^content-type' | tr -d '\r')"
echo "   corpo da resposta: $RESP"
echo "== Auditor (caio) =="
api POST /api/reports "$T_CAIO" "{$PERIODO,\"scope\":\"Global\"}"
chk "4. Auditor pede Global" 202 "$CODE"
REPORT_CAIO=$(jf id); echo "   id do relatório do caio: $REPORT_CAIO"
echo "   status: $(wait_status "$T_CAIO" "$REPORT_CAIO" Completed 60)"
api GET "/api/reports/$REPORT_CAIO" "$T_CAIO"
printf '%s' "$RESP" | python -I -c "$PY_SHOW" 4 375.00
api GET "/api/reports/$REPORT_E01" "$T_CAIO"
chk "5. Auditor consulta o relatório da ana" 404 "$CODE"
echo "   corpo da resposta: $RESP"
api GET "/api/reports/$REPORT_E01/excel" "$T_CAIO"
chk "6. Auditor baixa o Excel do relatório da ana" 404 "$CODE"
echo "   $(printf '%s\n' "$HDRS" | grep -i '^content-type' | tr -d '\r')"
api GET "/api/reports/$REPORT_CAIO" "$T_ANA"
chk "7. Employee consulta o relatório Global do Auditor" 404 "$CODE"
api GET /api/reports "$T_CAIO"
chk "8. a listagem do Auditor só tem o relatório dele (quantidade)" 1 "$(count_ids)"
```

**O que observar (esperado pelo contrato).** As oito linhas `chk` com `OK`: `403` para o Employee pedir `Global`; `404` para o Employee consultar e baixar o relatório do bruno, com corpo de erro (`ProblemDetails`, sem arquivo e sem `Content-Type` de planilha); `202` para o Auditor pedir `Global`, com resultado de 4 itens e 375.00; `404` para o Auditor consultar e baixar o relatório da ana; `404` para o Employee consultar o relatório do Auditor; e a listagem do Auditor com 1 relatório (só o dele).

**Saída real colada pelo Renan:**

```
== E04: um relatório de outro usuário para servir de alvo (bruno, Employee) ==
POST do bruno: HTTP 202
id do relatório do bruno: 8061bff1-4218-4bad-82ba-e8a00991a926
status: Completed
== Employee (ana) ==
1. Employee pede Global                                            esperado=403    obtido=403    OK
2. Employee consulta o relatório do bruno                         esperado=404    obtido=404    OK
   corpo da resposta: {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Report not found.","status":404,"traceId":"00-5744775228ce03cee1cd1e06e5399822-761d3667ac8c4e71-00"}
3. Employee baixa o Excel do relatório do bruno                   esperado=404    obtido=404    OK
   Content-Type: application/problem+json; charset=utf-8
   corpo da resposta: {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Report not found.","status":404,"traceId":"00-b0ea1f69bdc2fa54be2558b466119e2d-b2c72ed15dfb08d3-00"}
== Auditor (caio) ==
4. Auditor pede Global                                             esperado=202    obtido=202    OK
   id do relatório do caio: 3d7f3b45-d770-4a64-a0bc-3a56a1de61ad
   status: Completed
status=Completed scope=Global createdAt=2026-11-03T18:27:21.9674516Z startedAt=2026-11-03T18:27:21.9956811Z completedAt=2026-11-03T18:27:22.008155Z
totalCount=4 totalAmount=375 generatedAt=2026-11-03T18:27:22.0078504Z
  Draft     count=0 amount=0
  Submitted count=0 amount=0
  Approved  count=3 amount=350
  Rejected  count=0 amount=0
  Paid      count=1 amount=25
esperado 4 itens e 375.00: OK
5. Auditor consulta o relatório da ana                            esperado=404    obtido=404    OK
   corpo da resposta: {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Report not found.","status":404,"traceId":"00-f11359bbf772ae9ab637f6ff5866275b-3fb44d55ae484f51-00"}
6. Auditor baixa o Excel do relatório da ana                      esperado=404    obtido=404    OK
   Content-Type: application/problem+json; charset=utf-8
7. Employee consulta o relatório Global do Auditor                esperado=404    obtido=404    OK
8. a listagem do Auditor só tem o relatório dele (quantidade)    esperado=1      obtido=1      OK
```

**Resultado do E04: atendido, 8 de 8.** O Employee (ana) recebeu `403` ao pedir `Global` e `404` ao consultar e ao baixar o Excel do relatório do bruno, com corpo `ProblemDetails` (`Report not found.`) e `Content-Type: application/problem+json`, sem arquivo. O Auditor (caio) teve o `Global` aceito (`202`) e concluído com 4 itens e 375 (Approved 3 e 350, Paid 1 e 25), o esperado para os dados conhecidos num banco novo, sem outras despesas de setembro; recebeu `404` ao consultar e ao baixar o Excel do relatório da ana (criado por outro usuário), a ana recebeu `404` ao consultar o relatório `Global` do Auditor, e a listagem do Auditor trouxe só o relatório dele (1).

### E05: Persistência e versão

**Objetivo.** Recriar apenas web e worker usando as imagens publicadas, consultar um resultado anterior e comprovar versão e dados preservados. Não recria o banco nem remove volume.

**Pré-condições.** E01 concluído; terminal aberto desde o E01 (as variáveis `E05_*` precisam sobreviver entre os blocos E05-a e E05-b). O bloco E05-a faz um login novo logo antes da recriação, para a prova do token não depender do tempo gasto entre os blocos.

**Comandos, parte 1 (E05-a): estado antes.**

```bash
set +x; set +H
echo "== E05-a: login novo da ana e o estado ANTES de recriar web e worker =="
login ana.i06@example.test "$TEST_PW"; T_E05=$TOK; echo "login: HTTP $CODE"
api GET "/api/reports/$REPORT_E01" "$T_E05"; E05_ANTES_DETALHE=$(printf '%s' "$RESP" | sha256sum | cut -c1-16)
printf '%s' "$RESP" | python -I -c "$PY_SHOW" 3 175.00
curl -s -o "$W/e05-antes.xlsx" -H "Authorization: Bearer $T_E05" "$BASE/api/reports/$REPORT_E01/excel"; E05_ANTES_XLSX=$(sheet_hash "$W/e05-antes.xlsx")
echo "hash do JSON do detalhe : $E05_ANTES_DETALHE"
echo "hash das células (Excel): $E05_ANTES_XLSX"
id_de() { docker inspect --format '{{printf "%.12s" .Id}}' "$(docker compose ps -q "$1")"; }
E05_ID_web=$(id_de web); E05_ID_worker=$(id_de worker); E05_ID_rabbitmq=$(id_de rabbitmq)
echo "== contêineres antes =="
for s in web worker rabbitmq; do
  echo "$s | id=$(id_de $s) | $(docker inspect --format 'iniciado={{.State.StartedAt}} | {{.Config.Image}}' "$(docker compose ps -q $s)" | sed 's/@sha256:[0-9a-f]*//')"
done
echo "== linha de log da versão (contêineres atuais; o worker aparece mais de uma vez porque este contêiner foi parado e religado nos E02 e E03, e o log fica no mesmo contêiner) =="
docker compose logs web worker 2>&1 | grep "Image commit"
```

**Comandos, parte 2 (E05-b): recriar web e worker e comparar.**

```bash
set +x; set +H
echo "== E05-b: recriar SÓ web e worker (sem remover volumes) =="
docker compose up -d --force-recreate web worker
fim=$((SECONDS+180)); saude=""
while [ $SECONDS -lt $fim ]; do
  saude=$(docker inspect --format '{{.State.Health.Status}}' "$(docker compose ps -q web)" 2>/dev/null)
  [ "$saude" = "healthy" ] && break
  sleep 3
done
echo "web: $saude"
echo "== contêineres depois =="
for s in web worker rabbitmq; do
  echo "$s | id=$(id_de $s) | $(docker inspect --format 'iniciado={{.State.StartedAt}} | {{.Config.Image}} | revision={{index .Config.Labels "org.opencontainers.image.revision"}}' "$(docker compose ps -q $s)" | sed 's/@sha256:[0-9a-f]*//')"
done
[ "$(id_de web)" != "$E05_ID_web" ] && r_web=diferente || r_web=igual
[ "$(id_de worker)" != "$E05_ID_worker" ] && r_worker=diferente || r_worker=igual
[ "$(id_de rabbitmq)" = "$E05_ID_rabbitmq" ] && r_rabbit=igual || r_rabbit=diferente
chk "web é um contêiner novo (id)" diferente "$r_web"
chk "worker é um contêiner novo (id)" diferente "$r_worker"
chk "o RabbitMQ não foi recriado (id)" igual "$r_rabbit"
echo "== linha de log da versão (contêineres novos) =="
docker compose logs web worker 2>&1 | grep "Image commit"
echo "== o resultado anterior, com o MESMO token de antes da recriação =="
api GET "/api/reports/$REPORT_E01" "$T_E05"
echo "GET com o token anterior: HTTP $CODE"
if [ "$CODE" != "200" ]; then
  echo "o token anterior não valeu; nova autenticação (isso não invalida a prova de persistência do resultado)"
  login ana.i06@example.test "$TEST_PW"; T_E05=$TOK; api GET "/api/reports/$REPORT_E01" "$T_E05"; echo "GET com login novo: HTTP $CODE"
fi
E05_DEPOIS_DETALHE=$(printf '%s' "$RESP" | sha256sum | cut -c1-16)
printf '%s' "$RESP" | python -I -c "$PY_SHOW" 3 175.00
curl -s -o "$W/e05-depois.xlsx" -H "Authorization: Bearer $T_E05" "$BASE/api/reports/$REPORT_E01/excel"; E05_DEPOIS_XLSX=$(sheet_hash "$W/e05-depois.xlsx")
chk "hash do JSON do detalhe (antes e depois)" "$E05_ANTES_DETALHE" "$E05_DEPOIS_DETALHE"
chk "hash das células do Excel (antes e depois)" "$E05_ANTES_XLSX" "$E05_DEPOIS_XLSX"
```

**O que observar (esperado pelo contrato).**

- Web e worker com **ids de contêiner e horário de início novos**; o RabbitMQ com o mesmo id (não foi recriado). As três linhas `chk` dizem `OK`.
- Versão: o rótulo `org.opencontainers.image.revision` dos dois contêineres e a linha `Image commit` do log são o SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`, antes e depois.
- O relatório do E01 continua `Completed` com o mesmo `result` (3 itens e 175.00): hash do JSON do detalhe e das células do Excel iguais antes e depois.
- Token: com o `/keys` preservado, o mesmo token deve continuar valendo (`HTTP 200`); se não valer, a nova autenticação é aceita e registrada como veio.

**Saída real colada pelo Renan, parte 1 (E05-a):**

```
== E05-a: login novo da ana e o estado ANTES de recriar web e worker ==
login: HTTP 200
status=Completed scope=Mine createdAt=2026-11-03T18:09:44.076158Z startedAt=2026-11-03T18:09:45.96755Z completedAt=2026-11-03T18:09:46.276163Z
totalCount=3 totalAmount=175 generatedAt=2026-11-03T18:09:46.1549253Z
  Draft     count=0 amount=0
  Submitted count=0 amount=0
  Approved  count=2 amount=150
  Rejected  count=0 amount=0
  Paid      count=1 amount=25
esperado 3 itens e 175.00: OK
hash do JSON do detalhe : 225393dcc6004d6b
hash das células (Excel): ae3d22e2d17ba4e1
== contêineres antes ==
web | id=c7c38e21cac3 | iniciado=2026-11-03T17:52:00.653824754Z | ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
worker | id=5e5eff7f72fd | iniciado=2026-11-03T18:18:52.843633336Z | ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
rabbitmq | id=e9da1a9e8e44 | iniciado=2026-11-03T17:51:54.369066118Z | rabbitmq:4.4.0-management
== linha de log da versão (contêineres atuais; o worker aparece mais de uma vez porque este contêiner foi parado e religado nos E02 e E03, e o log fica no mesmo contêiner) ==
web-1     |       ExpenseHub web started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
worker-1  |       ExpenseHub worker started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
worker-1  |       ExpenseHub worker started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
worker-1  |       ExpenseHub worker started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
```

**Saída real colada pelo Renan, parte 2 (E05-b):**

```
== E05-b: recriar SÓ web e worker (sem remover volumes) ==
[+] up 3/3
 ✔ Container expensehub-rabbitmq-1 Healthy                                                                             2.3s
 ✔ Container expensehub-web-1      Healthy                                                                             7.3s
 ✔ Container expensehub-worker-1   Started                                                                             6.8s
web: healthy
== contêineres depois ==
web | id=f28c9d26aceb | iniciado=2026-11-03T18:29:20.941507984Z | ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f | revision=f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
worker | id=577614433ab7 | iniciado=2026-11-03T18:29:26.657281757Z | ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f | revision=f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
rabbitmq | id=e9da1a9e8e44 | iniciado=2026-11-03T17:51:54.369066118Z | rabbitmq:4.4.0-management | revision=
web é um contêiner novo (id)                                     esperado=diferente obtido=diferente OK
worker é um contêiner novo (id)                                  esperado=diferente obtido=diferente OK
o RabbitMQ não foi recriado (id)                                  esperado=igual  obtido=igual  OK
== linha de log da versão (contêineres novos) ==
web-1     |       ExpenseHub web started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
worker-1  |       ExpenseHub worker started. Image commit: f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f.
== o resultado anterior, com o MESMO token de antes da recriação ==
GET com o token anterior: HTTP 200
status=Completed scope=Mine createdAt=2026-11-03T18:09:44.076158Z startedAt=2026-11-03T18:09:45.96755Z completedAt=2026-11-03T18:09:46.276163Z
totalCount=3 totalAmount=175 generatedAt=2026-11-03T18:09:46.1549253Z
  Draft     count=0 amount=0
  Submitted count=0 amount=0
  Approved  count=2 amount=150
  Rejected  count=0 amount=0
  Paid      count=1 amount=25
esperado 3 itens e 175.00: OK
hash do JSON do detalhe (antes e depois)                           esperado=225393dcc6004d6b obtido=225393dcc6004d6b OK
hash das células do Excel (antes e depois)                        esperado=ae3d22e2d17ba4e1 obtido=ae3d22e2d17ba4e1 OK
```

Conferência extra que o Renan fez depois do E05 (só listagem, fora do roteiro; o `docker volume ls` não altera nada):

```
expensehub_expensehub-data
expensehub_expensehub-keys
expensehub_rabbitmq-data
```

**Resultado do E05: atendido.** Só web e worker foram recriados (ids `c7c38e21cac3` para `f28c9d26aceb` e `5e5eff7f72fd` para `577614433ab7`, com novos horários de início); o RabbitMQ manteve o id `e9da1a9e8e44` e o horário de início 17:51:54Z. A versão é a mesma antes e depois: as duas imagens com a tag `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`, o rótulo `org.opencontainers.image.revision` dos dois contêineres novos igual a esse SHA, e a linha `Image commit` do log igual a ele. O relatório do E01, consultado com **o mesmo token** emitido antes da recriação, respondeu `200` (não foi preciso autenticar de novo) e continuou `Completed` com o mesmo `result`: o hash do JSON do detalhe (`225393dcc6004d6b`) e o das células do Excel (`ae3d22e2d17ba4e1`) são iguais antes e depois, e iguais aos do E03 e do E01-b. Os três volumes continuaram a existir. Por que o token continuou valendo: ver a confirmação no código em [Diagnóstico, observações](#observações-que-os-experimentos-levantaram).

### Bloco Fim: limpeza das variáveis e do diretório temporário

```bash
set +x; set +H
echo "== estado final (os serviços continuam no ar; nada foi removido) =="
docker compose ps -a --format '{{.Service}} | estado={{.State}} | saude={{.Health}} | imagem={{.Image}}' | sed 's/@sha256:[0-9a-f]*//'
unset ADMIN_PW TEST_PW TOK T_ADMIN T_ANA T_BRUNO T_CAIO T_GESTOR T_E05 USERS
rm -rf "$W"
echo "variáveis de senha e de token apagadas; diretório temporário removido"
```

**Saída real colada pelo Renan:**

```
== estado final (os serviços continuam no ar; nada foi removido) ==
rabbitmq | estado=running | saude=healthy | imagem=rabbitmq:4.4.0-management
web | estado=running | saude=healthy | imagem=ghcr.io/renan-utida/expensehub-web:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
worker | estado=running | saude= | imagem=ghcr.io/renan-utida/expensehub-worker:f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f
variáveis de senha e de token apagadas; diretório temporário removido
```

Os três serviços continuaram no ar ao fim, com as mesmas imagens. Nenhum volume foi removido e nenhum comando de remoção (`down`, `docker volume rm`, `prune`) foi usado em todo o roteiro; a única recriação de contêineres foi a do E05, pedida pelo experimento (a frase "nada foi removido" que o bloco imprime se refere aos dados e aos volumes).

### Diagnóstico

O diagnóstico usa só o que os experimentos mostraram (com os ids e as linhas de log acima) e, onde diz expressamente, o que o código e os testes garantem. As partes (b) e (d) citam situações que **não** foram reproduzidas nesta sessão (broker indisponível, janela sem Outbox, mensagem `Unacked`); elas aparecem marcadas como "do código e dos testes" ou "da CP3-I03".

**(a) Aceitação e conclusão.** Aceitar não é concluir. O `POST` só responde `202` depois de o pedido estar gravado como `Pending` e de o broker ter confirmado a publicação; a conclusão é do worker, depois. A linha do tempo do E01, em UTC (as linhas de log são as do web e do worker; as de banco vêm do JSON do detalhe):

| Instante (18:09, UTC) | Origem | O que aconteceu |
|---|---|---|
| 44.076 | banco, `createdAt` | pedido gravado como `Pending` |
| 44.343 | log do web | `message confirmed by the broker` |
| 44.344 | log do web | `registered and published` (o `202` com `Location` só sai depois disso; o terminal não mediu o instante exato da resposta) |
| 44.404 | log do worker | `message received (redelivered: False)` |
| 45.968 | banco, `startedAt` | início gravado (o relatório passou a `Processing`) |
| 46.128 | log do worker | `processing started (resumed: False)` |
| 46.155 | banco, `result.generatedAt` | instante em que o resultado foi calculado |
| 46.276 | banco, `completedAt` | `Completed` gravado junto com o `result` |
| 46.290 | log do worker | `completed with 3 expenses` |
| 46.300 | log do worker | `message acknowledged after the result was saved or found saved` |

O que isso mostra: (1) o `202` quer dizer "aceito e publicado", não "concluído", e a prova está no E02, em que o `202` veio e o relatório ficou `Pending` por cerca de 12 s; (2) o relatório passou por `Pending`, `Processing` e `Completed` com o mesmo id; o `Pending` foi visto ao vivo no E02, mas o `Processing` **não foi capturado ao vivo** (a janela dura décimos de segundo e o acompanhamento consulta de 1 em 1 s), e a passagem por ele aparece pelo `startedAt` entre `createdAt` e `completedAt`, pela linha `processing started` e pelo código (`TryStartAsync` grava o início antes do cálculo); (3) `Completed` e `result` são gravados juntos: em todas as consultas dos cinco experimentos, um relatório `Completed` já veio com o `result`; (4) a confirmação da mensagem vem **depois** da gravação (46.300 depois de `completedAt` 46.276); (5) o resultado é salvo e não é recalculado na leitura: o `generatedAt`, o hash do JSON e o hash das células do Excel ficaram iguais em consultas feitas minutos depois (E03) e depois de recriar web e worker (E05), e o instante de geração da planilha (`2026-11-03T18:09:46Z`) é o do resultado salvo, não o do download; (6) o Excel sai do resultado salvo, sem nova mensagem: no E03-b, depois de três downloads do Excel (E01-b, E03-a e E03-b), o log do worker para esse id tem só as quatro linhas do E01 e as três da republicação.

**(b) Três falhas diferentes (e uma quarta).** Worker parado, broker indisponível e janela sem Outbox não são a mesma falha. O que cada uma deixa no banco e na fila:

| Falha | Resposta ao cliente | No banco | Na fila | Como se recupera | Fonte |
|---|---|---|---|---|---|
| Worker parado | `202` normal | relatório `Pending` | 1 mensagem `Ready`, 0 `Unacked` | religar o worker: a mensagem que esperava é entregue e o **mesmo id** chega a `Completed` | **observado no E02** (`1 0`, `Pending`, depois `Completed` e `0 0`) |
| Broker indisponível (publicação não confirmada) | `503` com o `id` do pedido salvo e erro sanitizado | relatório `Pending` | nenhuma mensagem | reiniciar o worker **não** recupera uma mensagem que nunca foi publicada; consulta-se o `id` e, se continuar `Pending`, pede-se outro relatório | do código e dos testes (`ReportsControllerTests.RequestAsync_NotPublished_Returns503WithTheIdentifierOfTheSavedReport`, `ReportScenarioTests.Scenario10_BrokerDoesNotConfirm_Is503WithTheIdAndTheReportStaysPending`) e observado na CP3-I03 (RabbitMQ parado: `503` com `id` em cerca de 2 s e o pedido ficou `Pending` para sempre) |
| Janela sem Transactional Outbox (queda do web entre gravar e publicar) | nenhuma (a conexão cai) ou erro | relatório `Pending` | nenhuma mensagem | não há reconciliação automática; só um novo pedido gera outro relatório | do código e do README; a janela está documentada na [CP3-I03](#detalhe-da-cp3-i03-rabbitmq-worker-e-idempotência) e não foi reproduzida |
| Banco indisponível antes de registrar | erro explícito (`503`) sem `202` e sem `id` inventado | nada gravado | nenhuma mensagem | repetir o pedido | do código e dos testes (`ReportsControllerTests.RequestAsync_StorageUnavailable_Returns503WithoutAnIdentifier`) |

Como separar as falhas olhando o relatório e a fila juntos: `Pending` com `Ready` maior que zero e nenhum consumidor é worker parado ou fora do ar; `Pending` com a fila vazia (`0 0`) e consumidor presente é uma mensagem que nunca chegou ao broker (a segunda ou a terceira linha da tabela); `Pending` com `Unacked` 1 é o instante curto em que o worker já recebeu a mensagem e ainda não gravou o início. Um `503` com `id` também **não garante** que a mensagem não chegou ao broker (a confirmação pode ter se perdido), então quem recebe `503` consulta o `id` antes de pedir outro. O único dos quatro casos que um experimento obrigatório cobre é o do worker parado; o tratamento da publicação sem confirmação existe no código e é verificado por teste, como a apostila pede, e um sexto experimento de interrupção do broker não é obrigatório.

**(c) Duplicata de mensagem e novo POST.** No E03, o mesmo `reportId` de um relatório já `Completed` foi publicado de novo pelo painel (18:24:34Z), depois de reiniciar o worker (18:18:52Z). O worker recebeu a mensagem (`redelivered: False`: para o broker é uma mensagem nova, não uma reentrega), leu o relatório, achou o estado `Completed` e registrou `is already Completed: duplicate message ignored, nothing was changed.`, confirmou a mensagem e não gravou nada: o JSON do detalhe, a listagem (2 relatórios) e as células do Excel ficaram idênticos, e a conclusão original aparece uma só vez no log. Esse reconhecimento não depende de memória do processo: o worker já era outro processo, então a prova é o estado gravado no banco (a idempotência é persistente). Já um **novo `POST /api/reports` não é duplicata**: o do E02 criou o relatório `56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f`, com id diferente do `2e1a02c1-aac4-40ca-b2c9-2e4043209fa3` e com ciclo próprio no log (recebida, início, conclusão, confirmação); o segundo `Completed` com os mesmos totais (3 e 175) é o efeito legítimo de uma solicitação nova, e por isso um novo `POST` não prova idempotência. A prova de idempotência é reaproveitar o `reportId` já concluído, que é o que o E03 fez.

**(d) `Ready` e `Unacked` do RabbitMQ não são `Pending` e `Processing` da aplicação.** São dois vocabulários sobre dois lugares. `Ready` é a mensagem disponível na fila e `Unacked` é a mensagem já entregue a um consumidor e ainda sem confirmação; `Pending` e `Processing` são o estado do relatório no banco. No E02 coincidiram (relatório `Pending` e uma mensagem `Ready`, `1 0`), mas não é equivalência: um relatório `Pending` pode não ter mensagem nenhuma (publicação não confirmada, ver (b)) e uma mensagem `Unacked` pode corresponder a um relatório ainda `Pending` (recebida, início ainda não gravado) ou `Processing`. A única medição de `Unacked` do projeto foi feita na CP3-I03: com o pedido `Processing` visível a mensagem estava sem confirmação (`ready 0, unacked 1, consumers 1`); com o worker morto, o pedido continuou `Processing` sem resultado e a mensagem voltou (`ready 1, unacked 0`), isto é, um relatório `Processing` com a mensagem `Ready`. Nestes cinco experimentos nenhum comando pegou `Unacked` maior que zero, porque a mensagem fica sem confirmação por pouco tempo: cerca de 40 ms do `createdAt` ao `completedAt` com o worker aquecido (E04, relatório do caio) e 1,4 s a 1,9 s entre `message received` e a confirmação na primeira mensagem de um processo (E02 e E01). Por isso a correlação é feita pelo id: o estado do relatório vem do `GET /api/reports/{id}`, a fila vem do `rabbitmqctl list_queues`, e o que liga os dois são as linhas do worker com o `reportId`.

### Observações que os experimentos levantaram

**1. O intervalo entre `message received` e a linha seguinte do worker.** O Renan notou que, depois de `message received`, a linha seguinte do worker demorou mais de 1 s no E01 e no E02 e menos de 1 s na duplicata, enquanto no E04 o `startedAt` do relatório do caio veio 28 ms depois do `createdAt`. Os números, calculados dos instantes dos logs e do banco acima:

| Execução | Processo do worker na hora | `message received` | Próxima linha do worker | Intervalo |
|---|---|---|---|---|
| E01 | primeira mensagem do processo (subiu às 17:52:06Z; ficou cerca de 17 min sem mensagens) | 18:09:44.404 | `processing started`, 18:09:46.128 | 1,724 s |
| E02 | primeira mensagem do processo (acabou de ser religado) | 18:17:38.705 | `processing started`, 18:17:40.019 | 1,314 s |
| E03 (duplicata) | primeira mensagem do processo (reiniciado às 18:18:52Z) | 18:24:34.432 | `duplicate message ignored`, 18:24:35.325 | 0,892 s |
| E04 (caio, `Global`) | mesmo processo, que já tinha tratado a duplicata e o relatório do bruno | o log não foi colado | `startedAt` do banco, 18:27:21.996 | 28 ms contados do `createdAt` do web (18:27:21.967), o que inclui publicar, entregar e ler o relatório |

O que o código mostra (verificado, sem afirmar mais do que isso):

- **Não há atraso fixo nem espera programada nesse caminho.** O `message received` é a primeira coisa de `OnReceivedAsync` (`ReportConsumerService.cs`); depois vêm `CreateScope`, a resolução do `ReportMessageProcessor`, a leitura do relatório (`FindAsync`) e o início (`TryStartAsync`, `ReportMessageProcessor.cs`), e só então a linha `processing started`. Os únicos `Task.Delay` do worker são o `Timeout.Infinite` que mantém a conexão aberta, a pausa antes de devolver uma mensagem que falhou de forma transitória (`TransientRetryDelaySeconds`) e a pausa antes de reconectar ao broker; nenhum roda quando a mensagem é tratada com sucesso.
- **Não há polling.** A entrega é por assinatura (`BasicConsumeAsync` com `AsyncEventingBasicConsumer`, `prefetch` 1) e a conexão e o canal são abertos quando o worker sobe, antes da linha de consumo, não a cada mensagem.
- **O `startedAt` é lido antes da primeira gravação.** `GetUtcNow()` é o argumento de `TryStartAsync`, então o `startedAt` marca o instante anterior à escrita. No E01, 1,563 s dos 1,724 s passaram **antes** dessa leitura de relógio (escopo, injeção de dependências e a primeira leitura do relatório) e 0,161 s depois (a primeira escrita condicional); no E02, 1,189 s antes e 0,125 s depois.

A explicação coerente com esses números é o **custo de primeiro uso por processo** do acesso a dados (montar o modelo do EF Core, abrir a conexão do SQLite com os `PRAGMA` e compilar o código na primeira passagem), que o caminho de duplicata paga em parte (só a leitura, 0,892 s) e que o processo já aquecido não paga (28 ms no E04). Sobre a hipótese de "ociosidade": o E02 foi a primeira mensagem logo depois de religar o worker, sem ociosidade nenhuma, e ainda assim levou 1,314 s, então a ociosidade não é necessária para o efeito; o que os três casos lentos têm em comum é serem a **primeira mensagem do processo** (o E03 esperou cerca de 5,7 min e o E01 cerca de 17 min depois de subir). Nenhum experimento isolou a ociosidade de um worker já aquecido (o caso do E04 teve o relatório do bruno segundos antes), então essa separação não está provada. Esta é uma explicação provável, **não medida** (não houve perfilador nem teste que isole o primeiro uso de um processo), e não é defeito: o contrato não depende desse tempo, e o efeito é só um `Pending` um a dois segundos mais longo na primeira mensagem depois de cada (re)início do worker.

**2. Por que o token continuou valendo depois de recriar o web (E05).** Confirmado no compose e no código, sem inferir além disso:

- O `AddBearerToken` do ASP.NET Core protege o token com o Data Protection (comportamento do framework; o registro está em `IdentityServiceCollectionExtensions.cs`, e o repositório não define outro protetor nem muda a validade padrão do token). As chaves do Data Protection são mantidas pelo repositório em `AddExpenseHubDataProtection` (`DeploymentServiceCollectionExtensions.cs`): quando `DataProtection:KeysPath` está definido, chama `AddDataProtection().SetApplicationName("ExpenseHub").PersistKeysToFileSystem(...)`, de modo que as chaves não nascem de novo a cada contêiner.
- O `compose.yaml` define `DataProtection__KeysPath: /keys` no serviço `web` e monta o volume nomeado `expensehub-keys` em `/keys`; o volume sobrevive ao `docker compose up -d --force-recreate web worker` (a listagem depois do E05 mostra `expensehub_expensehub-keys`). Os testes `KeyStorageTests.AddExpenseHubDataProtection_KeepsTheKeysInTheConfiguredDirectory` e `DeliveryFilesTests.Compose_OnlyTheWebApplicationMigratesAndKeepsTheKeys` travam as duas pontas (o código usa a pasta configurada e o compose a define e a monta só no web).
- O token do bloco 11 foi emitido depois do fim do E04 (18:27:22Z) e antes da recriação (18:29:20Z), e usado de novo logo depois dela, portanto bem dentro de 1 hora (a validade padrão do framework, que o repositório não altera).

Então o experimento mostrou que o token **continuou válido**, e a configuração e o código apontam as chaves persistidas no volume `expensehub-keys` como o motivo. O contrafactual (recriar sem o volume de chaves e ver o token falhar) **não foi testado**, de propósito, porque o experimento não pode remover volume.

## Entrega

Dados da entrega do Checkpoint 3 (o texto de entrega no Teams repete estes dados e acrescenta os que só existem depois do merge):

- **Commit do CP2:** `2e74619a311b38dbcc959888c8f1358b6877e9a1` (merge da PR 10 na `main`).
- **Commit final do CP3:** é o merge commit da PR da CP3-I07 na `main`, e o SHA completo dele vai no texto de entrega do Teams, não neste arquivo. O repositório não pode registrar o próprio SHA final sem mudá-lo (qualquer edição que o citasse criaria outro commit com outro SHA), e esse merge commit só passa a existir depois do merge.
- **Imagens** (publicadas pelo workflow `publish-images` para esse commit, com o SHA completo como tag, nunca `latest`):
  - `ghcr.io/renan-utida/expensehub-web:<SHA final completo>`
  - `ghcr.io/renan-utida/expensehub-worker:<SHA final completo>`
- **Acesso:** os dois pacotes são públicos; `docker pull` sem login e sem credenciais. Os experimentos rodaram com as imagens do SHA `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f`, e a diferença até o commit final é só documentação (veja [a prova](#prova-de-que-só-a-documentação-mudou-depois-dos-experimentos)).
- **Como subir:** [Rodar com Docker Compose (CP3-I05)](README.md#rodar-com-docker-compose-cp3-i05) no README, a partir do [`.env.example`](.env.example) (cópia para `.env` com `IMAGE_TAG` igual ao SHA final completo; os segredos ficam em branco no exemplo e são preenchidos só na máquina de quem roda).
- **Testes unitários:** `dotnet test ./sources/ExpenseHub.slnx` (1170 aprovados, sem banco, rede, RabbitMQ nem Docker).
- **Conferência depois do merge:** o `docker compose pull` com a tag do SHA final e a execução conferida são feitos pelo Renan depois do merge e ficam registrados no texto do Teams, não no repositório.
