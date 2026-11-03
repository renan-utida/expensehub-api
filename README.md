# ExpenseHub

[![code-quality](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml/badge.svg)](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml)

API REST corporativa de reembolso de despesas, desenvolvida para a disciplina de C# da FIAP (turma 3ESPW) com ASP.NET Core (.NET 10), Entity Framework Core e SQLite. O projeto evolui por checkpoints: o Checkpoint 2 entregou a API de reembolsos, e o Checkpoint 3 a coloca em operação, com relatórios assíncronos, RabbitMQ, um worker C# e execução por imagens Docker.

Estado atual: o Checkpoint 2 está concluído (10 issues, 611 testes unitários e workflow `code-quality` com 100/100 na `main`). O Checkpoint 3 está em andamento: a preparação (CP3-00) está na `main` (PR #11 da equipe), a CP3-I01 (rotas de relatório, cálculo, resultado salvo e Excel) está na `main` (PR #12 da equipe, workflow `code-quality` com 100/100), a CP3-I02 (matriz de role por escopo, ordem das respostas e isolamento da leitura) está na `main` (PR #13 da equipe, workflow `code-quality` com 100/100), a CP3-I03 (RabbitMQ, worker e idempotência) está concluída na branch `cp3-i03`, aguardando a PR, e as issues CP3-I04 a CP3-I07 ainda não começaram. Com o RabbitMQ e o worker no ar, `POST /api/reports` responde `202` e o relatório chega a `Completed`; os comandos para rodar os dois localmente estão em [Mensageria e worker](#mensageria-e-worker-cp3-i03), e os do Docker Compose entram na CP3-I05.

## Sumário

- [Equipe](#equipe)
- [Checkpoint 2: API de reembolso de despesas](#checkpoint-2-api-de-reembolso-de-despesas)
- [Checkpoint 3: ExpenseHub em operação](#checkpoint-3-expensehub-em-operação)
- [Endpoints](#endpoints)
- [Como rodar](#como-rodar)
- [Banco de dados](#banco-de-dados)
- [Arquitetura](#arquitetura)
- [Testes](#testes)
- [Qualidade](#qualidade)
- [Solução de problemas](#solução-de-problemas)
- [Uso de IA](#uso-de-ia)

## Equipe

| Nome | RM | GitHub | Checkpoint 2 | Checkpoint 3 |
|---|---|---|---|---|
| Renan Dias Utida | 558540 | [renan-utida](https://github.com/renan-utida) | I01, I07, I08, I09 e I10 | CP3-I01 a CP3-I07 |
| Pedro Almeida e Camacho | 556831 | [Pedro-Camacho](https://github.com/Pedro-Camacho) | I02, I03, I04, I05 e I06 | Não participa |

## Checkpoint 2: API de reembolso de despesas

O Checkpoint 2 entregou a API de reembolsos: persistência com SQLite e Entity Framework Core; Identity com login por token bearer, cinco roles e cadastro sem roles; criação, edição, envio, aprovação, reprovação e pagamento de despesas pela máquina de estados `Draft`, `Submitted`, `Approved`, `Rejected` e `Paid`; matriz de acesso por perfil, dono e estado na camada de serviço; histórico por despesa; e 611 testes unitários. O registro completo, com as decisões e as evidências de cada issue, está em [CHECKPOINT-2.md](CHECKPOINT-2.md). A especificação da disciplina está em [docs/REQUISITOS.md](docs/REQUISITOS.md).

| Issue | Título | Requisito | Peso | Estado | PR | Detalhe |
|---|---|---|---:|---|---|---|
| I01 | Fundação da solução e Entity Framework Core | Solução com API e testes, SQLite com EF Core, entidades mínimas e migrations reproduzíveis | 4% | Concluída | [#1](https://github.com/renan-utida/expensehub-api/pull/1) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i01-fundação-da-solução-e-entity-framework-core) |
| I02 | Identity, Admin e autenticação | Identity relacional, login bearer, cinco roles, seed idempotente do Admin com senha vinda de configuração, `401` e `403` | 9% | Concluída | [#2](https://github.com/renan-utida/expensehub-api/pull/2) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i02-identity-admin-e-autenticação) |
| I03 | Cadastro HTTP e gerenciamento de roles | Cadastro público sem roles; listagem de usuários e troca de roles só pelo Admin; novo login depois da troca | 8% | Concluída | [#3](https://github.com/renan-utida/expensehub-api/pull/3) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i03-cadastro-http-e-gerenciamento-de-roles) |
| I04 | Criar e editar rascunho | Criar e editar rascunho (`Draft`) pelo Employee, dono vindo do token, validações e DTOs sem mass assignment | 7% | Concluída | [#4](https://github.com/renan-utida/expensehub-api/pull/4) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i04-criar-e-editar-rascunho) |
| I05 | Enviar, listar e consultar | Enviar, listar e consultar com filtro por perfil aplicado na consulta, `404` fora do escopo e `409` em transição repetida | 7% | Concluída | [#5](https://github.com/renan-utida/expensehub-api/pull/5) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i05-enviar-listar-e-consultar) |
| I06 | Ownership e matriz de acesso | Matriz de acesso por perfil, dono e estado na camada de serviço, sem acesso implícito do Admin | 10% | Concluída | [#6](https://github.com/renan-utida/expensehub-api/pull/6) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i06-ownership-e-matriz-de-acesso) |
| I07 | Aprovar e reprovar com justificativa | Aprovar e reprovar (justificativa de 10 a 500 caracteres) pelo Approver que não é dono, sem histórico duplicado | 12% | Concluída | [#7](https://github.com/renan-utida/expensehub-api/pull/7) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i07-aprovar-e-reprovar-com-justificativa) |
| I08 | Pagamento e histórico | Pagar (`Approved` para `Paid`) pelo Finance que não é dono, com `PaymentRecord`, e histórico por despesa na mesma gravação | 8% | Concluída | [#8](https://github.com/renan-utida/expensehub-api/pull/8) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i08-pagamento-e-histórico) |
| I09 | Testes unitários | Testes unitários das regras, sem banco, rede ou serviço externo | 10% | Concluída | [#9](https://github.com/renan-utida/expensehub-api/pull/9) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i09-testes-unitários) |
| I10 | Qualidade de Código | Build sem avisos, sem segredos nem arquivos gerados, score do workflow `code-quality` | 25% | Concluída | [#10](https://github.com/renan-utida/expensehub-api/pull/10) | [Detalhe](CHECKPOINT-2.md#detalhe-da-i10-qualidade-de-código) |

## Checkpoint 3: ExpenseHub em operação

O Checkpoint 3, ExpenseHub em operação, adiciona relatórios de reembolso assíncronos. `POST /api/reports` registra o pedido e publica uma mensagem no RabbitMQ; um worker C# separado consome a mensagem, calcula o resumo por período e por estado e o persiste; o usuário acompanha o andamento, consulta o resultado e baixa uma planilha Excel (`.xlsx`). A aplicação roda a partir de duas imagens Docker (web e worker) com Docker Compose. Prazo: 04/11/2026, às 23:59, no Teams. O CP3 é feito pelo Renan, e a nota-base é coletiva. O registro por issue, as decisões e os experimentos E01 a E05 ficam em [CHECKPOINT-3.md](CHECKPOINT-3.md).

| Issue | Título | Requisito | Pontos | Estado | PR | Detalhe |
|---|---|---|---:|---|---|---|
| CP3-I01 (central [11](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/11)) | Contrato e resultado do relatório | Rotas `POST` e `GET /api/reports`, validação de período e escopo, resumo por estado com totais, resultado salvo sem recálculo e download `.xlsx` | 20 | Concluída | PR #12 | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-i01-contrato-e-resultado-do-relatório) |
| CP3-I02 (central [12](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/12)) | Autorização e isolamento | `Mine` para Employee e `Global` para Auditor; listagem, detalhe e Excel só do solicitante (`404` para alheios); solicitante e escopo definidos pelo servidor | 15 | Concluída | PR #13 | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-i02-autorização-e-isolamento) |
| CP3-I03 (central [13](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/13)) | RabbitMQ, worker e idempotência | Fila durável, mensagens persistentes e confirmação de publicação; worker separado, ack só depois de salvar, duplicata sem novo efeito e falha definitiva registrada | 20 | Concluída | n/d | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-i03-rabbitmq-worker-e-idempotência) |
| CP3-I04 (central [14](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/14)) | Testes próprios significativos | Testes unitários de cálculo, conjunto vazio, período, negação, isolamento, duplicata, resultado persistido e Excel | 15 | Pendente | n/d | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-i04-testes-próprios-significativos) |
| CP3-I05 (central [15](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/15)) | Publicação e execução reproduzível | Duas imagens (web e worker) com a tag do commit, Compose com RabbitMQ, banco compartilhado e instruções sem segredos | 15 | Pendente | n/d | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-i05-publicação-e-execução-reproduzível) |
| CP3-I06 (central [16](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/16)) | Experimentos e diagnóstico | E01 a E05 com evidências reais e diagnóstico da aceitação, da publicação e da janela sem Outbox | 10 | Pendente | n/d | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-i06-experimentos-e-diagnóstico) |
| CP3-I07 (central [17](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/17)) | Qualidade objetiva das alterações | Sem diagnósticos novos atribuíveis ao trabalho, convenções seguidas, analisadores preservados e entrega só com fontes | 5 | Pendente | n/d | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-i07-qualidade-objetiva-das-alterações) |
| CP3-EXTRA-LINKEDIN (central [18](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/18)) | Publicação individual no LinkedIn | Publicação própria no LinkedIn sobre a aplicação (opcional, manual, sem branch nem PR) | +1 individual | Pendente | n/d | [Detalhe](CHECKPOINT-3.md#detalhe-da-cp3-extra-linkedin-publicação-individual-no-linkedin) |

## Endpoints

| Método | Rota | Descrição | Issue |
|---|---|---|---|
| GET | `/health` | Confirma que a aplicação iniciou; não acessa o banco | Esqueleto inicial |
| POST | `/register` | Público. Recebe `email` e `password` e cria o usuário **sem nenhuma role**; qualquer role enviada no corpo é ignorada. `201` com `id`, `email` e `roles` vazia; entrada inválida ou senha fora da política: `400`; e-mail já cadastrado: `409` | I03 |
| POST | `/login` | Público. Recebe `email` e `password` e devolve o token bearer. Credencial inválida: `401`; corpo inválido: `400` | I02 |
| GET | `/api/admin/users` | Somente `Admin`. Lista `id`, `email` e `roles` dos usuários, ordenados por e-mail. Sem token: `401`; sem a role: `403` | I02 e I03 |
| PUT | `/api/admin/users/{id}/roles` | Somente `Admin`. Recebe `{"roles": [...]}` e **substitui** as roles do usuário: as listadas são atribuídas e as omitidas são removidas. Role desconhecida ou lista ausente: `400`; usuário inexistente: `404`; Admin removendo a própria role Admin: `403`. O usuário afetado precisa fazer login de novo | I03 |
| POST | `/api/expenses` | Somente `Employee`. Recebe `description`, `amount` e `expenseDate` e cria um rascunho (`Draft`) cujo dono é o usuário do token. `201` com a despesa e o cabeçalho `Location` (`/api/expenses/{id}`); dados inválidos: `400`; sem token: `401`; sem a role: `403` | I04 e I05 |
| PUT | `/api/expenses/{id}` | Somente `Employee`. **Substitui** `description`, `amount` e `expenseDate` de um rascunho do próprio usuário. `200` com a despesa; dados inválidos: `400`; inexistente: `404`; de outro dono: `403`; fora de `Draft`: `409`; sem token: `401`; sem a role: `403` | I04, I06 e I07 |
| POST | `/api/expenses/{id}/submit` | Somente `Employee`. Envia um rascunho do próprio usuário: `Draft` para `Submitted`, sem corpo (o estado nunca vem do cliente). `200` com a despesa; inexistente: `404`; de outro dono: `403`; fora de `Draft`, inclusive reenvio e envio simultâneo: `409`; sem token: `401`; sem a role: `403` | I05 e I07 |
| POST | `/api/expenses/{id}/approve` | Somente `Approver`, e nunca o dono da despesa. Aprova uma despesa `Submitted`: `Submitted` para `Approved`, sem corpo (o aprovador e o horário vêm do token e do servidor). `200` com a despesa; inexistente: `404`; despesa do próprio usuário: `403`; qualquer outro estado, inclusive `Draft`, aprovação repetida e decisão simultânea: `409`; sem token: `401`; sem a role: `403` | I07 |
| POST | `/api/expenses/{id}/reject` | Somente `Approver`, e nunca o dono da despesa. Reprova uma despesa `Submitted`: `Submitted` para `Rejected`, com o corpo `{"reason": "..."}` (de 10 a 500 caracteres depois de aparar os espaços), que fica só no histórico. `200` com a despesa; corpo ausente ou `reason` inválido: `400`; inexistente: `404`; despesa do próprio usuário: `403`; qualquer outro estado, inclusive `Draft`, reprovação repetida e decisão simultânea: `409`; sem token: `401`; sem a role: `403` | I07 |
| POST | `/api/expenses/{id}/pay` | Somente `Finance`, e nunca o dono da despesa. Paga uma despesa `Approved`: `Approved` para `Paid`, sem corpo (quem paga e o horário vêm do token e do servidor). Grava o pagamento, a mudança de estado e a linha `Paid` do histórico na mesma gravação. `200` com `expenseId`, `status`, `actorId` e `paidAtUtc`; inexistente: `404`; despesa do próprio usuário: `403`; qualquer outro estado, inclusive `Draft`, pagamento repetido e pagamento simultâneo: `409`; sem token: `401`; sem a role: `403` | I08 |
| GET | `/api/expenses` | `Employee`, `Approver`, `Finance` ou `Auditor`. Lista as despesas do escopo do perfil, da mais nova para a mais antiga: Employee as próprias, Approver as `Submitted`, Finance as `Approved` e `Paid`, Auditor todas; roles acumuladas somam os filtros. O Admin sozinho e quem não tem role recebem `403`; sem token: `401` | I05 |
| GET | `/api/expenses/{id}` | Mesmas roles da listagem. `200` com a despesa; inexistente ou fora do escopo: `404`, com a mesma resposta nos dois casos, para não revelar que o recurso existe; `403` para quem não lê despesas; `401` sem token | I05 |
| GET | `/api/expenses/{id}/history` | Mesmas roles da listagem, com a visibilidade da despesa. `200` com as entradas do histórico, da mais antiga para a mais nova (`id`, `expenseId`, `action`, `actorId`, `occurredAtUtc`, `previousStatus`, `newStatus`, `reason` e `changes`), só com identificadores internos e sem e-mail; inexistente ou fora do escopo: `404`, com a mesma resposta nos dois casos; `403` para quem não lê despesas; `401` sem token | I08 |
| POST | `/api/reports` | `Employee` ou `Auditor`; cada escopo exige a sua role: `Mine` para `Employee` e `Global` para `Auditor`, quem tem as duas pede os dois, e Admin, Approver ou Finance sozinhos não pedem nada. Solicita um relatório com `{"fromDate": "2026-09-01", "toDate": "2026-09-30", "scope": "Mine"}`: as datas são `yyyy-MM-dd`, inclusivas e comparadas com `expenseDate`; `scope` é exatamente `Mine` ou `Global`. O solicitante vem do token (`ownerId` e `requesterId` no corpo são ignorados). Salva o pedido como `Pending`, publica a mensagem e só responde `202`, com `Location` e o envelope, depois que o broker confirma. Sem confirmação: `503` com o `id` do pedido salvo (que continua `Pending`). Banco indisponível: `503` sem `id`. Datas ou escopo inválidos, ausentes ou inexistentes, e `scope` numérico: `400`, sem salvar nem publicar; sem token: `401`; sem nenhuma role que possa pedir: `403`, antes de validar o corpo; role que não permite o escopo pedido (`Global` sem `Auditor`, `Mine` sem `Employee`): `403`, depois da validação, sem salvar nem publicar (ordem completa na seção abaixo). Um `503` com `id` não garante que a mensagem não chegou ao broker (a confirmação pode ter se perdido): o relatório ainda pode ser concluído, então consulte o `id` retornado antes de pedir outro, porque um novo `POST` cria outro id (veja [a janela sem Outbox](#mensageria-e-worker-cp3-i03)) | CP3-I01 e CP3-I03 |
| GET | `/api/reports` | Autenticado, sem exigir role (só posse). `200` com os envelopes dos relatórios do próprio usuário, do mais novo para o mais antigo; relatórios de outros usuários nunca aparecem; sem token: `401` | CP3-I01 |
| GET | `/api/reports/{id}` | Autenticado, sem exigir role (só posse). `200` com o envelope (`id`, `scope`, `fromDate`, `toDate`, `status`, `createdAt`, `startedAt`, `completedAt`, `result` e `error`, com instantes em UTC terminando em `Z`) e o resultado que foi salvo, sem recalcular; relatório inexistente ou de outro usuário: `404`, com a mesma resposta nos dois casos; sem token: `401` | CP3-I01 |
| GET | `/api/reports/{id}/excel` | Autenticado, sem exigir role (só posse). `200` com a planilha `.xlsx` (`Content-Type` `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` e `Content-Disposition` `attachment` com `report-{id}.xlsx`), montada do resultado salvo; relatório próprio que não está `Completed` (`Pending`, `Processing` ou `Failed`): `409`; inexistente ou de outro usuário: `404`; sem token: `401` | CP3-I01 |

Esta tabela lista todos os endpoints implementados.

### Relatórios de reembolso (CP3-I01)

- O cálculo soma em centavos inteiros e devolve os cinco estados (`Draft`, `Submitted`, `Approved`, `Rejected`, `Paid`), cada um com `count` e `amount`, sempre presentes e com zero quando vazios; `totalCount` e `totalAmount` são a soma dos estados. O período filtra `ExpenseDate`, com os dois limites inclusivos, e nunca a data de criação. O escopo `Mine` soma só as despesas do solicitante; `Global` soma as de todos.
- O resultado fica salvo na própria linha do pedido (coluna `ResultJson`, com centavos inteiros e o instante de geração). Consultar, listar e baixar o Excel leem esse resultado e não leem as despesas de novo.
- Biblioteca do Excel: `DocumentFormat.OpenXml` 3.5.1, licença MIT (traz `DocumentFormat.OpenXml.Framework` e `System.IO.Packaging`, também MIT). A planilha tem uma folha "Resumo" com identificador, escopo, período, instante de geração e a tabela dos cinco estados com o total geral; quantidades e valores são células numéricas.
- A migration `AddReportRequests` cria a tabela `ReportRequests`: rode `dotnet ef database update` depois de atualizar o código.
- A publicação fica atrás de `IReportPublisher`. O web registra o publicador do RabbitMQ (CP3-I03); sem um publicador real registrado, `UnavailableReportPublisher` nunca confirma e o `POST` autorizado e válido responde `503` com o `id` e o pedido fica `Pending`.
- Requisições de exemplo estão em `sources/ExpenseHub.Api/ExpenseHub.Api.http` (login do Employee e do Auditor, `Mine`, `Global`, os dois `403` e o `404` do relatório alheio); as credenciais ficam num arquivo local `http-client.env.json.user`, que o git ignora.

### Autorização dos relatórios (CP3-I02)

Quem pode pedir cada escopo é decidido pela classe `ReportAccess` (sem EF nem web), testada com uma tabela escrita à mão das 32 combinações das cinco roles:

| Roles do usuário | `Mine` | `Global` |
|---|---|---|
| Employee | Sim | Não |
| Auditor | Não, salvo se também for Employee | Sim |
| Employee e Auditor | Sim | Sim |
| Admin, Approver ou Finance, sem Employee nem Auditor | Não | Não |

- As roles acumulam e o nome é comparado exatamente. O solicitante vem sempre do token: `ownerId`, `requesterId` e `status` no corpo são ignorados. O solicitante e o escopo ficam gravados no pedido, e o worker (CP3-I03) monta a consulta só com o que está gravado (`ReportQuery.From`): ele nunca relê as roles do usuário nem confia na mensagem, que leva só o `reportId`.
- **Ordem das respostas do `POST /api/reports`**, a mesma do CP2: (1) `401` sem token; (2) `403` quando o usuário não tem `Employee` nem `Auditor`, pelo atributo `[Authorize(Roles = "Employee,Auditor")]` da rota, que roda antes de o corpo ser lido e por isso vem antes de qualquer `400`; (3) `400` para datas ou escopo inválidos; (4) `403` quando a role não permite o escopo pedido; (5) gravação e publicação (`202`, ou `503` com o `id`). Nos dois `403` nada é gravado nem publicado. O serviço repete a ordem (role, validação, escopo) e não depende só do atributo.
- Listar, detalhar e baixar o Excel não exigem role, só autenticação e posse: cada usuário vê apenas os relatórios que pediu, inclusive o Auditor, cujo privilégio é sobre os dados do relatório dele e não sobre os relatórios dos outros. Relatório alheio e inexistente dão o mesmo `404`; o `409` do Excel é só para o dono.
- Quem teve a role mudada precisa entrar de novo para o token trazer as roles novas (comportamento do CP2).

### Mensageria e worker (CP3-I03)

O `POST /api/reports` grava o pedido como `Pending`, publica `{"reportId": "<guid>"}` no RabbitMQ e só responde `202` (com `Location`) depois que o broker **confirma** a mensagem. O worker (`sources/ExpenseHub.Worker`) consome a fila, lê o período, o escopo e o solicitante do pedido **gravado** (a mensagem leva só o id), calcula com a mesma `ReportCalculator`, grava `Completed` e o resultado numa só gravação e só então confirma o consumo.

**Fila e mensagem**

| Item | Valor |
|---|---|
| Fila | `expensehub.reports` (chave `RabbitMq:QueueName`). Web e worker a declaram com os mesmos argumentos: durável, não exclusiva, sem exclusão automática, no exchange padrão (a chave de roteamento é o nome da fila) |
| Mensagem | `{"reportId":"<guid>"}`, `application/json`, **persistente** (`delivery_mode` 2). Nada além do id: qualquer outro campo recebido é ignorado |
| Publicação | conexão aberta na primeira publicação, canal com **confirmação de publicação**, publicação `mandatory` e espera de `RabbitMq:PublishTimeoutSeconds` (5 s). Mensagem recusada, devolvida, sem confirmação a tempo ou broker fora: `503` com o `id`, e o pedido continua `Pending` |
| Consumo | confirmação manual, uma mensagem por vez (prefetch 1) e reconexão automática. A mensagem só é confirmada depois que o resultado ou o erro está gravado |

**Configuração.** Seção `RabbitMq`, igual no web e no worker; o worker não inicia se estiver incompleta, e o web responde `503` com o `id` e registra o motivo.

| Chave | Variável de ambiente | Padrão | Observação |
|---|---|---|---|
| `RabbitMq:Host` | `RabbitMq__Host` | `localhost` | |
| `RabbitMq:Port` | `RabbitMq__Port` | `5672` | |
| `RabbitMq:VirtualHost` | `RabbitMq__VirtualHost` | `/` | |
| `RabbitMq:QueueName` | `RabbitMq__QueueName` | `expensehub.reports` | |
| `RabbitMq:UserName` | `RabbitMq__UserName` | nenhum | Só de variável de ambiente ou user-secrets. Nunca de arquivo versionado |
| `RabbitMq:Password` | `RabbitMq__Password` | nenhum | Idem |
| `RabbitMq:PublishTimeoutSeconds` | `RabbitMq__PublishTimeoutSeconds` | `5` | Espera pela confirmação do broker |
| `RabbitMq:TransientRetryDelaySeconds` | `RabbitMq__TransientRetryDelaySeconds` | `5` | Espera do worker antes de devolver uma mensagem que não pôde ser salva |
| `ConnectionStrings:ExpenseHub` | `ConnectionStrings__ExpenseHub` | só no web: `Data Source=expensehub.db` | **Mesmo arquivo nos dois, por caminho absoluto** (abaixo) |

Para guardar as credenciais do broker fora do repositório (o worker e a Api usam o mesmo `UserSecretsId`, então um `set` serve aos dois; o user-secrets só é lido no ambiente `Development`):

```shell
dotnet user-secrets set "RabbitMq:UserName" "<usuário do broker>" --project ./sources/ExpenseHub.Worker/ExpenseHub.Worker.csproj
dotnet user-secrets set "RabbitMq:Password" "<senha do broker>" --project ./sources/ExpenseHub.Worker/ExpenseHub.Worker.csproj
```

**Web e worker precisam apontar para o mesmo arquivo de banco, por caminho absoluto na connection string**, definido por variável de ambiente nos dois processos. Um caminho relativo é resolvido contra a pasta de cada projeto e daria dois bancos diferentes: o worker não acharia os pedidos (a mensagem seria descartada) e os relatórios ficariam `Pending`. O worker avisa no log quando o caminho é relativo.

```shell
export ConnectionStrings__ExpenseHub="Data Source=/caminho/absoluto/expensehub.db"
```

No PowerShell: `$env:ConnectionStrings__ExpenseHub = "Data Source=C:\caminho\absoluto\expensehub.db"`.

**Como rodar localmente.** Rode a partir da raiz do repositório, com o banco já migrado (`dotnet ef database update`).

1. RabbitMQ, com um volume **nomeado** para os dados (defina `RABBITMQ_USER` e `RABBITMQ_PASSWORD` no terminal antes, sem escrevê-los em arquivo):

```shell
docker run -d --name expensehub-rabbitmq \
  -p 5672:5672 -p 15672:15672 \
  -v expensehub-rabbitmq-data:/var/lib/rabbitmq \
  -e RABBITMQ_DEFAULT_USER="$RABBITMQ_USER" \
  -e RABBITMQ_DEFAULT_PASS="$RABBITMQ_PASSWORD" \
  rabbitmq:4-management
```

O broker guarda tudo em `/var/lib/rabbitmq` dentro do contêiner: as definições (a fila durável) e as mensagens persistentes que ainda não foram consumidas. O volume nomeado `expensehub-rabbitmq-data` monta esse caminho fora do contêiner, então os dados sobrevivem a `docker stop` e `docker start` e também a recriar o contêiner com o mesmo `-v`. Sem o volume (ou com um volume anônimo, que se perde ao remover o contêiner) a fila e as mensagens pendentes somem junto com o contêiner. As duas variáveis `RABBITMQ_DEFAULT_*` só valem quando o volume é criado; depois disso o usuário já está nos dados do volume. Não rode `docker volume rm` nem `docker rm -v` neste volume: eles apagam as mensagens. O painel de gerenciamento fica em `http://localhost:15672`. A tag `4-management` acompanha a versão 4 do RabbitMQ; a CP3-I05 fixa a versão no Compose.

2. Web e worker, cada um num terminal, com as mesmas variáveis (`ConnectionStrings__ExpenseHub` absoluta, `RabbitMq__UserName`, `RabbitMq__Password` e a senha do Admin do web):

```shell
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
dotnet run --project ./sources/ExpenseHub.Worker/ExpenseHub.Worker.csproj
```

**Desfechos, duplicata e confirmação.** O processador (`ReportMessageProcessor`, na Api) devolve um desfecho, e a classe `ReportDelivery` decide a confirmação; o consumidor só executa a decisão.

| Desfecho | Quando | O que o worker faz com a mensagem |
|---|---|---|
| `Completed` | o relatório foi calculado e `Completed` e o resultado foram gravados juntos | confirma |
| `Duplicate` | o relatório já estava `Completed` ou `Failed`, ou outro worker o concluiu antes: nada é lido nem gravado | confirma |
| `PermanentFailure` | o pedido gravado é incoerente (por exemplo, sem solicitante) ou o cálculo é impossível | grava `Failed` com `error.code` estável (`InvalidRequest` ou `ProcessingFailed`) e mensagem fixa, e confirma. Não volta |
| `Discarded` | mensagem ilegível ou pedido inexistente | recusa sem devolver (não há fila de mensagens não processáveis) e registra |
| `TransientFailure` | banco ocupado, trancado ou inacessível (o resultado não está gravado) | **não confirma**: espera `TransientRetryDelaySeconds` (5 s) e devolve a mensagem. Enquanto o banco estiver fora tenta de novo a cada 5 s, sem laço quente. Nunca vira `Failed` |

A duplicata não depende da memória do worker: a prova é o estado gravado no banco, então reiniciar o worker não a elimina. Se o worker cair depois de receber e antes de confirmar, o broker devolve a mensagem; o cálculo pode se repetir, o efeito gravado não.

**Retomada de `Processing`.** Se o worker parar no meio (pedido já `Processing`, mensagem sem confirmação), o broker devolve a mensagem e, na entrega seguinte, o processador vê `Processing`, recalcula e tenta concluir. A gravação final só vale se o estado ainda for `Processing`, então mesmo com dois workers o efeito gravado é um só. Não há limite de idade: quem decide é a entrega da mensagem.

**Janela sem Transactional Outbox.** Salvar o pedido e publicar a mensagem são duas operações separadas. Uma queda entre as duas deixa o pedido `Pending` sem mensagem, e reiniciar o worker **não** recria uma mensagem que nunca existiu; um novo `POST` cria outro id. Um `503` com `id` também **não garante que a mensagem não chegou ao broker**: a confirmação pode ter se perdido (o broker gravou a mensagem e a conexão caiu antes da resposta), e nesse caso o worker ainda conclui o relatório. Por isso quem recebe `503` deve consultar o `id` retornado (`GET /api/reports/{id}`) antes de pedir outro. Transactional Outbox, retentativas completas e fila de mensagens não processáveis são opcionais e não foram feitos.

**SQLite compartilhado.** Web e worker abrem o mesmo arquivo pelo mesmo método (`AddExpenseHubPersistence`), então têm a mesma configuração: em cada conexão rodam `PRAGMA busy_timeout = 5000` e, **depois dele**, `PRAGMA journal_mode = WAL` (a troca de modo precisa de um bloqueio, e a conexão já precisa saber quanto esperar por ele se o outro processo abrir o banco ao mesmo tempo), e a espera de uma gravação também é limitada em 5 s (`Default Timeout` da connection string, quando não definido). Com o WAL, quem lê não é bloqueado por quem grava. O modo fica gravado no arquivo, e aparecem `-wal` e `-shm` ao lado do banco (ignorados pelo `.gitignore`). O WAL exige que os dois processos estejam no mesmo computador e o arquivo num disco local, não numa pasta de rede.

**Logs.** Cada etapa de um relatório registra uma linha com o `reportId`: publicação (confirmada, não confirmada ou com falha), mensagem recebida (e se é reentrega), início (ou retomada), conclusão, duplicata, falha definitiva, nova tentativa e confirmação. As linhas não trazem e-mail, token, senha, connection string nem texto de exceção (só o nome do tipo).

**Estrutura e trade-off.** O worker referencia o projeto `ExpenseHub.Api` e é fino: só o host e o adaptador do RabbitMQ. Toda a regra (mensagem, processador, decisão de confirmação, calculadora e acesso ao banco) fica na Api e é testada sem broker. O custo é que a Api é um projeto web, então o worker herda o framework `Microsoft.AspNetCore.App` (a imagem do worker, na CP3-I05, usa a base `aspnet`). O worker não registra seed do Admin, controllers nem autenticação. Nada na Api precisou de `InternalsVisibleTo`.

## Como rodar

Pré-requisito: SDK do .NET 10. Não é preciso instalar servidor de banco.

Rode os comandos a partir da raiz do repositório:

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet tool restore
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
dotnet user-secrets set "Seed:Admin:Password" "<senha do Admin>" --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

- `dotnet tool restore` instala o `dotnet-ef` na versão fixada em `dotnet-tools.json`.
- `dotnet ef database update` cria o arquivo do banco e aplica as migrations. Rode a partir da raiz do repositório, porque o caminho de `--project` é relativo à pasta atual. O arquivo do banco cai sempre em `sources/ExpenseHub.Api/expensehub.db`.
- `dotnet user-secrets set` guarda a senha inicial do Admin fora do repositório. Troque `<senha do Admin>` por uma senha que atenda à política padrão do Identity: pelo menos 6 caracteres, com maiúscula, minúscula, número e símbolo. Sem essa senha a aplicação não inicia (veja [Conta Admin inicial](#conta-admin-inicial)).
- A aplicação escuta em `http://localhost:5245`. Para conferir que ela iniciou:

```shell
curl http://localhost:5245/health
```

Resposta esperada: `{"status":"ok"}`.

### Conta Admin inicial

Na inicialização a aplicação cria as cinco roles (`Admin`, `Employee`, `Approver`, `Finance` e `Auditor`) e, se ainda não existir nenhum usuário `Admin`, uma única conta Admin. Nenhum outro usuário é criado pelo seed.

| Chave | De onde vem |
|---|---|
| `Seed:Admin:Email` | `appsettings.json` (`admin@expensehub.local`); pode ser trocada por configuração. E-mail não é segredo |
| `Seed:Admin:Password` | Somente de user-secrets ou da variável de ambiente `Seed__Admin__Password`. Nunca de arquivo versionado |

Alternativa ao user-secrets, por variável de ambiente:

```powershell
$env:Seed__Admin__Password = "<senha do Admin>"
```

```shell
export Seed__Admin__Password="<senha do Admin>"
```

- O seed é idempotente: reiniciar a aplicação não duplica roles nem o Admin.
- Se a senha (ou o e-mail) não estiver configurada, a aplicação **não inicia**, com a mensagem `'Seed:Admin:Password' is not configured...`, e nada é gravado. A senha é exigida em toda inicialização, mesmo depois de o Admin existir.
- Se a senha não atender à política do Identity, a aplicação não inicia e informa só os códigos das regras violadas (por exemplo `PasswordTooShort`), nunca a senha. As roles ficam criadas e o Admin não é criado; uma nova execução com uma senha válida conclui o que faltou.
- O seed exige o banco migrado. Sem a migration, o erro é `no such table`.

### Usando o login

O login devolve um token bearer, que vai no cabeçalho `Authorization` das rotas protegidas. No PowerShell 7 ou no bash:

```shell
curl -X POST http://localhost:5245/login -H "Content-Type: application/json" -d '{"email":"admin@expensehub.local","password":"<senha do Admin>"}'
curl http://localhost:5245/api/admin/users -H "Authorization: Bearer <accessToken>"
```

No Windows PowerShell 5.1 o `curl.exe` estraga as aspas do JSON; use `Invoke-RestMethod` com `-ContentType 'application/json'` e o corpo montado com `ConvertTo-Json`.

### Cadastro e roles

Um usuário novo se cadastra sozinho, **sem roles**, e só um Admin concede roles:

```shell
curl -X POST http://localhost:5245/register -H "Content-Type: application/json" -d '{"email":"maria@exemplo.com","password":"<senha>"}'
curl http://localhost:5245/api/admin/users -H "Authorization: Bearer <accessToken do Admin>"
curl -X PUT http://localhost:5245/api/admin/users/<id>/roles -H "Authorization: Bearer <accessToken do Admin>" -H "Content-Type: application/json" -d '{"roles":["Employee"]}'
```

- O `PUT` **substitui** as roles do usuário: as listadas são atribuídas e as omitidas são removidas. `{"roles":[]}` remove todas.
- As roles aceitas são `Admin`, `Employee`, `Approver`, `Finance` e `Auditor`. Qualquer outro nome é `400`, e nenhuma role nova é criada.
- **Depois de alterar as roles de um usuário, ele precisa fazer login de novo.** O token que ele já tinha deixa de valer (`401`), e só o token novo traz as roles atualizadas. Isso vale também para o Admin que altera as próprias roles.
- O Admin não pode remover a própria role Admin (`403`).

## Banco de dados

**Provider:** SQLite, escolhido por ser um arquivo local que dispensa servidor.

| Pacote | Versão | Onde |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.12 | `ExpenseHub.Api` |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.12 | `ExpenseHub.Api` (somente tempo de design, `PrivateAssets=all`) |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.12 | `ExpenseHub.Api` (tabelas do Identity, I02) |
| `DocumentFormat.OpenXml` | 3.5.1 (MIT) | `ExpenseHub.Api` (Excel dos relatórios, CP3-I01) |
| `RabbitMQ.Client` | 7.2.2 (`Apache-2.0 OR MPL-2.0`; traz `System.IO.Pipelines` e `System.Threading.RateLimiting`, MIT) | `ExpenseHub.Api` (publicador); o `ExpenseHub.Worker` o recebe pela referência à Api (CP3-I03) |
| `dotnet-ef` | 10.0.12 | ferramenta local em `dotnet-tools.json`, na raiz do repositório |

**Configuração.** A connection string fica em `sources/ExpenseHub.Api/appsettings.json`, chave `ConnectionStrings:ExpenseHub`, com o valor `Data Source=expensehub.db`. É só o caminho de um arquivo, sem usuário nem senha. Um caminho relativo é ancorado na pasta do projeto da API, então o arquivo cai sempre em `sources/ExpenseHub.Api/expensehub.db`, seja com `dotnet run` ou com `dotnet ef`. Rode os comandos `dotnet ef` a partir da raiz do repositório, porque o caminho de `--project` é relativo à pasta atual.

Para usar outro arquivo, sobrescreva com a variável de ambiente `ConnectionStrings__ExpenseHub`. O valor precisa estar no formato `Data Source=caminho`; sem o prefixo `Data Source=` o EF falha ao ler a string. A variável vale para `dotnet run` e para `dotnet ef`.

```powershell
$env:ConnectionStrings__ExpenseHub = "Data Source=C:\temp\expensehub.db"
```

```shell
export ConnectionStrings__ExpenseHub="Data Source=/tmp/expensehub.db"
```

**Criação e atualização.** O banco não é criado na inicialização da aplicação: só `dotnet ef database update` o cria ou atualiza. Para recriar do zero, apague o arquivo `.db` e rode o comando de novo. Para gerar uma nova migration:

```shell
dotnet ef migrations add NomeDaMigration --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api --output-dir Persistence/Migrations
```

O arquivo `.db` (e os auxiliares `-shm` e `-wal`) é ignorado pelo Git, e nenhuma credencial é versionada.

**O que foi medido** (EF Core 10.0.12 com SQLite, em um experimento descartável fora do repositório):

- `DateTimeOffset` sem conversor não serve para consulta: `OrderBy` e `Max` lançam `NotSupportedException`, e `Where` com `>=` lança `InvalidOperationException` porque a expressão não é traduzida. Por isso os instantes são gravados como ticks UTC em `INTEGER`.
- `decimal` sem conversor funciona no EF 10: `OrderBy` (com `COLLATE EF_DECIMAL`), `Where`, `Sum`, `Max` e `Average` retornaram valores corretos.
- `DateOnly` é gravado como texto `yyyy-MM-dd` e ordena corretamente.
- O script gerado por `dotnet ef migrations script` não é idempotente no SQLite: a opção `--idempotent` não é suportada pelo provider.
- Com a migration aplicada, o `CHECK` de faixa do valor, o índice único de pagamento e as chaves estrangeiras com `RESTRICT` rejeitaram os dados inválidos.

## Arquitetura

Estrutura de pastas:

```text
dotnet-tools.json                 ferramentas locais (dotnet-ef)
.github/workflows/                workflow code-quality
docs/                             especificação da disciplina
scripts/                          pipeline de qualidade local
tests/                            testes do próprio script de qualidade (vêm do template)
sources/
  ExpenseHub.slnx
  ExpenseHub.Api/
    Program.cs                    inicialização, pipeline e GET /health
    appsettings.json
    Controllers/                  AuthController (POST /register e /login), AdminUsersController, ExpensesController, ReportsController
    Domain/
      Entities/                   Expense, ExpenseCategory, ExpenseHistory, PaymentRecord, ReportRequest
      Enums/                      ExpenseStatus, ExpenseHistoryAction, ReportScope, ReportStatus
    Expenses/                     ExpenseService (criar, editar, enviar, aprovar, reprovar, pagar e consultar, inclusive o histórico),
                                  ExpenseRules (contrato de validação),
                                  ExpenseVisibility e ExpenseScope (o que cada perfil lê),
                                  ExpenseAccess (a matriz de acesso: role, dono e estado numa regra só),
                                  BrazilTime, IExpenseRepository, MoneyAmountAttribute, RejectionReasonAttribute e os DTOs
                                  (ExpenseRequest, RejectExpenseRequest, ExpenseResponse, ExpenseHistoryResponse e PaymentResponse)
    Reports/                      ReportService (pedir, listar, consultar e baixar o Excel de um relatório do próprio usuário),
                                  ReportAccess (a matriz de role por escopo), ReportQuery (a consulta nasce do pedido gravado),
                                  ReportCalculator (a regra de quais despesas entram, como consulta, e a soma em centavos),
                                  ReportRules e CreateReportRequest (validação), ReportResultSerializer (o resultado salvo),
                                  ReportExcelWriter (a planilha), IReportRepository, IReportPublisher (o broker fica atrás
                                  dela; RabbitMqReportPublisher publica com confirmação e UnavailableReportPublisher nunca
                                  confirma), ReportMessage (a mensagem, só com o id), ReportQueueTopology e RabbitMqOptions
                                  (fila e configuração), ReportMessageProcessor e ReportDelivery (o tratamento da mensagem e a
                                  decisão de confirmar), IReportProcessingStore (as transições condicionais do estado) e os DTOs
                                  (ReportResponse e derivados)
    Identity/                     registro do Identity e do bearer, DTOs, AppRoles, o seed, UserAccountService
                                  (regras de cadastro e de roles) e a validação do SecurityStamp
    Persistence/
      ExpenseHubDbContext.cs
      ExpenseHubDbContextFactory.cs             usada só pelo dotnet ef
      PersistenceServiceCollectionExtensions.cs registro no DI e connection string
      SqlitePragmas.cs e SqliteConnectionInterceptor.cs  espera por bloqueio e WAL, iguais no web e no worker
      Configurations/             mapeamento de cada entidade
      Converters/                 conversões de valor e de instante
      Migrations/                 InitialCreate, AddIdentity, ExpenseStatusConcurrencyToken, AddReportRequests e snapshot (gerados)
      Repositories/               ExpenseRepository, ReportRepository e ReportProcessingStore (EF Core), SqliteConstraintErrors e
                                  SqliteTransientErrors (classificam os erros do SQLite)
  ExpenseHub.Worker/
    Program.cs                    host genérico: banco, configuração do RabbitMQ e o processador (sem seed, controllers nem autenticação)
    ReportConsumerService.cs      adaptador do RabbitMQ: recebe, chama o processador e executa a decisão de confirmação
    appsettings.json              só o endereço do broker e o nome da fila, sem credenciais
  ExpenseHub.UnitTests/
    Expenses/                     testes das regras de despesa, do serviço e dos DTOs, com fake do repositório,
                                  e do controller de despesas (contrato HTTP) e dos papéis dos endpoints de despesas e de administração de usuários, com fake do serviço
    Identity/                     testes do seed, do cadastro, das roles, das validações e do middleware do SecurityStamp,
                                  com fakes das stores e do gerenciador de usuários
    Persistence/                  testes das conversões, da classificação dos erros do SQLite e dos comandos de WAL e espera por bloqueio
    Reports/                      testes da mensagem, da fila, das opções, do processador (duplicata, retomada, falhas e logs) e da decisão de confirmação,
                                  da calculadora, das regras, do corpo da requisição, do resultado salvo, do serviço, do Excel,
                                  do envelope, da matriz de role por escopo, do isolamento por role e dos atributos das rotas, e do
                                  controller de relatórios, com fakes do repositório, do publicador e do serviço
```

As entidades são classes simples, sem métodos nem regras de negócio. A regra do seed fica em `IdentitySeeder`, que depende da interface `IIdentitySeedStore`; a implementação com `UserManager` e `RoleManager` é `IdentitySeedStore`. As regras de cadastro e de roles ficam em `UserAccountService`, que depende de `IUserAccountStore`; a implementação com EF Core e Identity é `UserAccountStore`. As regras de despesa ficam em `ExpenseService` e `ExpenseRules`, que dependem de `IExpenseRepository` e de um `TimeProvider`; a implementação com EF Core é `ExpenseRepository`. As regras de relatório seguem o mesmo molde: `ReportService` depende de `IReportRepository`, de `IReportPublisher` e de um `TimeProvider`, e `ReportCalculator` é uma classe estática pura, sem EF Core. O serviço nunca recebe `DbContext`. O código de erro do SQLite só aparece em `SqliteConstraintErrors`, uma função pura sobre números, testada sem banco, que o repositório usa para converter a violação do índice único em conflito.

## Testes

```shell
dotnet test ./sources/ExpenseHub.slnx
```

- Somente testes unitários (MSTest 4), sem banco, rede ou serviço externo. Na I09, 611 testes: 13 da I01 (9 de `MoneyConversion` e 4 de `UtcTicks`), 25 da I02 (14 de `IdentitySeeder` e 11 de `LoginRequest`), 48 da I03 (25 de `UserAccountService`, 11 de `RegisterRequest`, 4 de `UpdateUserRolesRequest` e 8 de `SecurityStampCheck`), 110 da I04 (42 de `ExpenseRules`, 4 de `BrazilTime`, 26 de `ExpenseService` na criação, 20 na edição e 18 de `ExpenseRequest`), 54 da I05 (19 de `ExpenseVisibility`, 18 de envio e 16 de consulta, mais 1 de conflito na edição, que ficou junto dos testes de edição), 100 da I06 (83 de `ExpenseAccess`, a matriz completa, 8 novos de criação e 9 novos de edição, sobre a exigência da role no serviço e a regra de dono do `PUT`), 136 da I07 (37 da regra de acesso e dos serviços de edição e envio, sendo 28 casos novos na matriz de `ExpenseAccess` e 9 nos serviços; 26 da regra e do DTO da justificativa, sendo 14 de `ExpenseRules` e 12 de `RejectExpenseRequest`; e 73 dos serviços de decisão, sendo 30 de aprovação e 43 de reprovação), 77 da I08 (6 da classificação dos erros do SQLite em `SqliteConstraintErrors`, 32 do pagamento, 32 do histórico e 7 das respostas, sendo 4 de `ExpenseHistoryResponse` e 3 de `PaymentResponse`) e 48 da I09 (6 de lacunas das regras, do serviço e da `ExpenseResponse`, 22 do contrato HTTP do controller de despesas, 13 dos papéis dos endpoints de despesas e de administração de usuários (11 casos) e do acesso anônimo (2 casos), e 7 do middleware do `SecurityStamp`), contando cada caso de `DataRow`. A conta fecha: os 563 acumulados até a I08 mais 48 da I09 dão 611.
- Os testes da I01 chamam funções estáticas puras. Os do seed, de `UserAccountService`, de `ExpenseService`, do controller de despesas e do middleware do `SecurityStamp` usam fakes escritos à mão (das stores, do repositório, do serviço, do gerenciador de usuários e da fábrica de `ProblemDetails`) e um relógio fixo escrito à mão (`TimeProvider`). Nenhum usa tipos do EF Core, e nenhum usa Moq.
- O pipeline real de `401` e `403`, o login com o `SignInManager`, a leitura do token pelo host e a gravação no banco dependem do host e do banco, então foram validados à mão (seções da I02 à I08 do [CHECKPOINT-2.md](CHECKPOINT-2.md)). O controller de despesas e o middleware do `SecurityStamp` têm testes unitários com fakes; o que a suíte deixa de fora está em [O que a suíte não cobre](CHECKPOINT-2.md#o-que-a-suíte-não-cobre).
- Não usamos EF Core InMemory nem SQLite em memória nos testes unitários.
- CP3-I01: a suíte passa de 611 para 788 testes aprovados. Os 177 novos são de relatórios: calculadora 24, resultado salvo 20, regras 25, corpo da requisição 22, solicitação 20, consulta 11, Excel pelo serviço 11, escritor do Excel 9, envelope 11, controller 16, autenticação das rotas 3 e publicador 5. A tradução da consulta para SQL, as restrições da tabela e o download real foram conferidos à mão contra o SQLite (veja a seção da CP3-I01 no [CHECKPOINT-3.md](CHECKPOINT-3.md#detalhe-da-cp3-i01-contrato-e-resultado-do-relatório)).
- CP3-I02: a suíte passa de 788 para 936 testes aprovados. Os 148 novos são: matriz de role por escopo 59 (a tabela escrita à mão das 32 combinações das cinco roles, mais a conferência de que nenhuma combinação foi esquecida), ordem e autorização no serviço 35, isolamento por role 36, consulta montada do pedido gravado 10, atributos das rotas 6 e controller 2. A ordem das respostas (`401`, `403` por role, `400`, `403` por escopo) e o isolamento entre contas de roles diferentes foram conferidos à mão contra a aplicação real e um SQLite temporário (veja a seção da CP3-I02 no [CHECKPOINT-3.md](CHECKPOINT-3.md#detalhe-da-cp3-i02-autorização-e-isolamento)).
- CP3-I03: a suíte passa de 936 para 1086 testes aprovados. Os 150 novos, sem banco, rede nem RabbitMQ, com fakes escritos à mão do repositório, do estado dos pedidos e do publicador: mensagem 26 (só o `reportId`; corpo não confiável), fila e mensagem persistente 8, opções do broker 17, registro do publicador 9, processador 33 (resultado igual ao da calculadora, duplicata, reinício, `Processing` retomado, corrida entre workers, falha definitiva, falha transitória em cada ponto), logs por `reportId` sem segredos 9, decisão de confirmação 15 (nenhuma falha de gravação chega a confirmação), erros transitórios do SQLite 25 e comandos de WAL e espera 8. Mutações feitas e desfeitas: confirmar mesmo sem ter salvo quebrou 10 testes, e remover a checagem de duplicata quebrou 5. A fila real, a confirmação de publicação, o worker, a duplicata depois de reiniciar, o `Processing` interrompido por um `kill`, a falha transitória e o broker parado foram conferidos à mão contra um RabbitMQ 4.4.0 em contêiner e um SQLite temporário, com 65 conferências aprovadas (veja a seção da CP3-I03 no [CHECKPOINT-3.md](CHECKPOINT-3.md#detalhe-da-cp3-i03-rabbitmq-worker-e-idempotência)).

Para rodar um teste ou uma classe:

```shell
dotnet test ./sources/ExpenseHub.slnx --filter "FullyQualifiedName~MoneyConversionTests"
```

## Qualidade

- O workflow `code-quality` (`.github/workflows/build.yml`) roda `scripts/Invoke-CodeQuality.ps1`. As regras de pontuação estão em [docs/code-quality-rules.md](docs/code-quality-rules.md).
- Rodar localmente (`-SkipGitleaks` se o Gitleaks não estiver instalado); o relatório vai para `artifacts/code-quality/`, pasta ignorada pelo Git:

```shell
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

- I01: `dotnet build` com 0 erros e 0 avisos, `dotnet test` com 13 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes). Essa execução usou `-SkipGitleaks`, então a varredura de segredos do Gitleaks só roda no CI.
- Na PR #1, o workflow code-quality no GitHub também deu 100/100 (com Gitleaks 8.30.1), sem bloqueantes e sem achados.
- I02: `dotnet build --no-incremental` com 0 erros e 0 avisos e `dotnet test` com 38 testes aprovados. Score oficial na PR #2 (execução #14): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados. Uma execução anterior na branch (#12) deu 9/100 pela regra FIAP1002, porque os testes tinham valores literais atribuídos a campos sensíveis; os valores foram removidos antes da PR e as execuções seguintes passaram.
- I03: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 86 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. Score oficial na PR #3 (execução #18): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- I04: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 196 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. Score oficial na PR #4 (execução #22): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- I05: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 250 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. Depois dos commits da I04, o Smart App Control do Windows chegou a bloquear a DLL de testes nesta máquina e o script mostrou 96 por um falso "teste falhou"; o score oficial na PR #5 (execução #26) é **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- I06: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 350 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. Score oficial na PR #6 (execução #29): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- I07: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 486 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. A execução do workflow da branch passou com 100/100 e Gitleaks em cada push. Score oficial na PR #7 (execução #37): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- I08: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 563 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. A execução do workflow da branch passou com 100/100 e Gitleaks em cada push. Score oficial na PR #8 (execução #46): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- I09: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 611 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. A execução do workflow da branch passou com 100/100 e Gitleaks em cada push. Score oficial na PR #9 (execução #55): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- I10: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 611 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. A I10 não altera código, pacotes, workflow, script nem analisadores. A execução do workflow da branch passou com 100/100 e Gitleaks em cada push. Score oficial na PR #10 (execução #60): **100/100**, com Gitleaks 8.30.1, sem bloqueantes e sem achados.
- CP3-I01: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 788 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. O único achado é o informativo `FIAP0002` (Gitleaks ignorado). Um primeiro passe do script deu 99 por `IDE1006` (campos `private static readonly` sem o prefixo `_`), corrigido antes da PR. Score oficial do workflow `code-quality` depois do merge (PR #12): 100/100 na execução #69.
- CP3-I02: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 936 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`; o único achado é o informativo `FIAP0002`. Score oficial do workflow `code-quality` depois do merge (PR #13): 100/100 na execução #72.
- CP3-I03: `dotnet build --no-incremental` com 0 erros e 0 avisos (três projetos: Api, Worker e testes), `dotnet test` com 1086 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`; o único achado é o informativo `FIAP0002`. Nenhuma credencial do broker está em arquivo versionado. O score oficial do workflow entra aqui quando a PR existir.
- Nenhum aviso é suprimido no código da equipe (sem `#pragma warning disable`, `[SuppressMessage]` nem `NoWarn`). Os 4 arquivos de migration gerados pelo `dotnet ef` (os três `.Designer.cs` e o `ExpenseHubDbContextModelSnapshot.cs`) trazem o `#pragma warning disable 612, 618` do gerador e são tratados como código gerado pelo `.editorconfig`.

## Solução de problemas

- `dotnet ef` não encontrado: rode `dotnet tool restore` na raiz do repositório.
- `no such table`: o banco ainda não foi criado ou está desatualizado; rode `dotnet ef database update`.
- A aplicação não inicia e o log mostra `SQLite Error 1: 'no such table: AspNetRoles'`: o seed roda na inicialização e exige o banco já migrado. Rode `dotnet ef database update` antes de `dotnet run`. A tentativa sem migrations deixa um arquivo `.db` vazio (0 bytes), e ele pode ficar onde está: o `dotnet ef database update` o aproveita, aplica todas as migrations e a aplicação inicia normalmente depois (testado com um arquivo vazio de 0 bytes).
- `Format of the initialization string does not conform to specification`: o valor de `ConnectionStrings__ExpenseHub` está sem o prefixo `Data Source=`.
- O arquivo `.db` não está onde se esperava: confira se a variável `ConnectionStrings__ExpenseHub` está definida no terminal; sem ela o arquivo fica em `sources/ExpenseHub.Api/expensehub.db`.
- `Generating idempotent scripts for migrations is not currently supported for SQLite`: use `dotnet ef migrations script` sem `--idempotent`.
- O worker não inicia e o log diz `The RabbitMq settings are missing or invalid: UserName, Password`: defina `RabbitMq__UserName` e `RabbitMq__Password` (variáveis de ambiente ou user-secrets); a lista traz só os nomes dos campos.
- `Connection string 'ExpenseHub' is not configured` no worker: o worker não tem connection string própria. Defina `ConnectionStrings__ExpenseHub` com o caminho absoluto do mesmo arquivo que o web usa.
- Os relatórios ficam `Pending` e o worker registra `was not found, so its message was discarded`: web e worker estão apontando para arquivos de banco diferentes (caminho relativo). Use o mesmo caminho absoluto nos dois.
- Um relatório ficou `Pending` depois de um `503`: a mensagem pode nunca ter sido publicada (janela sem Transactional Outbox) e reiniciar o worker não a recria. Consulte o `id` retornado e, se continuar `Pending`, peça outro relatório.
- O `POST /api/reports` responde `503` com o `id` e o log do web diz `could not be published`: o RabbitMQ não está acessível ou as credenciais estão ausentes; suba o broker e confira `RabbitMq__*` no web. Os relatórios `Pending` com mensagem na fila são concluídos assim que o worker estiver no ar.
- `database is locked` ou o log do worker com `could not be saved now`: outro processo está com o banco bloqueado por mais de 5 s. A mensagem não é confirmada e volta sozinha; nada vira `Failed` por isso.
- `'Seed:Admin:Password' is not configured`: defina a senha com `dotnet user-secrets set "Seed:Admin:Password" "<senha>" --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj` ou com a variável de ambiente `Seed__Admin__Password`.
- `Could not create the initial Admin user: PasswordTooShort, ...`: a senha não atende à política do Identity (pelo menos 6 caracteres, com maiúscula, minúscula, número e símbolo).
- Login do Admin devolve `401` mesmo com a senha certa: o Admin já existia de uma execução anterior com outra senha. O seed não altera um Admin existente. Apague o arquivo `.db` e rode `dotnet ef database update` de novo.
- Os testes com `curl.exe` no Windows PowerShell 5.1 devolvem `400` com `is an invalid start of a property name`: o PowerShell 5.1 estraga as aspas do JSON. Use `Invoke-RestMethod` ou o PowerShell 7.
- Um token que funcionava passou a devolver `401`: as roles ou o `SecurityStamp` do usuário mudaram depois que o token foi emitido. Faça login de novo.
- `POST /register` devolve `400` com `PasswordTooShort` ou outros códigos `Password...`: a senha não atende à política do Identity (pelo menos 6 caracteres, com maiúscula, minúscula, número e símbolo).
- `POST /api/expenses` ou `PUT /api/expenses/{id}` devolvem `403` para um usuário que acabou de se cadastrar: o cadastro não dá nenhuma role. Um Admin precisa conceder `Employee` (`PUT /api/admin/users/{id}/roles`), e o usuário precisa fazer login de novo para receber o token com a role.
- `400` com `The amount cannot have more than 2 decimal places`: o valor tem mais de duas casas decimais. Elas não são arredondadas; envie, por exemplo, `12.35`.
- `400` com `The expense date cannot be in the future`: a data é posterior a hoje em Brasília.
- `GET /api/expenses` ou `GET /api/expenses/{id}` devolvem `403`: o usuário não tem nenhuma role que leia despesas (`Employee`, `Approver`, `Finance` ou `Auditor`). O Admin sozinho também recebe `403`; um Admin precisa conceder a si mesmo, por exemplo, `Auditor`, e fazer login de novo.
- `GET /api/expenses` vem vazia ou sem uma despesa que você esperava: a listagem só mostra o que o perfil lê (por exemplo, um Approver só vê as `Submitted`, e um Employee só vê as próprias). Para ver uma despesa em outro estado, use um usuário com a role certa ou `Auditor`.
- `GET /api/expenses/{id}` devolve `404` para uma despesa que existe: ela está fora do escopo do seu perfil. A resposta é igual à de uma despesa que não existe, de propósito.
- `POST /api/expenses/{id}/submit` devolve `409`: a despesa já foi enviada (ou não está em `Draft`). Enviar de novo nunca grava histórico novo.
- `POST /api/expenses/{id}/submit` devolve `403` para um Employee que não é o dono, tenha ele outra role ou não: a despesa existe e é de outra pessoa, e só o dono envia. O `404` é só para despesa inexistente (alterado na I07: antes, um Employee sem outra role recebia `404`).
- `PUT /api/expenses/{id}` devolve `403` (e não `404`) para um Employee que não é o dono, com ou sem outra role, como `Auditor` ou `Approver`: a despesa existe e é de outra pessoa, e só o dono edita. O `404` é só para despesa inexistente (alterado na I07).
- `POST /api/expenses/{id}/approve` ou `.../reject` devolve `409`: a despesa existe, mas não está `Submitted` (já foi aprovada, reprovada ou paga, ou ainda é `Draft`). Repetir a decisão nunca grava histórico novo. O `404` é só para despesa inexistente (alterado na I07: antes, o Approver recebia `404` para as despesas fora do escopo de leitura).
- `POST /api/expenses/{id}/approve` ou `.../reject` devolve `403`: o usuário não tem a role `Approver`, ou a despesa é dele mesmo (ninguém decide a própria despesa, mesmo acumulando `Employee` com `Approver`).
- `POST /api/expenses/{id}/reject` devolve `400`: a justificativa (`reason`) está ausente, vazia, só com espaços ou fora de 10 a 500 caracteres depois de aparada, ou o corpo não é um JSON válido.
- `POST /api/expenses/{id}/reject` sem corpo e sem o cabeçalho `Content-Type: application/json` devolve `415`, e não `400`: é o comportamento padrão do ASP.NET para endpoints com corpo; com o cabeçalho e o corpo vazio, a resposta é `400`.
- `POST /api/expenses/{id}/pay` devolve `409`: a despesa existe, mas não está `Approved` (ainda é `Draft`, `Submitted` ou `Rejected`, ou já está `Paid`), ou outro pagamento foi feito antes ou ao mesmo tempo. Repetir o pagamento nunca grava um segundo `PaymentRecord` nem uma linha de histórico nova.
- `POST /api/expenses/{id}/pay` devolve `403`: o usuário não tem a role `Finance`, ou a despesa é dele mesmo (ninguém paga a própria despesa, mesmo acumulando `Employee` com `Finance`). O `404` é só para despesa inexistente.
- `POST /api/expenses/{id}/pay` devolve `500`: a gravação falhou por um motivo que não é conflito. Nada foi gravado (a despesa continua `Approved`, sem pagamento e sem linha `Paid`), e o mesmo pedido pode ser repetido.
- `GET /api/expenses/{id}/history` devolve `404` para uma despesa que existe: ela está fora do escopo do seu perfil, e o histórico segue a visibilidade da despesa. Um Approver não lê mais o histórico de uma despesa depois de decidi-la, e um Finance só lê o de `Approved` e `Paid`. Use um usuário com a role certa ou `Auditor`.
- `GET /api/expenses/{id}/history` devolve `403`: o usuário não tem nenhuma role que leia despesas. O Admin sozinho também recebe `403`.

## Uso de IA

O projeto usa o Claude Code como assistente de desenvolvimento, conforme [docs/USO-DE-IA.md](docs/USO-DE-IA.md). Todos os diffs são revisados por uma pessoa da equipe, e os commits são feitos pelos autores. Nenhum segredo é enviado em prompts ou versionado.
