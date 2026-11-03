# PROGRESSO do CP3

Registro temporário de contexto e andamento do Checkpoint 3 (CP3). Esta pasta (`andamento/`) será removida antes do SHA final. Leia este arquivo por inteiro antes de começar qualquer issue. Ao terminar uma issue, atualize a linha "Estado atual", a tabela de status e a seção da issue, e confira se o `CHECKPOINT-3.md` e o `README.md` também foram atualizados (ver "Organização da documentação" no `CLAUDE.md`).

## Estado atual

> CP3-00 (preparação) concluída e mergeada na `main` como PR #11 da equipe (merge commit `6d7f811`): pasta `andamento/`, `CLAUDE.md` do CP3, baseline (build 0 avisos e 0 erros, 611 testes, SDK 10.0.403, Docker 29.9.2) e documentação reorganizada (`CHECKPOINT-2.md`, `CHECKPOINT-3.md` e `README.md` como página de entrada). CP3-I01 concluída e mergeada na `main` como PR #12 da equipe (merge commit `48810e3`, SHA completo `48810e3f69def12fdc391d5d6e440af75f481836`), com score oficial do workflow `code-quality` de 100/100 na execução #69 (informado pelo Renan): rotas e DTOs de relatório, entidade e migration `AddReportRequests`, calculadora pura, resultado salvo, Excel com `DocumentFormat.OpenXml` 3.5.1 (MIT), publicador atrás de `IReportPublisher` (o POST responde 503 até a CP3-I03) e esqueleto do `.http`; 788 testes. CP3-I02 (autorização e isolamento) concluída e mergeada na `main` como PR #13 da equipe (merge commit `6e47693`, SHA completo `6e476939514aa88710b5158ea3f92a9198d632fe`), com score oficial do workflow `code-quality` de 100/100 na execução #72 (informado pelo Renan): matriz de role por escopo (`ReportAccess`), ordem das respostas (401, 403 por role, 400, 403 por escopo), consulta do worker montada do pedido gravado (`ReportQuery.From`) e testes de isolamento por role; 936 testes. CP3-I03 (RabbitMQ, worker e idempotência) concluída e mergeada na `main` como PR #14 da equipe (merge commit `78bca7d`, SHA completo `78bca7d00a785bb37bcedc5b40bcd14adffa2535`), com score oficial do workflow `code-quality` de 100/100 na execução #75 (informado pelo Renan): `RabbitMQ.Client` 7.2.2, publicador com confirmação (fila `expensehub.reports` durável, mensagem persistente só com o `reportId`), projeto `ExpenseHub.Worker` fino, processador testável com idempotência no banco, decisão de confirmação em `ReportDelivery`, e SQLite compartilhado com `busy_timeout` e WAL; 1086 testes. CP3-I05 (imagens, workflow de publicação e Compose) concluída na branch `cp3-i05`, aguardando a PR e o merge: dois Dockerfiles (mesmas bases, usuário não root, commit na variável `Build__Commit`, na label e no log), workflow novo `.github/workflows/publish-images.yml` (testes, depois imagens, tag igual ao SHA completo, sem `latest`), `compose.yaml` com `${IMAGE_TAG}`, RabbitMQ 4.4.0 fixado por tag e digest, volumes nomeados (broker, banco compartilhado e chaves), migrations por flag desligada por padrão e `.env.example` sem segredos; 1149 testes. O texto da PR está em `andamento/PR-CP3-I05.md`. Nenhuma outra issue começou. Próximo passo: depois do merge da CP3-I05 (que dispara a primeira publicação das imagens na `main`), tornar os dois pacotes públicos no GHCR (manual), conferir `docker compose pull` com o SHA completo e seguir para a CP3-I04.

## Links

