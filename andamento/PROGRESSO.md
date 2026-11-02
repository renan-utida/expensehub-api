# PROGRESSO do CP3

Registro temporário de contexto e andamento do Checkpoint 3 (CP3). Esta pasta (`andamento/`) será removida antes do SHA final. Leia este arquivo por inteiro antes de começar qualquer issue. Ao terminar uma issue, atualize a linha "Estado atual", a tabela de status e a seção da issue, e confira se o `CHECKPOINT-3.md` e o `README.md` também foram atualizados (ver "Organização da documentação" no `CLAUDE.md`).

## Estado atual

> CP3-00 (preparação) ainda não iniciada. Nenhuma issue do CP3 começou. Próximo passo: concluir a CP3-00 e abrir a CP3-I01.

## Links

- Repositório da equipe (código e PRs): https://github.com/renan-utida/expensehub-api
- Repositório do professor (issues centrais, não fechar): https://github.com/Racass/checkpoint-csharpracass-expensehub
- Lista das issues do CP3: https://github.com/Racass/checkpoint-csharpracass-expensehub/issues?q=is%3Aissue+CP3
- Issues originais: CP3-I01 = #11, CP3-I02 = #12, CP3-I03 = #13, CP3-I04 = #14, CP3-I05 = #15, CP3-I06 = #16, CP3-I07 = #17, CP3-EXTRA-LINKEDIN = #18.
- Fontes locais da especificação: `andamento/requisitos/apostila-cp3.html` e `andamento/requisitos/slides-cp3.html` (autoridade em caso de dúvida) e `andamento/requisitos/issues-cp3.md` (texto das issues #11 a #18 como o professor publicou).

## Prazo e regras do jogo

- **Prazo:** quarta-feira, 04/11/2026, às 23:59, no Teams. A apostila (em três lugares) e as issues centrais dizem "04/10"; é erro de digitação, vale a data do Teams.
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

## Status

| Item | Estado | Branch | PR | Pontos da rubrica |
| :- | :- | :- | :- | :- |
| CP3-00 Preparação | Pendente | `cp3-00-preparacao` | n/d | n/d |
| CP3-I01 Contrato e resultado | Pendente | `cp3-i01` | n/d | 20 |
| CP3-I02 Autorização e isolamento | Pendente | `cp3-i02` | n/d | 15 |
| CP3-I03 RabbitMQ, worker e idempotência | Pendente | `cp3-i03` | n/d | 20 |
| CP3-I04 Testes próprios | Pendente | `cp3-i04` | n/d | 15 |
| CP3-I05 Publicação e execução reproduzível | Pendente | `cp3-i05` | n/d | 15 |
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
- Aceitação normal: **202**, cabeçalho `Location` apontando para o detalhe e o envelope abaixo, depois de persistir a solicitação **e** obter a confirmação de publicação do broker.

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
| Publicação sem confirmação depois de registrar | 503 com o id persistido e erro sanitizado |

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
| Data inicial posterior à final | Solicitação rejeitada sem publicação |
| Employee solicita Global | Negação sem criar processamento autorizado |
| Solicitação de outro usuário | Não expõe detalhe nem resultado |
| Mesma mensagem novamente | Nenhum novo resultado ou efeito de conclusão |
| Consulta depois de alterar um reembolso | Retorna o resultado persistido, sem recálculo |
| Download do relatório concluído | Planilha válida, cabeçalhos corretos e números iguais ao resultado salvo |
| Download pendente, `Failed` ou alheio | 409 para próprio não concluído; 404 para alheio, sem expor dados |

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

**Tetos:** não compila = 2,0; autorização sistematicamente ausente (acesso indevido reproduzível em pelo menos duas operações entre solicitar, listar, consultar e baixar Excel) = 4,0; sem testes próprios significativos de cálculo, negação e duplicata = 8,0.

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

1. **Estrutura atual.** Tudo vive em `ExpenseHub.Api` (SDK Web; `Program` é `internal static`; domínio, persistência, serviços e identidade juntos) e há `ExpenseHub.UnitTests`. O worker precisa do `DbContext`, das entidades e dos conversores. O plano da I03 deve comparar as opções (worker referenciando o projeto Api; extrair uma biblioteca compartilhada; biblioteca só com o necessário) e escolher a de menor risco para os 611 testes, para o zero aviso e para a imagem do worker. Não mover namespaces sem necessidade.
2. **Novos projetos herdam** o `Directory.Build.props` (ImplicitUsings desligado, analisadores, StyleCop, zero avisos). Todo `using` explícito, `<summary>` em inglês nos tipos públicos.
3. **Dinheiro.** `Expense.Amount` é `decimal` e o banco guarda centavos inteiros (`MoneyConversion`). Somar em centavos inteiros e converter para `decimal` só na saída. Nada de `double`.
4. **Datas.** `Expense.ExpenseDate` é `DateOnly`. Filtro inclusivo nos dois lados. Todo novo `DateTimeOffset` persistido (criado, iniciado, concluído, gerado) precisa do `UtcDateTimeOffsetConverter`, senão `OrderBy`, `Max` e `Where` falham no SQLite.
5. **Filtro de escopo na consulta.** `Mine` filtra por `OwnerId` igual ao solicitante registrado; `Global` não filtra por dono. Aplicar o filtro na consulta ao banco, nunca carregar tudo e filtrar na memória. O worker usa o solicitante e o escopo do registro persistido, nunca da mensagem.
6. **Papéis.** `AppRoles` já tem `Employee`, `Auditor` etc. A regra de escopo por papel precisa ficar numa classe simples e testável, fora do controller. Admin, Approver e Finance sem Employee ou Auditor não solicitam nada (403).
7. **SQLite compartilhado.** Web e worker escrevem no mesmo arquivo: usar um volume nomeado montado no mesmo caminho nos dois contêineres, ativar WAL e definir tempo de espera de bloqueio (busy timeout) para evitar `database is locked`. Documentar. Concorrência de escrita entre dois processos é um risco real; tratar `SQLITE_BUSY` como transitório.
8. **Chaves do bearer token.** O esquema usa `AddBearerToken` (tokens protegidos pelo Data Protection). Ao recriar o contêiner da web (E05), as chaves somem a menos que sejam persistidas num volume. Persistir o conjunto de chaves (sem versionar as chaves) para os tokens sobreviverem à recriação. A apostila aceita nova autenticação no E05, mas pede preservar a configuração necessária entre execuções.
9. **Migrations.** A aplicação não aplica migrations na inicialização. O Compose precisa de um passo reprodutível de inicialização do banco (por exemplo um serviço de uma execução só, ou um bundle do EF). Decidir no plano da I05 sem apagar dados. A senha do Admin do seed vem de configuração, nunca de arquivo versionado.
10. **Idempotência.** O estado vive no registro do relatório (`Pending`, `Processing`, `Completed`, `Failed`). Transições condicionais (por exemplo atualização com `WHERE Status = ...`) evitam corrida entre tentativas. `Completed` com `result` no mesmo `SaveChangesAsync`. Mensagem de relatório já `Completed` ou `Failed`: ack sem novo efeito. `Processing` antigo deve poder ser retomado.
11. **Falhas.** Definitiva (dado inconsistente, escopo inválido no registro): `Failed` + `error {code, message}` sanitizado + ack. Transitória (banco ocupado, indisponível): não confirmar como sucesso; não girar em laço infinito; documentar o comportamento. Nunca registrar nem devolver stack trace, connection string ou token.
12. **Publicação.** Usar o cliente oficial do RabbitMQ para .NET (conferir a versão estável atual e a API assíncrona) com confirmação de publicação habilitada. Encapsular atrás de uma interface (por exemplo `IReportPublisher`) para os testes usarem um fake. Na I01, a interface nasce junto do POST; a implementação real chega na I03. Enquanto não houver implementação real registrada, o POST não pode fingir sucesso (503, não 202).
13. **Biblioteca de Excel.** Escolher uma com licença permissiva e registrar a licença no README. Candidatas a verificar no NuGet: ClosedXML e DocumentFormat.OpenXml. Evitar EPPlus (licença não comercial nas versões recentes). Testar abrindo os bytes gerados com a própria biblioteca e conferindo células numéricas.
14. **Imagens (I05).** Criar um **workflow novo** em `.github/workflows/` (não editar `build.yml`, que é do professor). Fluxo: testes primeiro, depois build e push das duas imagens para um registry público (sugestão: GitHub Container Registry), com tag igual ao **SHA completo do commit** e sem `latest`. O pacote publicado precisa ficar **público** para a correção puxar sem credenciais (ajuste manual do Renan na página do pacote no GitHub).
15. **Circularidade do SHA.** O Compose não pode conter o SHA do commit que o contém. Parametrizar a tag (`${IMAGE_TAG}`) com valor pelo ambiente ou arquivo `.env` local, e entregar o SHA e as referências no Teams. O último commit da `main` deve ser o commit a partir do qual o workflow publicou as imagens. Congelar a `main` depois disso.
16. **Compose.** RabbitMQ com volume nomeado para dados, imagem com painel de gerenciamento (necessário para o E03), verificação de saúde, credenciais por variável de ambiente com `.env.example` sem segredos (o `.gitignore` já ignora `*.env`). Nunca `docker compose down -v`, nem `docker volume rm` ou `prune`.
17. **Segredos e FIAP1002.** Nunca atribuir literal a nome que contenha password, senha, secret, token ou api key, nem em testes nem em Compose de exemplo versionado. Montar valores em tempo de execução ou ler de configuração. Rodar o script de qualidade depois de `git add -A` (só para alimentar o script) e ler os IDs dos achados, não só a nota.
18. **Registros (logs).** Correlacionar tudo por `reportId`. Sem tokens, senhas, cabeçalhos de autenticação nem connection strings.
19. **Ambiente do Renan.** Windows com Git Bash (o agente roda comandos no Git Bash) e Visual Studio 2026. Para `docker run -v`, o Git Bash converte caminhos Unix em caminhos do Windows; prefira `docker compose` ou use `MSYS_NO_PATHCONV=1`. Docker Desktop é pré-requisito dos experimentos e deve ser instalado cedo.
20. **Pasta de trabalho.** O agente roda na raiz do repositório (`expensehub-api`), não em `sources/`. Todos os comandos deste projeto usam caminhos relativos à raiz.

## Seções por issue

Cada seção abaixo é preenchida pelo agente ao terminar a issue. A lista de unidades da rubrica serve de checklist: marcar só o que está provado por teste, execução ou evidência.

### CP3-00: Preparação

**Estado:** pendente. **Branch:** `cp3-00-preparacao`.

- [ ] Pasta `andamento/` com este arquivo e os dois HTML (copiados pelo Renan).
- [ ] `CLAUDE.md` do CP3 no lugar (copiado pelo Renan).
- [ ] Issues #11 a #18 salvas em `andamento/requisitos/issues-cp3.md` (copiadas pelo Renan).
- [ ] README reorganizado e `CHECKPOINT-2.md` e `CHECKPOINT-3.md` criados, sem perda de conteúdo do CP2.
- [ ] Baseline registrado (build, testes, score).

### CP3-I01: Contrato e resultado do relatório (20)

**Estado:** pendente. **Branch:** `cp3-i01`. **Issue:** #11.

- [ ] Entrada, período e solicitação persistida conforme o contrato (4)
- [ ] Listagem, detalhe e estados observáveis (4)
- [ ] Resumo por estado e totais corretos, inclusive conjunto vazio (8)
- [ ] Resultado e instante de geração salvos; consulta sem recálculo (2)
- [ ] Excel válido, fiel ao resultado salvo e com o contrato de download (2)

### CP3-I02: Autorização e isolamento (15)

**Estado:** pendente. **Branch:** `cp3-i02`. **Issue:** #12.

- [ ] Autenticação e permissão por perfil e escopo (5)
- [ ] Listagem, detalhe e Excel isolados por solicitante, inclusive Auditor (5)
- [ ] Solicitante e escopo controlados pelo servidor até o processamento (5)

### CP3-I03: RabbitMQ, worker e idempotência (20)

**Estado:** pendente. **Branch:** `cp3-i03`. **Issue:** #13.

- [ ] Publicação confirmada, fila durável e mensagens persistentes (5)
- [ ] Worker processa; consumo confirmado só depois de salvar (5)
- [ ] Conclusão e resultado consistentes; falha definitiva registrada (5)
- [ ] Duplicata reconhecida sem novo efeito, com proteção persistente (5)

### CP3-I04: Testes próprios significativos (15)

**Estado:** pendente. **Branch:** `cp3-i04`. **Issue:** #14.

- [ ] Cálculo, conjunto vazio e validação de período (5)
- [ ] Negação por perfil e escopo; isolamento por solicitante (5)
- [ ] Duplicata, resultado persistido e Excel, com asserções de comportamento (5)

### CP3-I05: Publicação e execução reproduzível (15)

**Estado:** pendente. **Branch:** `cp3-i05`. **Issue:** #15.

- [ ] Duas imagens publicadas e identificadas pelo commit, com testes antes da publicação (5)
- [ ] Compose executa as imagens publicadas e o RabbitMQ com configuração documentada (5)
- [ ] Banco e armazenamento compatíveis, inicialização reproduzível e segredos externos (5)

### CP3-I06: Experimentos e diagnóstico (10)

**Estado:** pendente. **Branch:** `cp3-i06`. **Issue:** #16.

- [ ] E01 com Excel e E02 correlacionados na mesma solicitação (3)
- [ ] E03 duplicata depois de reiniciar o worker, sem novo efeito (2)
- [ ] E04 cenários de autorização reproduzíveis (2)
- [ ] E05 persistência depois de recriar web e worker (2)
- [ ] Diagnóstico correto de aceitação, publicação e janela sem outbox (1)

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
- [ ] Texto de entrega para o Teams: URL do repositório, SHA do CP2 (completo), SHA final do CP3, referências das duas imagens, comando de acesso, comando dos testes.
- [ ] LinkedIn (opcional, manual, texto próprio): publicar e enviar o link individualmente no Teams.
