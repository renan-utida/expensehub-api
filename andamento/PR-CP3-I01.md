# CP3-I01: Contrato e resultado do relatório

## Issue implementada

Racass/checkpoint-csharpracass-expensehub#11 (CP3-I01, 20 pontos). A PR entrega o que não depende do RabbitMQ: as quatro rotas de relatório, a solicitação e o resultado salvos, o cálculo isolado e testado, o Excel e a abstração do publicador. O fluxo real até `Completed` e o `202` com o broker são da CP3-I03; a regra de escopo por papel é da CP3-I02.

## Resumo técnico

- **Rotas** (`ReportsController`, todas autenticadas e limitadas ao próprio usuário): `POST /api/reports`, `GET /api/reports`, `GET /api/reports/{id}` e `GET /api/reports/{id}/excel`, com o envelope do contrato (`id`, `scope`, `fromDate`, `toDate`, `status`, `createdAt`, `startedAt`, `completedAt`, `result`, `error`; instantes em UTC terminando em `Z`).
- **Entrada** (`CreateReportRequest`): `DateOnly?` obrigatórias e `scope` como texto obrigatório. `scope` só vale como o nome exato `Mine` ou `Global`; número, `"0"`, outra caixa e vazio dão `400`. `ownerId` e `requesterId` no corpo são ignorados: o solicitante vem do token.
- **Cálculo** (`ReportCalculator`, classe estática pura): `Select` é a regra de quais despesas entram (período inclusivo por `ExpenseDate`, `Mine` por dono, `Global` sem filtro de dono), escrita como consulta para o SQLite aplicar antes de carregar qualquer despesa; `Summarize` soma em centavos inteiros, devolve sempre os cinco estados e só converte para `decimal` na saída. É somente leitura.
- **Persistência**: entidade `ReportRequest` (tabela `ReportRequests`, `Guid`, estado como *concurrency token*, instantes com `UtcDateTimeOffsetConverter`, três `CHECK`), migration `AddReportRequests` e `ReportRepository`. O resultado fica na coluna `ResultJson` (centavos inteiros e instante de geração), lida e escrita só por `ReportResultSerializer`, que recusa resultado incoerente.
- **Serviço** (`ReportService`): valida e só então salva e publica; sem confirmação do broker o pedido fica `Pending` e a resposta é `503` com o `id`; banco indisponível dá `503` sem `id`; consultar nunca recalcula nem lê despesas.
- **Publicador**: `IReportPublisher` devolve confirmado ou não confirmado. Sem publicador real, `UnavailableReportPublisher` nunca confirma, então o `POST` responde `503` e nunca finge `202`. A CP3-I03 o substitui.
- **Excel** (`ReportExcelWriter`, pacote novo `DocumentFormat.OpenXml` 3.5.1, MIT, com `DocumentFormat.OpenXml.Framework` e `System.IO.Packaging`, também MIT): montado em memória a partir do resultado salvo; quantidades e valores em células numéricas.
- **`.http`**: `sources/ExpenseHub.Api/ExpenseHub.Api.http` com login, solicitar, listar, consultar e baixar, sem credenciais (ficam em `http-client.env.json.user`, que o git ignora). `http-client.env.json` só tem o endereço do host.
- **Documentação**: README (rotas, estrutura, biblioteca do Excel, testes, qualidade), seção da CP3-I01 no `CHECKPOINT-3.md` e `andamento/PROGRESSO.md`.

## Decisões e concessões

- O resultado fica em uma coluna JSON da própria linha, e não em uma tabela filha, para que `Completed` e o resultado nasçam numa única gravação (CP3-I03).
- A produção lê só `Status` e `Amount` (projeção estreita) e soma em `Summarize`. O agrupamento no banco também traduz e dá valores certos, mas não foi adotado: assim o caminho em produção é o mesmo que os testes unitários cobrem.
- Banco indisponível ao registrar responde `503` sem `id`, e publicação não confirmada responde `503` com `id`; o estado nunca vira `Failed` por falha de publicação.
- Nesta PR só autenticação e dono são exigidos. Até a CP3-I02, qualquer usuário autenticado pode pedir o escopo `Global`. **Esta PR deve entrar na `main` junto com a CP3-I02, em sequência**, e não ficar como estado final.
- Quem já tem um banco precisa rodar `dotnet ef database update` para criar `ReportRequests`.

## Como validar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

Para ver as rotas, abra `sources/ExpenseHub.Api/ExpenseHub.Api.http` com um usuário `Employee` (credenciais no arquivo local `http-client.env.json.user`). Com a aplicação rodando, `POST /api/reports` devolve `503` com o `id` do pedido, que aparece `Pending` na listagem e no detalhe.

## Evidências relevantes

- Build com 0 avisos e 0 erros. `dotnet test`: **788 aprovados** (611 do CP2 e 177 novos), 0 falhas.
- Testes novos: calculadora 24, resultado salvo 20, regras 25, corpo da requisição 22, solicitação 20, consulta 11, Excel pelo serviço 11, escritor do Excel 9, envelope 11, controller 16, autenticação das rotas 3 e publicador 5. Não usam banco, rede nem RabbitMQ.
- SQLite real, em um experimento descartável fora do repositório: as migrations criam `ReportRequests` com os três `CHECK`; com 11 despesas semeadas (exemplo da apostila e limites), `Mine` dá 6 e 235,00 e `Global` dá 9 e 2.147.484.082,01, iguais a um oráculo independente; o SQL tem o `WHERE` do período e do dono com projeção de `Status` e `AmountCents`; nada fica rastreado e a tabela de despesas não muda; os `CHECK` rejeitam `Completed` sem resultado, `Failed` sem código e período invertido; o estado é *concurrency token*.
- HTTP real contra a aplicação (duas contas `Employee`, banco temporário): `401` sem token; 11 corpos inválidos dão `400` e 0 linhas gravadas; `POST` válido dá `503` com o `id` e o pedido fica `Pending`; `requesterId`, `ownerId` e `status` no corpo são ignorados; o outro usuário recebe lista vazia e `404`; com o relatório gravado como `Completed`, o detalhe traz o resultado e o Excel dá `200` com `Content-Type` do xlsx e `attachment; filename=report-{id}.xlsx`, ZIP e esquema válidos; `Failed` e `Pending` dão `409`. O log tem o `reportId` e não tem senha nem token.
- Script de qualidade (`-SkipGitleaks`): **100/100**, 20 em cada categoria, sem bloqueantes. Achado informativo `FIAP0002` (Gitleaks ignorado; roda no CI). Um primeiro passe deu 99 por `IDE1006` em campos `private static readonly` sem o prefixo `_`, corrigido.

## Impactos em segurança e autorização

- Listagem, detalhe e Excel só devolvem relatórios do próprio usuário, e o relatório de outro dá `404`, igual ao inexistente, também para o `Auditor`.
- O solicitante e o estado vêm sempre do token e do servidor; o corpo não tem campo para eles.
- Ainda **não** há regra de escopo por papel: até a CP3-I02, um usuário autenticado sem papel `Auditor` consegue pedir `Global`. Está registrado como pendência da CP3-I02.
- Nenhuma credencial foi versionada; o `.http` lê as credenciais de um arquivo local ignorado pelo git. Nenhum comportamento do CP2 (autenticação, autorização, pagamento) foi alterado.

## Checklist

- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