- Repositório da equipe (código e PRs): https://github.com/renan-utida/expensehub-api
- Repositório do professor (issues centrais, não fechar): https://github.com/Racass/checkpoint-csharpracass-expensehub
- Lista das issues do CP3: https://github.com/Racass/checkpoint-csharpracass-expensehub/issues?q=is%3Aissue+CP3
- Issues originais: CP3-I01 = #11, CP3-I02 = #12, CP3-I03 = #13, CP3-I04 = #14, CP3-I05 = #15, CP3-I06 = #16, CP3-I07 = #17, CP3-EXTRA-LINKEDIN = #18.
- Fontes locais da especificação: `andamento/requisitos/apostila-cp3.html` e `andamento/requisitos/slides-cp3.html` (autoridade em caso de dúvida) e `andamento/requisitos/issues-cp3.md` (texto das issues #11 a #18 como o professor publicou).

## Prazo e regras do jogo

- **Prazo:** quarta-feira, 04/11/2026, às 23:59, no Teams. A apostila (2 vezes), os slides (1 vez) e as issues centrais (8 vezes) dizem "04/10"; é erro de digitação, vale a data do Teams (04/11).
- **Autoria:** o CP3 é feito pelo Renan sozinho. A nota-base é coletiva (mesmo grupo do CP2). O ponto extra do LinkedIn é individual.
- **Ponto de partida:** o CP2 está entregue e mergeado na `main` (merge commit `2e74619`, PR #10). A nota do CP2 não é recalculada. Defeitos antigos sem relação com o novo trabalho não geram desconto; correções necessárias para integrar a funcionalidade são permitidas.
- **Base de comparação:** 611 testes unitários no fim do CP2, build com 0 avisos, score do pipeline 100/100. A issue de qualidade compara o commit do CP2 com a entrega final.
- **Não mudar o comportamento existente**, especialmente autenticação, autorização e pagamento. Não há frontend, assistente, nova autenticação, pagamento assíncrono, nuvem, Kubernetes nem migração geral de banco.
- **Issues centrais:** não usar `Closes`, `Fixes` nem `Resolves` ao citá-las. Elas são a especificação comum e permanecem abertas.
- **Commit de entrega:** o professor corrige o SHA exato do último commit da `main`. Depois dele, nada muda.

## Ordem recomendada e dependências

| Ordem | Issue | Branch | Depende de | Observação |
| :- | :- | :- | :- | :- |
| 0 | CP3-00 Preparação | `cp3-00-preparacao` | nada | Pasta `andamento/`, `CLAUDE.md` do CP3 e reorganização do README. Sem código de produção. |
| 1 | CP3-I01 Contrato e resultado | `cp3-i01` | nada | Rotas, validação, persistência, cálculo isolado, Excel. Publicador por abstração. |
| 2 | CP3-I02 Autorização | `cp3-i02` | I01 | Escopos Mine e Global, isolamento por solicitante, 404 para alheios. |
| 3 | CP3-I03 RabbitMQ e worker | `cp3-i03` | I01 (respeitar I02) | Publicação com confirmação, worker, idempotência persistente. |
| 4 | CP3-I05 Imagens e Compose | `cp3-i05` | web da I01 e worker da I03 | A apostila a chama de independente, mas só há o que publicar depois da I03. Vem antes da I04 porque CI, registry e pacote público são o ponto que mais dá problema e a I06 precisa das imagens publicadas. |
| 5 | CP3-I04 Testes | `cp3-i04` | I02 e I03 | Escrever testes desde a I01. A I04 audita a suíte contra a tabela de cenários e fecha as lacunas. |
| 6 | CP3-I06 Experimentos | `cp3-i06` | I02, I03 e I05 | E01 a E05 com evidências reais, no ambiente Docker do Renan. |
| 7 | CP3-I07 Qualidade | `cp3-i07` | transversal | Comparar com o commit do CP2, limpar, remover `andamento/`. |

Cada issue de implementação tem uma branch e uma PR. A publicação no LinkedIn não tem branch nem PR.

## Decisões tomadas (após a leitura inicial do agente, 02/11)

Respostas do Renan às perguntas do agente. Valem até surgir um fato novo; qualquer mudança entra aqui com a data.

| Tema | Decisão |
| :- | :- |
| Escopo da CP3-00 | Fazer completa agora, mas por movimentação mecânica com shell (`sed -n 'A,Bp'`), provando que nada se perdeu com contagem de linhas e `diff`. Nada de reescrever o README à mão. |
| Estrutura de projetos | Opção A. O worker referencia `ExpenseHub.Api`. A lógica de relatório (calculadora, processador, tratador da mensagem) fica na Api e é coberta pelos testes existentes. Worker fino: host e adaptador do RabbitMQ. Ele não registra seed do Admin, controllers nem autenticação. O que o worker usar precisa ser `public` ou visível por `InternalsVisibleTo` (escolher no plano da I03). Registrar o trade-off no README. |
| Biblioteca de Excel | `DocumentFormat.OpenXml` (MIT). Conferir versão, licença e dependências transitivas no NuGet dentro do plano da I01 e pedir o OK do Renan antes de adicionar o pacote. |
| POST na I01 | Responde 503 até existir publicador real (I03). Os testes usam um fake do publicador. Nunca 202 sem confirmação. |
| Identificador | `Guid`, como `Expense.Id`, igual nas rotas, na mensagem e nas respostas. |
| Migrações no Compose | Decidir no plano da I05. Restrição: só duas imagens publicadas (web e worker), nenhuma terceira. Opções: flag de configuração desligada por padrão (ligada só no Compose) ou bundle do EF dentro da imagem web, como serviço de uma execução. Escolher a de menos peças móveis, que preserve o comportamento padrão do CP2 e não apague dados. |
| Versão e SHA | O workflow publica a cada push na `main`, sem filtro de caminho, com tag igual ao SHA completo. O commit final gera as suas próprias imagens: esperar o workflow desse commit, fazer `docker compose pull` com essa tag e conferir. O dossiê informa o SHA usado nos experimentos e mostra, com `git diff --stat`, que a diferença até o SHA final é só documentação. |
| Arquivo `.http` | Nasce na I01 e é completado na I06. Credenciais lidas de um arquivo de ambiente ignorado pelo git. Nenhum literal de senha ou token (FIAP1002 e apostila). |
| Professor | Nenhuma pergunta ao professor neste checkpoint. A apostila só manda consultá-lo se o banco for incompatível, e SQLite com arquivo persistente compartilhado é citado como compatível. |

Decisões que ficam para o plano da issue (o agente propõe, o Renan aprova):

- I03: política de falha transitória (sem laço quente e sem ack falso) e mecanismo concreto para retomar um `Processing`.
- I03 e I05: evidência da versão em execução para o E05 (por exemplo variável de ambiente com o SHA, label OCI e uma linha de log na partida).
- I06: contas do E04 (duas Employee e uma Auditor): criar por registro e atribuir o papel pelo Admin; senhas vindas de arquivo de ambiente ignorado ou digitadas no terminal, nunca versionadas.

## Calendário alvo

| Quando | Meta |
| :- | :- |
| Seg 02/11, noite | CP3-00 mergeada; CP3-I01 em plano ou execução. |
| Ter 03/11 | I01, I02 e I03 mergeadas; I05 em andamento. |
| Qua 04/11, manhã | I05 mergeada, imagens publicadas e públicas; I04. |
| Qua 04/11, tarde | I06 (experimentos) e I07; remoção de `andamento/`. |
| Qua 04/11, até 18h | `main` congelada, workflow do SHA final concluído, `docker compose pull` conferido, texto do Teams pronto. |
| Qua 04/11, 23:59 | Prazo. Margem de cerca de seis horas. |

## Status

| Item | Estado | Branch | PR | Pontos da rubrica |
| :- | :- | :- | :- | :- |
| CP3-00 Preparação | Concluída | `cp3-00-preparacao` | PR #11 (merge `6d7f811`) | n/d |
| CP3-I01 Contrato e resultado | Concluída | `cp3-i01` | PR #12 (merge `48810e3`) | 20 |
| CP3-I02 Autorização e isolamento | Concluída | `cp3-i02` | PR #13 (merge `6e47693`) | 15 |
| CP3-I03 RabbitMQ, worker e idempotência | Concluída | `cp3-i03` | PR #14 (merge `78bca7d`) | 20 |
| CP3-I04 Testes próprios | Pendente | `cp3-i04` | n/d | 15 |
| CP3-I05 Publicação e execução reproduzível | Concluída na branch (aguardando PR) | `cp3-i05` | n/d | 15 |
| CP3-I06 Experimentos e diagnóstico | Pendente | `cp3-i06` | n/d | 10 |
| CP3-I07 Qualidade objetiva | Pendente | `cp3-i07` | n/d | 5 |
| CP3-EXTRA-LINKEDIN | Pendente (manual, Renan) | n/d | n/d | +1 individual |

## Resumo do contrato (extraído da apostila; em caso de divergência vale o HTML)

### Objetivo

O usuário pede um resumo de reembolsos por período sem esperar o cálculo dentro da requisição. A aplicação web registra o pedido e publica uma mensagem no RabbitMQ. O worker C# consome, calcula e persiste. O usuário acompanha, consulta o resultado e baixa uma planilha Excel. Banco compartilhado entre web e worker é permitido. Sem interface nova: usar Swagger ou arquivo `.http`.

### Rotas

| Rota | Comportamento |
| :- | :- |
| `POST /api/reports` | Solicita um relatório com `fromDate`, `toDate` e `scope`. |
| `GET /api/reports` | Lista só as solicitações criadas pelo usuário autenticado (array de envelopes, sem paginação obrigatória). |
| `GET /api/reports/{id}` | Detalhe e andamento de uma solicitação própria. |
| `GET /api/reports/{id}/excel` | Baixa o resultado salvo de uma solicitação própria concluída, em `.xlsx`. |

### Corpo do POST

```json
{ "fromDate": "2026-09-01", "toDate": "2026-09-30", "scope": "Mine" }
```

- Datas no formato ano-mês-dia, obrigatórias e válidas; `fromDate <= toDate`.
- `scope` é `Mine` ou `Global`, respeitando a matriz de acesso. Não existe `ownerId` vindo do cliente.
- O filtro usa `ExpenseDate`, com os dois limites inclusivos. Nunca usar a data de criação no lugar.
- O servidor gera o identificador e obtém o solicitante da identidade autenticada. O tipo do identificador pode seguir a base do grupo, mas deve ser o mesmo nas rotas, mensagens e respostas.
- Um novo POST válido cria outra solicitação. Deduplicar requisições do cliente não é obrigatório.
- Aceitação normal: **202**, cabeçalho `Location` apontando para o detalhe e o envelope abaixo, depois de persistir a solicitação **e** obter a confirmação de publicação do broker. O envelope do 202 pode ser o `Pending` recém-criado: o worker pode avançar o estado antes da próxima consulta.
- Datas ou escopo inválidos: 400, **nenhum registro persistido e nenhuma publicação**. Campo `ownerId` ou `requesterId` no corpo é ignorado; o solicitante vem sempre da identidade autenticada.

### Envelope (POST 202, detalhe 200, itens da listagem)

```json
{
  "id": "rel-001",
  "scope": "Mine",
  "fromDate": "2026-09-01",
  "toDate": "2026-09-30",
  "status": "Pending",
  "createdAt": "2026-10-01T12:00:00Z",
  "startedAt": null,
  "completedAt": null,
  "result": null,
  "error": null
}
```

Instantes em UTC.

| Estado | Campos |
| :- | :- |
| `Pending` | `createdAt` preenchido; sem resultado. |
| `Processing` | `startedAt` preenchido; sem resultado. |
| `Completed` | `completedAt` e `result` preenchidos; `error` nulo. |
| `Failed` | `completedAt` e `error` preenchidos; `result` nulo. |

Em `Failed`, `error` tem `code` estável e `message` sanitizada (a redação humana não é comparada literalmente).

### Resultado (campo `result` quando `Completed`)

```json
{
  "generatedAt": "2026-10-01T12:00:02Z",
  "totalCount": 3,
  "totalAmount": 175.00,
  "byStatus": [
    {"status": "Draft", "count": 0, "amount": 0.00},
    {"status": "Submitted", "count": 0, "amount": 0.00},
    {"status": "Approved", "count": 2, "amount": 150.00},
    {"status": "Rejected", "count": 0, "amount": 0.00},
    {"status": "Paid", "count": 1, "amount": 25.00}
  ]
}
```

- Os cinco estados são sempre obrigatórios. Sem ocorrências: `totalCount` e `totalAmount` zero e os cinco estados com zero.
- Valores decimais, sem erro de ponto flutuante. O estado e o valor são os lidos **durante o processamento**, não os de quando a solicitação foi criada.
- Salvar `result` e `generatedAt`. Consultar não recalcula. Alterar um reembolso depois da conclusão não altera o relatório salvo.
- Não é exigido congelamento dos dados na solicitação nem snapshot transacional entre múltiplas consultas: o worker pode fazer consultas sem transação única. A ordem dos itens e as casas decimais da serialização não alteram o valor.
- Exemplo de verificação: reembolsos do solicitante no período (Approved 100,00; Approved 50,00; Paid 25,00), de outro funcionário no período (Approved 200,00) e do solicitante fora do período (Paid 500,00). `Mine` dá 3 itens e 175,00. `Global` daria 4 itens e 375,00. O registro fora do período fica de fora nos dois.

### Download Excel

- `GET /api/reports/{id}/excel`: **200** com arquivo `.xlsx` real (não CSV nem texto renomeado).
- `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- `Content-Disposition: attachment` com nome `.xlsx`, por exemplo `report-{id}.xlsx`.
- Conteúdo: identificador, escopo, período, instante de geração e tabela com os cinco estados, quantidades, valores e total geral. Contagens e valores em **células numéricas**, iguais ao resultado persistido (inclusive zeros e conjunto vazio).
- O arquivo pode ser montado no momento do download, a partir do resultado persistido, sem outra mensagem na fila e sem consultar os reembolsos de novo.
- Biblioteca livre, respeitando a licença. Sem serviço externo nem formatação elaborada. Registrar a biblioteca e a licença no README.
- Somente o solicitante autenticado: **404** para inexistente ou alheio, **409** para o próprio ainda não `Completed` ou `Failed`.

### Autorização

| Perfil | Mine | Global |
| :- | :- | :- |
| Employee | Sim | Não |
| Auditor | Não, salvo se também Employee | Sim |
| Employee e Auditor | Sim | Sim |
| Admin, Approver ou Finance sem os perfis acima | Não | Não |

Listagem, detalhe e Excel: **somente solicitações próprias**, mesmo para o Auditor (o privilégio global é sobre os dados do relatório dele, não sobre os relatórios dos outros). O proprietário nunca vem do corpo, de parâmetros do cliente nem de conteúdo não confiável da mensagem. No processamento de `Mine`, o worker filtra pelo solicitante registrado no servidor; calcular sobre todos os usuários também viola o isolamento.

### Códigos de resposta

| Cenário | Resposta |
| :- | :- |
| Sem autenticação | 401 |
| Perfil sem permissão para o escopo pedido | 403 |
| Detalhe ou Excel inexistente ou alheio | 404 |
| Excel do próprio relatório não concluído, ou `Failed` | 409 |
| Datas ou escopo inválidos | 400 |
| Solicitação persistida e publicação confirmada | 202 com `Location` |
| Banco indisponível antes de registrar | Erro explícito, sem 202 e sem identificador fictício |
| Publicação sem confirmação depois de registrar | 503 com o id persistido e erro sanitizado. O registro continua `Pending` e nunca vira `Failed` (falha de publicação não prova falha do cálculo). |

### Mensageria e confiabilidade

- Mensagem mínima: `{"reportId": "..."}` (mesmo tipo de identificador da aplicação). O worker lê período, escopo e solicitante do **registro persistido**. Documentar nome da fila e configuração de publicação e consumo.
- Fila **durável**, mensagens **persistentes**, **confirmação de publicação** (202 só depois dela; falha nunca vira sucesso falso).
- **Confirmação de consumo (ack) só depois de salvar.** Sem ack antecipado.
- **Idempotência persistente:** o mesmo `reportId` já concluído não gera outro resultado, inclusive depois de reiniciar o worker. O cálculo pode repetir; o efeito persistido não.
- `Completed` e `result` persistidos de forma consistente (mesma operação).
- Falha definitiva: `Failed` com erro sanitizado, **sem reencaminhamento infinito**. Falha ao salvar o resultado ou o estado de erro não pode ser confirmada como se tivesse persistido.
- Diferenciar falha de negócio definitiva de indisponibilidade transitória. Documentar como um `Processing` pode ser retomado. `Pending` e `Processing` não podem ser descartados para sempre só porque uma tentativa anterior começou.
- **Janela sem Transactional Outbox:** salvar e publicar são operações distintas. Uma queda entre elas pode deixar `Pending` sem mensagem. Retornar 503 com id quando não houver confirmação, preservar o registro e **documentar a limitação**. Não afirmar que reiniciar o worker recupera uma mensagem que nunca existiu. Novo POST cria outro id.
- Opcionais, sem bônus: escala, retentativas automáticas completas, Outbox e fila de mensagens não processáveis (DLQ).
- No RabbitMQ, `Ready` e `Unacked` não são os estados `Pending` e `Processing` da aplicação.

### Imagens, banco e execução

- Publicar **duas imagens reais** (web e worker), **depois** da aprovação dos testes, identificadas pelo **commit final** (nunca `latest`). Informar referências completas e a forma de acesso para correção, sem credenciais.
- Docker Compose usando as **imagens publicadas** (só construir localmente não comprova) e incluindo RabbitMQ com configuração persistente.
- Fornecer exemplo de configuração, inicialização do banco e instruções, sem segredos.
- Web e worker acessam o **mesmo estado**. SQLite exige arquivo persistente compartilhado e configuração coerente nos dois processos. O banco pode ser externo ao Compose se estiver documentado e reproduzível.
- Preservar as chaves e configurações necessárias à autenticação entre execuções.
- Não apagar volumes nem dados para esconder problemas.
- Logs correlacionados por `reportId` (publicação, início, conclusão, duplicata, falha). Sem tokens, senhas, cabeçalhos de autenticação nem connection strings.
- Guardar evidência da versão em execução (publicar a imagem não prova que o ambiente a executou).

### Testes mínimos (unitários, sem banco, rede nem RabbitMQ reais)

| Cenário | Asserção |
| :- | :- |
| Dados conhecidos e limites inclusivos do período | Quantidade e soma corretas por estado e no total |
| Nenhum reembolso elegível | Cinco estados e totais com zero |
| Data inicial posterior à final, ou datas ausentes ou inválidas | 400, nenhum registro persistido e nenhuma publicação |
| Employee solicita Global | Negação sem criar processamento autorizado |
| Solicitação de outro usuário | Não expõe detalhe nem resultado |
| Mesma mensagem novamente | Nenhum novo resultado ou efeito de conclusão |
| Consulta depois de alterar um reembolso | Retorna o resultado persistido, sem recálculo |
| Download do relatório concluído | Planilha válida, cabeçalhos corretos e números iguais ao resultado salvo |
| Download pendente, `Failed` ou alheio | 409 para próprio não concluído; 404 para alheio, sem expor dados |
| Publicação sem confirmação depois de registrar | 503 com o id persistido, registro ainda `Pending`, nenhum `Failed` (fake do publicador que não confirma) |
| Cálculo do relatório | Somente leitura: nenhum estado de reembolso é alterado |
| Corpo com `ownerId` ou `requesterId` | Ignorado; o solicitante gravado é o da identidade autenticada |
| Resultado do tratamento da mensagem | A decisão ack/nack é devolvida por uma classe testável (concluído, duplicata, falha definitiva, transitória); o consumidor só mapeia o resultado |

Documentar o comando `dotnet test` da suíte unitária. Sem pontos por cobertura percentual nem por quantidade de métodos. Testes antigos ou dos scripts de qualidade não contam.

### Experimentos obrigatórios (CP3-I06)

| Experimento | O que mostrar |
| :- | :- |
| E01 Fluxo normal | Autenticar, solicitar, guardar o id, acompanhar até `Completed`, baixar o Excel e conferir resultado e planilha contra dados conhecidos. |
| E02 Worker parado | Parar só o worker, solicitar, observar mensagem disponível e relatório não concluído, retomar e acompanhar o mesmo id até `Completed`. |
| E03 Duplicata | Depois de concluir, reiniciar o worker e republicar a mensagem com o mesmo `reportId` (por exemplo pelo painel de gerenciamento do RabbitMQ); comparar resultado e registros, sem novo efeito. |
| E04 Isolamento | Employee não solicita Global nem consulta ou baixa Excel alheio; Auditor solicita Global mas não consulta nem baixa relatório de outro usuário. |
| E05 Persistência e versão | Recriar apenas web e worker com as imagens publicadas (`docker compose up -d --force-recreate web worker`, ajustado aos nomes reais), consultar um resultado anterior e provar versão e dados preservados. Sem remover volumes. |

Dossiê curto com identificadores, comandos, resultados e logs sanitizados. Tokens ficam fora. Explicar aceitação, conclusão, reentrega, efeito idempotente, a diferença entre worker parado e broker indisponível e a janela sem outbox.

### Rubrica (100 pontos; nota-base = pontos ÷ 10)

| Item | Unidades | Total |
| :- | :- | :- |
| I01 Contrato e resultado | Entrada, período e solicitação 4; listagem, detalhe e estados 4; resumo, totais e vazio 8; resultado salvo sem recálculo 2; Excel válido e fiel 2 | 20 |
| I02 Autorização | Autenticação, perfil e escopo 5; isolamento de listagem, detalhe e Excel 5; identidade e escopo preservados até o worker 5 | 15 |
| I03 Mensageria | Publicação, fila e mensagem 5; worker e confirmação após salvar 5; conclusão e falha consistentes 5; duplicata persistente sem novo efeito 5 | 20 |
| I04 Testes | Cálculo, vazio e período 5; negações e isolamento 5; duplicata, resultado persistido e Excel 5 | 15 |
| I05 Entrega executável | Duas imagens e testes antes de publicar 5; Compose, imagens e RabbitMQ 5; banco, armazenamento, instruções e segredos 5 | 15 |
| I06 Experimentos | E01 com Excel e E02 correlacionados 3; E03 2; E04 2; E05 2; explicação das falhas e da janela sem outbox 1 | 10 |
| I07 Qualidade | Sem novos diagnósticos atribuíveis 2; convenções 1; verificações preservadas 1; higiene dos fontes 1 | 5 |

Cada unidade vale tudo ou nada. Captura isolada ou ticket fechado não conta sem o comportamento.

**Tetos:** não compila (web ou worker obrigatório não compila no ambiente declarado, depois de validar a configuração usada para corrigir) = 2,0; autorização sistematicamente ausente (acesso indevido reproduzível em pelo menos duas operações entre solicitar, listar, consultar e baixar Excel) = 4,0; sem testes próprios significativos de cálculo, negação e duplicata (executáveis, aprovados e com asserções de comportamento) = 8,0.

Uma falha localizada de autorização reduz a I02, mas não ativa o teto. Falha do ambiente do professor não gera teto para o grupo. Ter testes significativos afasta o teto de 8,0, mas não garante os 15 pontos da I04, que exige também os demais cenários publicados. O bônus do LinkedIn (+1) nunca ultrapassa os tetos nem 10.

### Checklist de entrega (Teams)

- Referência do grupo, integrantes, commit original do CP2 (SHA completo de `2e74619`) e commit final do CP3.
- Duas imagens: referências completas, versão do commit e forma de acesso para correção, sem credenciais.
- Execução: Compose, banco compatível, inicialização e instruções reproduzíveis.
- Arquivo `.http` (ou coleção) sem tokens persistidos, cobrindo solicitar, consultar e baixar uma planilha válida.
- Testes: comando e resultados dos testes próprios, separados dos experimentos.
- Dossiê E01 a E05 e diagnóstico (aceitação e conclusão; worker e broker; duplicata e novo POST; janela sem outbox).
- LinkedIn (opcional, individual): link no Teams, mais captura se o acesso for restrito.
- Código compactado, se o Teams pedir: só fontes e exemplo de configuração, sem binários, caches, dados pessoais nem segredos.

## Pontos de atenção técnicos (validar no plano de cada issue)

Estes pontos vêm da leitura do código atual e da apostila. Confirme cada um no código antes de decidir.

1. **Estrutura atual.** Tudo vive em `ExpenseHub.Api` (SDK Web; `Program` é `internal static`; domínio, persistência, serviços e identidade juntos) e há `ExpenseHub.UnitTests`. O worker precisa do `DbContext`, das entidades e dos conversores. O plano da I03 deve comparar as opções (worker referenciando o projeto Api; extrair uma biblioteca compartilhada; biblioteca só com o necessário) e escolher a de menor risco para os 611 testes, para o zero aviso e para a imagem do worker. Não mover namespaces sem necessidade. **Decidido: opção A** (ver "Decisões tomadas").
2. **Novos projetos herdam** o `Directory.Build.props` (ImplicitUsings desligado, analisadores, StyleCop, zero avisos). Todo `using` explícito, `<summary>` em inglês nos tipos públicos.
3. **Dinheiro.** `Expense.Amount` é `decimal` e o banco guarda centavos inteiros (`MoneyConversion`). Somar em centavos inteiros e converter para `decimal` só na saída. Nada de `double`.
4. **Datas.** `Expense.ExpenseDate` é `DateOnly`. Filtro inclusivo nos dois lados. Todo novo `DateTimeOffset` persistido (criado, iniciado, concluído, gerado) precisa do `UtcDateTimeOffsetConverter`, senão `OrderBy`, `Max` e `Where` falham no SQLite.
5. **Filtro de escopo na consulta.** `Mine` filtra por `OwnerId` igual ao solicitante registrado; `Global` não filtra por dono. Aplicar o filtro na consulta ao banco, nunca carregar tudo e filtrar na memória. O worker usa o solicitante e o escopo do registro persistido, nunca da mensagem.
6. **Papéis.** `AppRoles` já tem `Employee`, `Auditor` etc. A regra de escopo por papel precisa ficar numa classe simples e testável, fora do controller. Admin, Approver e Finance sem Employee ou Auditor não solicitam nada (403).
7. **SQLite compartilhado.** Web e worker escrevem no mesmo arquivo: usar um volume nomeado montado no mesmo caminho nos dois contêineres, ativar WAL e definir tempo de espera de bloqueio (busy timeout) para evitar `database is locked`. Documentar. Concorrência de escrita entre dois processos é um risco real; tratar `SQLITE_BUSY` como transitório.
8. **Chaves do bearer token.** O esquema usa `AddBearerToken` (tokens protegidos pelo Data Protection). Ao recriar o contêiner da web (E05), as chaves somem a menos que sejam persistidas num volume. Persistir o conjunto de chaves (sem versionar as chaves) para os tokens sobreviverem à recriação. A apostila aceita nova autenticação no E05, mas pede preservar a configuração necessária entre execuções.
9. **Migrations.** A aplicação não aplica migrations na inicialização. O Compose precisa de um passo reprodutível de inicialização do banco (por exemplo um serviço de uma execução só, ou um bundle do EF). Decidir no plano da I05 (ver "Decisões tomadas": só duas imagens), sem apagar dados. A senha do Admin do seed vem de configuração, nunca de arquivo versionado.
10. **Idempotência.** O estado vive no registro do relatório (`Pending`, `Processing`, `Completed`, `Failed`). Transições condicionais (por exemplo atualização com `WHERE Status = ...`) evitam corrida entre tentativas. `Completed` com `result` no mesmo `SaveChangesAsync`. Mensagem de relatório já `Completed` ou `Failed`: ack sem novo efeito. `Processing` antigo deve poder ser retomado.
11. **Falhas.** Definitiva (dado inconsistente, escopo inválido no registro): `Failed` + `error {code, message}` sanitizado + ack. Transitória (banco ocupado, indisponível): não confirmar como sucesso; não girar em laço infinito; documentar o comportamento. Nunca registrar nem devolver stack trace, connection string ou token.
12. **Publicação.** Usar o cliente oficial do RabbitMQ para .NET (conferir a versão estável atual e a API assíncrona) com confirmação de publicação habilitada. Encapsular atrás de uma interface (por exemplo `IReportPublisher`) para os testes usarem um fake. Na I01, a interface nasce junto do POST; a implementação real chega na I03. Enquanto não houver implementação real registrada, o POST não pode fingir sucesso (503, não 202).
13. **Biblioteca de Excel.** **Decidido: `DocumentFormat.OpenXml` (MIT).** Conferir no NuGet a versão, a licença e as dependências transitivas no plano da I01 e registrar a licença no README. ClosedXML ficou de fora porque as versões recentes podem puxar dependências com licença própria; EPPlus também (licença não comercial nas versões recentes). Testar abrindo os bytes gerados com a própria biblioteca e conferindo células numéricas.
14. **Imagens (I05).** Criar um **workflow novo** em `.github/workflows/` (não editar `build.yml`, que é do professor). Fluxo: testes primeiro, depois build e push das duas imagens para um registry público (sugestão: GitHub Container Registry), com tag igual ao **SHA completo do commit** e sem `latest`. O pacote publicado precisa ficar **público** para a correção puxar sem credenciais (ajuste manual do Renan na página do pacote no GitHub).
15. **Circularidade do SHA.** O Compose não pode conter o SHA do commit que o contém. Parametrizar a tag (`${IMAGE_TAG}`) com valor pelo ambiente ou arquivo `.env` local, e entregar o SHA e as referências no Teams. O último commit da `main` deve ser o commit a partir do qual o workflow publicou as imagens. Congelar a `main` depois disso.
16. **Compose.** RabbitMQ com volume nomeado para dados, imagem com painel de gerenciamento (necessário para o E03), verificação de saúde, credenciais por variável de ambiente com `.env.example` sem segredos (o `.gitignore` já ignora `*.env`). Nunca `docker compose down -v`, nem `docker volume rm` ou `prune`.
17. **Segredos e FIAP1002.** Nunca atribuir literal a nome que contenha password, senha, secret, token ou api key, nem em testes nem em Compose de exemplo versionado. Montar valores em tempo de execução ou ler de configuração. Rodar o script de qualidade depois de `git add -A` (só para alimentar o script) e ler os IDs dos achados, não só a nota.
18. **Registros (logs).** Correlacionar tudo por `reportId`. Sem tokens, senhas, cabeçalhos de autenticação nem connection strings.
19. **Ambiente do Renan.** Windows com Git Bash (o agente roda comandos no Git Bash) e Visual Studio 2026. Para `docker run -v`, o Git Bash converte caminhos Unix em caminhos do Windows; prefira `docker compose` ou use `MSYS_NO_PATHCONV=1`. Docker Desktop é pré-requisito dos experimentos e deve ser instalado cedo.
20. **Pasta de trabalho.** O agente roda na raiz do repositório (`expensehub-api`), não em `sources/`. Todos os comandos deste projeto usam caminhos relativos à raiz.
21. **Binding do DTO.** `DateOnly` e enum não anuláveis aceitam omissão sem erro (`0001-01-01` e `Mine = 0`). Usar `DateOnly?` e `scope` anulável com `[Required]`, e rejeitar valor numérico no enum (por exemplo `"scope": 0`), senão "datas obrigatórias" e "escopo inválido" não devolvem 400. Testar corpo sem campos, data inválida e escopo inexistente.
22. **Tradução EF/SQLite.** `GroupBy` com `Sum` sobre `Amount` (conversor decimal para centavos) pode não traduzir para SQL, e o filtro por `DateOnly` também precisa ser conferido. Testes com InMemory não provam a tradução: confirmar contra SQLite real. Plano B: projetar `(Status, Amount)` já filtrados no SQL e somar centavos em memória.
23. **Formato dos instantes.** `DateTimeOffset` com offset zero pode serializar como `+00:00`; o exemplo da apostila usa `Z`. São equivalentes, mas convém emitir `Z`.
24. **Testabilidade do worker.** "Ack só depois de salvar" só é testável se a decisão estiver numa classe que devolve um resultado (concluído, duplicata, falha definitiva, transitória) e o consumidor do RabbitMQ apenas mapeia esse resultado para ack ou nack.
25. **Publicação sem confirmação.** A apostila exige que esse tratamento seja verificável por teste. Usar um fake de `IReportPublisher` que não confirma e afirmar: 503, id persistido, registro ainda `Pending`.
26. **GHCR e CODEOWNERS.** O pacote precisa ser tornado público manualmente pelo Renan. O `CODEOWNERS` tem `* @Racass` e não se sabe se existe proteção de branch exigindo revisão dele. Se uma PR ficar travada, avisar o Renan; não contornar.

## Seções por issue

Cada seção abaixo é preenchida pelo agente ao terminar a issue. A lista de unidades da rubrica serve de checklist: marcar só o que está provado por teste, execução ou evidência.

### CP3-00: Preparação

**Estado:** concluída; PR #11 da equipe, merge commit `6d7f811`. **Branch:** `cp3-00-preparacao`.

- [x] Pasta `andamento/` com este arquivo e os dois HTML (copiados pelo Renan, commit `4c9b2a8`).
- [x] `CLAUDE.md` do CP3 no lugar (commit `4c9b2a8`).
- [x] Issues #11 a #18 salvas em `andamento/requisitos/issues-cp3.md` (commit `4c9b2a8`).
- [x] Baseline registrado: build 0 avisos e 0 erros, 611 testes aprovados, SDK 10.0.403, Docker 29.9.2 com daemon ativo. Score 100/100 é o do CP2 (o script exige `git add -A` e não foi rodado nesta etapa).
- [x] Decisões da leitura inicial registradas na seção "Decisões tomadas".
- [x] README reorganizado e `CHECKPOINT-2.md` e `CHECKPOINT-3.md` criados, sem perda de conteúdo do CP2. Prova: as 1044 linhas do README original viraram 259 mantidas no README, 768 movidas para o `CHECKPOINT-2.md` e 17 descartadas (Sumário antigo e uma linha em branco); 7 linhas do texto movido tiveram só o link ou a referência corrigidos; script de verificação com 83 conferências aprovadas; build 0 avisos e 0 erros; 611 testes aprovados; script de qualidade 100/100 (único achado, informativo: `FIAP0002`, Gitleaks ignorado por `-SkipGitleaks`).

Apoio: Decisões de projeto, Fora de escopo e Fluxo de trabalho do CP2 foram para o `CHECKPOINT-2.md`; Testes e Qualidade ficam inteiros no README. CLAUDE.md corrigido (título da seção Decisions e frase transitória removida). Depois do push, abrir 4 links no GitHub para conferir as âncoras com acento (lista no `andamento/PR-CP3-00.md`).

### CP3-I01: Contrato e resultado do relatório (20)

**Estado:** concluída; PR #12 da equipe, merge commit `48810e3` (SHA completo `48810e3f69def12fdc391d5d6e440af75f481836`); score oficial do workflow `code-quality` 100/100 na execução #69 (informado pelo Renan). **Branch:** `cp3-i01`. **Issue:** #11.

- [x] Entrada, período e solicitação persistida conforme o contrato (4): `CreateReportRequestTests`, `ReportRulesTests`, `ReportServiceRequestTests`, `ReportsControllerTests`; HTTP real com 11 corpos inválidos e 0 linhas gravadas
- [x] Listagem, detalhe e estados observáveis (4): `ReportResponseTests`, `ReportServiceQueryTests`; HTTP real com `Pending`, `Completed` e `Failed`
- [x] Resumo por estado e totais corretos, inclusive conjunto vazio (8): `ReportCalculatorTests` (24); SQLite real igual a um oráculo independente
- [x] Resultado e instante de geração salvos; consulta sem recálculo (2): `ReportResultSerializerTests` e `ReportServiceQueryTests` (leituras de reembolso em zero)
- [x] Excel válido, fiel ao resultado salvo e com o contrato de download (2): `ReportExcelWriterTests` com `OpenXmlValidator`; download real com `Content-Type` e `attachment`

Apoio, tudo verificado: DTO com `DateOnly?` e escopo como texto obrigatório (número, `"0"`, outra caixa e vazio dão 400; teste de desserialização e HTTP real); datas inválidas não persistem nem publicam; `ownerId` e `requesterId` do corpo ignorados; cálculo somente leitura (teste e SQLite real: tabela idêntica e nada rastreado); tradução EF/SQLite conferida (ponto 22: projeção estreita traduz; o agrupamento no banco também traduz e dá valores certos, mas não foi adotado); `.http` com login, solicitar, listar, consultar e baixar, sem credenciais (`http-client.env.json.user` ignorado); `Guid` como identificador; POST responde 503 com o id enquanto não houver publicador real (teste com fake que não confirma e HTTP real); pacote `DocumentFormat.OpenXml` 3.5.1 (MIT).

Resultados: build 0 avisos e 0 erros; 788 testes aprovados (611 do CP2 e 177 novos); script de qualidade 100/100 (achado informativo `FIAP0002`; um primeiro passe deu 99 por `IDE1006` em campos `private static readonly`, corrigido). Pendências para as próximas issues: regra de escopo por papel e testes de isolamento por papel (CP3-I02); publicador real, worker e `202` de verdade (CP3-I03); completar o `.http` (CP3-I06). A migration `AddReportRequests` precisa de `dotnet ef database update` em bancos existentes.

### CP3-I02: Autorização e isolamento (15)

**Estado:** concluída; PR #13 da equipe, merge commit `6e47693` (SHA completo `6e476939514aa88710b5158ea3f92a9198d632fe`); score oficial do workflow `code-quality` 100/100 na execução #72 (informado pelo Renan). **Branch:** `cp3-i02`. **Issue:** #12.

- [x] Autenticação e permissão por perfil e escopo (5): `ReportAccessTests` (59, tabela escrita à mão das 32 combinações das cinco roles), `ReportServiceAuthorizationTests` (35), `ReportEndpointRolesTests` (6); duas mutações quebraram os testes (42 e 2) e foram desfeitas; HTTP real com 7 contas x `Mine` e `Global`
- [x] Listagem, detalhe e Excel isolados por solicitante, inclusive Auditor (5): `ReportIsolationTests` (36, oito conjuntos de roles x lista, detalhe e Excel x quatro estados); HTTP real com oito contas cruzadas, `404` igual ao de um id inexistente
- [x] Solicitante e escopo controlados pelo servidor até o processamento (5): registro com `RequesterId` e `Scope` (teste e banco real), `ReportQuery.From` e `ReportQueryFromRequestTests` (10), mensagem do publicador só com o `Guid`

Apoio, tudo verificado: ordem das respostas (401, 403 por role, 400, 403 por escopo) provada com HTTP real (sem role e corpo inválido dá 403; Employee com período invertido e `Global` dá 400); corpo com `ownerId`, `requesterId` e `status` ignorado (testes da I01 seguem passando e HTTP real); leitura sem role, só posse; `.http` com login do Auditor, `Mine`, `Global`, os dois `403` e o `404`; testes de serviço da I01 ajustados para um chamador `Employee`.

Resultados: build 0 avisos e 0 erros; 936 testes aprovados (788 da base e 148 novos); script de qualidade 100/100 (achado informativo `FIAP0002`); HTTP real com 120 conferências aprovadas, log sem senha, token nem e-mail. Pendências: `main` fica com `Global` aberto a qualquer autenticado até esta PR entrar (mergear em seguida); publicador real, worker e `202` de verdade na CP3-I03 (que chama `ReportQuery.From`); completar o `.http` na CP3-I06.

### CP3-I03: RabbitMQ, worker e idempotência (20)

**Estado:** concluída; PR #14 da equipe, merge commit `78bca7d` (SHA completo `78bca7d00a785bb37bcedc5b40bcd14adffa2535`); score oficial do workflow `code-quality` 100/100 na execução #75 (informado pelo Renan). **Branch:** `cp3-i03`. **Issue:** #13.

- [x] Publicação confirmada, fila durável e mensagens persistentes (5): `ReportQueueTopologyTests`, `ReportMessageTests`, `RabbitMqOptionsTests`, `MessagingRegistrationTests`; RabbitMQ real: `202` só depois da confirmação, fila durável (`durable` verdadeiro, `auto_delete` e `exclusive` falsos), mensagem persistente (`delivery_mode` 2) só com o `reportId`, broker parado dá `503` com o `id` e `Pending`
- [x] Worker processa; consumo confirmado só depois de salvar (5): `ReportMessageProcessorTests`, `ReportDeliveryTests` (nenhum ponto de falha de gravação chega a ack); RabbitMQ real: com o `Processing` visível a mensagem está sem ack (`0 1 1`), o worker é morto e a mensagem volta (`1 0 0`)
- [x] Conclusão e resultado consistentes; falha definitiva registrada (5): `Completed` e resultado numa só chamada (teste e `CHECK` do banco), resultado igual ao da calculadora, `Failed` com `InvalidRequest` e mensagem fixa; RabbitMQ real: exemplo da apostila (`Mine` 3 e 175,00; `Global` 4 e 375,00), falha definitiva confirmada uma só vez
- [x] Duplicata reconhecida sem novo efeito, com proteção persistente (5): processador novo sobre o mesmo estado continua `Duplicate`; RabbitMQ real: worker reiniciado e mensagem republicada, linha do banco idêntica byte a byte

Apoio, tudo verificado: decisão ack/nack numa classe pura (`ReportDelivery`); política de falha transitória (espera de 5 s e devolve, sem limite e sem laço quente) e retomada de `Processing` (recalcula, não regrava o início); SQLite com `busy_timeout` antes do WAL (spike com dois processos abrindo um banco sem WAL, sem erro em nenhuma das duas ordens) e espera por bloqueio medida; pacote `RabbitMQ.Client` 7.2.2 (aprovado no plano); logs por `reportId` sem segredos; janela sem Transactional Outbox e `503` sem garantia de que a mensagem não chegou documentados; web e worker no mesmo arquivo de banco, por caminho absoluto em variável de ambiente (README e CHECKPOINT-3); `docker run` do RabbitMQ com volume nomeado e a explicação de onde o broker guarda os dados.

Resultados: build 0 avisos e 0 erros nos três projetos; 1086 testes aprovados (936 da base e 150 novos); duas mutações (10 e 5 testes quebrados), desfeitas; script de qualidade 100/100 (achado informativo `FIAP0002`); spike em SQLite real (22 conferências, mais a medição da espera e a corrida de dois processos); validação real com RabbitMQ 4.4.0 em contêiner (65 conferências aprovadas em 7 cenários). Pendências: o contêiner `expensehub-i03-rabbitmq` ficou parado e o volume do projeto `expensehub-i03` continua no Docker (a limpeza é do Renan); imagens, Compose e a fixação da versão do RabbitMQ na CP3-I05; completar o `.http` e o dossiê na CP3-I06.

### CP3-I04: Testes próprios significativos (15)

**Estado:** pendente. **Branch:** `cp3-i04`. **Issue:** #14.

- [ ] Cálculo, conjunto vazio e validação de período (5)
- [ ] Negação por perfil e escopo; isolamento por solicitante (5)
- [ ] Duplicata, resultado persistido e Excel, com asserções de comportamento (5)

Apoio: tabela cenário por teste no `CHECKPOINT-3.md`; verificação de mutação nas regras novas; comando `dotnet test` documentado.

### CP3-I05: Publicação e execução reproduzível (15)

**Estado:** concluída na branch, aguardando PR e merge (texto da PR em `andamento/PR-CP3-I05.md`). **Branch:** `cp3-i05`. **Issue:** #15.

- [x] Duas imagens identificadas pelo commit, com testes antes da publicação (5): dois Dockerfiles com as mesmas bases, `publish-images.yml` com `needs: test`, tag `${GITHUB_SHA}` completa, sem `latest` e push só na `main`; `DeliveryFilesTests` (22) com mutações; as duas imagens construídas aqui. **A publicação em si (execução na `main` e pacotes no GHCR) só acontece no merge.**
- [x] Compose executa as imagens e o RabbitMQ com configuração documentada (5): `compose.yaml`, RabbitMQ 4.4.0 por tag e digest, hostname fixo, volume, healthcheck, README; subida real aqui com as imagens locais e os mesmos nomes (71 conferências). **`docker compose pull` com o SHA só depois do merge e de tornar os pacotes públicos.**
- [x] Banco e armazenamento compatíveis, inicialização reproduzível e segredos externos (5): volume `/data` único nos dois contêineres (WAL entre dois contêineres), flag `Database:MigrateOnStartup` (4 migrations na primeira partida, 0 na segunda), chaves em volume (token anterior válido depois de recriar), `.env.example` em branco, FIAP1002 limpo, logs sem as credenciais geradas.

Apoio, tudo verificado: decisão pela flag de migração em vez do bundle do EF (zero serviços novos); `Build__Commit` + label OCI `revision` + linha de log na partida como evidência da versão (E05); `/keys` e `/data` entregues ao usuário `app` nos Dockerfiles; `${VAR:?mensagem}` em todas as variáveis obrigatórias (a de `RABBITMQ_PASSWORD` fica na linha `RabbitMq__Password`, porque o script de qualidade leria `PASSWORD:?` como valor literal em outra linha); `docker inspect` sempre com `--format` de campos específicos (o `Env` completo traz as credenciais); ambiente `compose` no `http-client.env.json`; descoberta e correção do NETSDK1152 no `publish` do worker (o `appsettings.json` da Api colidia com o do worker); gatilho `pull_request` no workflow (acréscimo para exercitar os Dockerfiles antes do merge).

Resultados: build 0 avisos e 0 erros; 1149 testes aprovados (1086 da base e 63 novos); quatro mutações quebraram 11 testes e foram desfeitas; script de qualidade 100/100 (achado informativo `FIAP0002`); validação real no Docker com 71 conferências aprovadas (primeira subida, fluxo, E02, E05, broker recriado, logs).

**Passos manuais do Renan depois do merge:** (1) conferir que o workflow `publish-images` terminou na `main`; (2) tornar públicos os dois pacotes (`expensehub-web` e `expensehub-worker`) na página deles no GitHub (Package settings, Change visibility, Public); (3) `docker compose pull` com `IMAGE_TAG` igual ao SHA completo do commit; (4) preencher o número da PR e o score oficial no README, no CHECKPOINT-3 e aqui. Pendências: limpeza dos contêineres parados e dos três volumes do projeto `expensehub-i05` que a validação deixou no Docker; completar o `.http` e o dossiê na CP3-I06.

### CP3-I06: Experimentos e diagnóstico (10)

**Estado:** pendente. **Branch:** `cp3-i06`. **Issue:** #16.

- [ ] E01 com Excel e E02 correlacionados na mesma solicitação (3)
- [ ] E03 duplicata depois de reiniciar o worker, sem novo efeito (2)
- [ ] E04 cenários de autorização reproduzíveis (2)
- [ ] E05 persistência depois de recriar web e worker (2)
- [ ] Diagnóstico correto de aceitação, publicação e janela sem outbox (1)

Apoio: contas do E04 (duas Employee e uma Auditor) criadas sem senha versionada; `.http` completo; SHA usado nos experimentos informado no dossiê.

### CP3-I07: Qualidade objetiva das alterações (5)

**Estado:** pendente. **Branch:** `cp3-i07`. **Issue:** #17.

- [ ] Sem novos warnings ou diagnósticos atribuíveis ao trabalho (2)
- [ ] Naming e formatação seguem as regras existentes (1)
- [ ] Analisadores e verificações não foram enfraquecidos (1)
- [ ] Higiene da entrega: só fontes, sem binários nem arquivos locais (1)

## Pendências finais (antes do SHA de entrega)

- [ ] Remover a pasta `andamento/` e as regras que a citam no `CLAUDE.md`.
- [ ] Conferir `git status` e arquivos rastreados: sem `bin/`, `obj/`, `.vs/`, bancos, `.env`, chaves, planilhas baixadas.
- [ ] `main` com o commit final; workflow de publicação terminou; imagens com a tag do SHA completo; pacotes públicos.
- [ ] `docker compose pull` com a tag do SHA e execução conferida.
- [ ] `git diff --stat` do SHA dos experimentos até o SHA final mostra só documentação, e isso está dito no dossiê.
- [ ] Texto de entrega para o Teams: URL do repositório, SHA do CP2 (completo), SHA final do CP3, referências das duas imagens, comando de acesso, comando dos testes.
- [ ] LinkedIn (opcional, manual, texto próprio): publicar e enviar o link individualmente no Teams.
