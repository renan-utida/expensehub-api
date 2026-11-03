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
| 02/11/2026 | Migrações no Compose | A decidir no plano da CP3-I05, com só duas imagens publicadas e sem apagar dados | CP3-I05 |
| 02/11/2026 | Resultado salvo | Uma coluna `ResultJson` na linha do pedido, com centavos inteiros; `Completed` e resultado numa só gravação | CP3-I01 |
| 02/11/2026 | Cálculo | `ReportCalculator.Select` (consulta com o período inclusivo e o escopo) e `Summarize` (centavos); projeção estreita no banco, sem agrupar no SQL | CP3-I01 |
| 02/11/2026 | Entrada e respostas | `scope` só pelo nome exato; `503` com `id` quando o broker não confirma (continua `Pending`) e `503` sem `id` quando o banco falha | CP3-I01 |
| 02/11/2026 | Autorização da CP3-I01 | Só autenticação e dono; a regra de escopo por papel fica para a CP3-I02 | CP3-I01 |
| 02/11/2026 | Matriz de role por escopo | Classe pura `ReportAccess`: `Mine` exige `Employee`, `Global` exige `Auditor`, quem tem as duas pede os dois, e Admin, Approver e Finance não somam nada; os nomes das roles são comparados exatamente | CP3-I02 |
| 02/11/2026 | Ordem das respostas do `POST` | `401`, `403` por role (atributo da rota, antes do corpo), `400`, `403` por escopo, depois `202` ou `503`; o serviço repete a ordem sozinho, sem depender do atributo | CP3-I02 |
| 02/11/2026 | Leitura sem role | Listar, detalhar e baixar o Excel exigem só autenticação e posse, mesmo para o Auditor; relatório alheio e inexistente dão o mesmo `404` | CP3-I02 |
| 02/11/2026 | Consulta do worker | `ReportQuery.From` monta a consulta só do pedido gravado (solicitante, escopo e período); a mensagem leva apenas o `reportId`, e o worker nunca relê as roles | CP3-I02 e CP3-I03 |

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

Issue central: [12](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/12). Branch: `cp3-i02`. Pontos: 15. Estado: Concluída na branch, aguardando a PR. A regra de role por escopo e o isolamento entram aqui; o worker que usa o pedido gravado chega na CP3-I03, e até lá um `POST` autorizado e válido responde `503` com o `id`, com o pedido `Pending`.

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

Issue central: [13](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/13). Branch: `cp3-i03`. Pontos: 20. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Publicação confirmada, fila durável e mensagens persistentes | 5 | Pendente | A preencher ao concluir a issue |
| Worker processa; consumo confirmado só depois de salvar | 5 | Pendente | A preencher ao concluir a issue |
| Conclusão e resultado consistentes; falha definitiva registrada | 5 | Pendente | A preencher ao concluir a issue |
| Duplicata reconhecida sem novo efeito, com proteção persistente | 5 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. Inclui a janela sem Transactional Outbox entre salvar e publicar.

## Detalhe da CP3-I04: Testes próprios significativos

Issue central: [14](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/14). Branch: `cp3-i04`. Pontos: 15. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Cálculo, conjunto vazio e validação de período | 5 | Pendente | A preencher ao concluir a issue |
| Negação por perfil e escopo; isolamento por solicitante | 5 | Pendente | A preencher ao concluir a issue |
| Duplicata, resultado persistido e Excel, com asserções de comportamento | 5 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. Inclui a tabela cenário por teste e o comando `dotnet test`.

## Detalhe da CP3-I05: Publicação e execução reproduzível

Issue central: [15](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/15). Branch: `cp3-i05`. Pontos: 15. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Duas imagens publicadas e identificadas pelo commit, com testes antes da publicação | 5 | Pendente | A preencher ao concluir a issue |
| Compose executa as imagens publicadas e o RabbitMQ com configuração documentada | 5 | Pendente | A preencher ao concluir a issue |
| Banco e armazenamento compatíveis, inicialização reproduzível e segredos externos | 5 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue.

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
