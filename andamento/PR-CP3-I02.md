# CP3-I02: Autorização e isolamento

## Issue implementada

Racass/checkpoint-csharpracass-expensehub#12 (CP3-I02, 15 pontos). A PR fecha a regra de quem pode pedir cada escopo de relatório, fixa a ordem das respostas do `POST /api/reports` e prova o isolamento de listagem, detalhe e Excel por solicitante, inclusive para o Auditor. O publicador real, o worker e o `202` de verdade continuam sendo da CP3-I03. Esta PR deve entrar logo depois da CP3-I01 (já na `main`), porque até ela a `main` deixa qualquer usuário autenticado pedir `Global`.

## Resumo técnico

- **Matriz como classe pura** (`ReportAccess`, sem EF nem web): `Mine` exige `Employee`, `Global` exige `Auditor`, quem tem as duas roles pede os dois, e Admin, Approver e Finance sozinhos não pedem nada. As roles acumulam e o nome é comparado exatamente. A constante `AppRoles.ReportRequesters` alimenta o atributo da rota.
- **Ordem das respostas do `POST /api/reports`**, a mesma do CP2: `401` sem token; `403` quando o usuário não tem `Employee` nem `Auditor` (atributo `[Authorize(Roles = "Employee,Auditor")]`, que roda no middleware antes de o corpo ser lido, então vem antes de qualquer `400`); `400` para dados inválidos; `403` quando a role não permite o escopo pedido; depois a gravação e a publicação (`202` ou `503` com o `id`). O serviço repete a mesma sequência e não depende só do atributo. Nos dois `403` nada é gravado nem publicado.
- **Resultado `Forbidden`** em `ReportOperationResult`, mapeado no controller para `403` com `ProblemDetails` sem `id`. O log da recusa leva só o escopo pedido.
- **Leitura sem role**: `GET /api/reports`, `GET /api/reports/{id}` e `GET /api/reports/{id}/excel` seguem exigindo só autenticação e posse, e o filtro por solicitante já está na consulta ao banco. Relatório alheio e inexistente dão o mesmo `404`, também para o Auditor.
- **Junção com o worker**: `ReportQuery.From(ReportRequest)` monta a consulta só com o solicitante, o escopo e o período do pedido gravado. O worker (CP3-I03) não relê as roles e não confia na mensagem, que leva só o `reportId` (um teste fixa que o `IReportPublisher` recebe apenas o `Guid`).
- **`.http`**: login do Auditor, `Mine` com o Employee, `Global` com o Auditor (com a nota de que `Global` exige a role Auditor), os dois `403` e o `404` do relatório do Auditor lido pelo Employee. As credenciais continuam num arquivo local ignorado pelo git.
- **Documentação**: README (linha do `POST`, seção "Autorização dos relatórios", estrutura, testes e qualidade), seção da CP3-I02 e quatro decisões novas no `CHECKPOINT-3.md`, e I01 marcada como concluída (PR #12, merge `48810e3`, workflow `code-quality` 100/100 na execução #69).

## Decisões e concessões

- A regra de escopo mora no serviço, junto com a validação, e o atributo da rota só barra quem não tem nenhuma role que possa pedir. Assim a ordem do CP2 (role, validação, depois a regra do recurso) fica igual e testável sem host.
- Um só resultado `Forbidden` para role e escopo: os dois são `403` e o cliente não precisa distinguir.
- Os testes de `ReportServiceRequestTests` da CP3-I01 pediam relatório com um usuário sem role. Passaram a usar um `Employee` (e Employee com Auditor onde o escopo é `Global`), porque a regra mudou. Nenhuma asserção foi enfraquecida.
- Nada muda no CP2: autenticação, autorização das despesas e pagamento seguem como estavam. Nenhum pacote novo e nenhuma migration nova.

## Como validar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

Para ver as rotas, abra `sources/ExpenseHub.Api/ExpenseHub.Api.http` com um usuário `Employee` e um `Auditor` (credenciais no arquivo local `http-client.env.json.user`). Com a aplicação rodando e sem o publicador da CP3-I03, um `POST` autorizado e válido devolve `503` com o `id` do pedido, que fica `Pending`; isso é o esperado, não um defeito.

## Evidências relevantes

- Build com 0 avisos e 0 erros. `dotnet test`: **936 aprovados** (788 da base e 148 novos), 0 falhas.
- Testes novos: matriz de role por escopo 59 (tabela escrita à mão das 32 combinações das cinco roles, mais a conferência de que nenhuma combinação foi esquecida), autorização no serviço 35, isolamento por role 36, consulta montada do pedido gravado 10, atributos das rotas 6 e controller 2. Não usam banco, rede nem RabbitMQ.
- Mutações feitas e desfeitas: trocar `Employee` e `Auditor` na regra quebrou 42 testes; tirar o atributo do `POST` quebrou 2.
- HTTP real contra a aplicação e um SQLite temporário (senhas geradas na hora, nunca impressas), com o Admin do seed, duas Employee, um Auditor, uma conta Employee e Auditor, um Approver, um Finance e uma conta sem role: 120 conferências aprovadas.
  - Sem token: `401` nas quatro rotas e 0 linhas gravadas.
  - Matriz de 7 contas contra `Mine` e `Global`: Employee `Mine` `503` com `id` e `Global` `403`; Auditor `Mine` `403` e `Global` `503` com `id`; Employee e Auditor `503` nos dois; Admin, Approver, Finance e sem role `403` nos dois. Cada `503` gravou o `RequesterId` do token, o `Scope` pedido e `Pending`, mesmo com `requesterId`, `ownerId` e `status` no corpo; cada `403` deixou a tabela como estava.
  - Ordem: sem role, `403` com corpo vazio, período invertido, `scope` numérico ou texto que não é JSON; Employee com período invertido e `Global`: `400`; Auditor com período invertido e `Mine`: `400`; só com dados válidos o escopo não permitido dá `403`.
  - Isolamento: com relatórios `Completed` da Employee (`Mine`) e do Auditor (`Global`), o dono recebe `200` no detalhe e no Excel; as outras sete contas, inclusive o Auditor e a conta com as duas roles, recebem `404`, igual a um id inexistente. Relatório `Pending` alheio dá `404`, nunca `409`.
  - Log sem senha, token nem e-mail.
- Script de qualidade (`-SkipGitleaks`): **100/100**, 20 em cada categoria, sem bloqueantes. Achado informativo `FIAP0002` (Gitleaks ignorado; roda no CI).

## Impactos em segurança e autorização

- Antes desta PR, qualquer usuário autenticado podia pedir `Global`. Agora `Global` exige `Auditor` e `Mine` exige `Employee`; quem não tem nenhuma das duas roles recebe `403` antes de o corpo ser lido.
- Ninguém lê relatório de outra pessoa: o filtro por solicitante está na consulta ao banco, e o `404` é o mesmo do relatório inexistente. O privilégio do Auditor é sobre os dados do próprio relatório.
- O solicitante vem do token; `ownerId`, `requesterId` e `status` do corpo são ignorados. Solicitante e escopo ficam gravados no pedido e são o que o worker vai usar.
- Nenhuma credencial foi versionada. Nenhum comportamento do CP2 foi alterado.

## Checklist

- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
