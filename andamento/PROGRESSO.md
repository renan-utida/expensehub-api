# PROGRESSO

Registro temporário do andamento por issue. Esta pasta será removida antes do SHA final. Leia este arquivo antes de começar uma issue e atualize-o ao terminar (junto com a linha "Estado atual", a tabela de status, a tabela de endpoints e a seção da issue no README).

## Prazo e decisões do professor

- **Prazo:** quarta-feira, 14/10/2026, às 23:59, no Teams (confirmado pelo professor; o ENUNCIADO e o README dele ainda dizem 13/10). Vale planejar para fechar antes e deixar folga para o score, o README e o SHA final.
- **ExpenseCategory:** nenhuma issue cobra categoria. Fica só a entidade mínima (`Id` e `Name`), sem endpoint, seed nem vínculo com `Expense`.
- **Testes unitários:** sem EF Core InMemory e sem SQLite em memória. O acesso a dados é simulado por interface de repositório injetada, com fakes ou mocks. O banco em si seria coberto por testes funcionais, que são opcionais e não pontuam.
- **Códigos de status:** o professor pediu que a equipe decida pelo significado de cada código (401 é autenticação, 403 é autorização, 404 é recurso inexistente, 409 é conflito de estado). Respostas dele no Teams, já tomadas e que a I07 aplica: em toda ação que escreve (editar, enviar, aprovar, reprovar e pagar), uma despesa que existe em outro estado dá 409, inclusive `Draft`; o 404 é só para despesa inexistente (URL incorreta); despesa de outro dono dá 403, inclusive quando um Employee edita ou envia o `Draft` de outro Employee ("o erro é de auth/authz, não de not found"). A leitura (listagem, detalhe e, na I08, histórico) continua com 404 fora do escopo de leitura. Ordem das ações de escrita: role pelo atributo (403), corpo (400), despesa inexistente (404), regra de dono (403), estado (409). Os textos do `CLAUDE.md` e do README são atualizados na I07.
- **Histórico (resposta do professor no Teams, aplicada na I08):** o histórico segue a visibilidade da despesa ("quem vai ler elas vai ser o auditor"). Fora do escopo de leitura é 404, então o Approver deixa de ler o histórico de uma despesa assim que ela sai de `Submitted`, inclusive a que ele acabou de decidir, e o Finance só lê o de `Approved` e `Paid`.

## Divisão e marcos

- **Responsáveis previstos** (tabela do README): Renan fica com a I01 e da I07 à I10; Pedro fica com da I02 à I06.
- **Ponto de controle superado:** o Pedro terminou da I02 à I06 no sábado, 03/10, antes do ponto de controle de domingo, 04/10. As PRs #2 a #6 estão mescladas. O Renan segue com da I07 à I10.
- **Reta final:** funcionalidades prontas até domingo, 11/10; segunda e terça (12 e 13/10) para score, README e SHA final; prazo de entrega quarta, 14/10, às 23:59, no Teams.
- Quem sentir aperto avisa cedo. Cada issue mergeada na `main` libera a seguinte.

## Avisos para a I02 a I06

Estes pontos vêm da I01 e evitam retrabalho:

- (a) **I02, Identity:** ao herdar de `IdentityDbContext`, chame `base.OnModelCreating(modelBuilder)` antes das `ApplyConfiguration`. Hoje `ExpenseHubDbContext` herda de `DbContext` e não chama a base.
- (b) **I04, DTO de despesa:** rejeitar valores com mais de duas casas decimais. `MoneyConversion.ToCents` arredonda meio centavo para longe do zero, mas isso é só defesa; o conversor nunca deve arredondar entrada de usuário.
- (c) **I04:** as interfaces de repositório (por exemplo `IExpenseRepository`) nascem na I04. Serviços nunca recebem `DbContext`.
- (d) **Rotas:** `Expense.Id` é `Guid`. Use `{id:guid}` nas rotas de `/api/expenses/{id}`.
- (e) **Instantes:** qualquer novo `DateTimeOffset` persistido precisa de `UtcDateTimeOffsetConverter` (ticks UTC em `INTEGER`), senão `OrderBy`, `Max` e `Where` falham no SQLite. Atenção na I02: `IdentityUser.LockoutEnd` é `DateTimeOffset?`; só precisa de conversor se alguma consulta ordenar ou filtrar por ele.
- (f) **Testes unitários:** sem banco, sem EF Core InMemory e sem SQLite em memória. Use fakes escritos à mão dos repositórios.
- (g) **Commits:** em inglês, no padrão convencional, com o identificador da issue entre parênteses no fim da primeira linha, por exemplo `feat(identity): add login endpoint (I02)`. Não use `#2` no texto, porque o GitHub liga a um item do repositório da equipe. A frase "Commit messages in English" já está no `CLAUDE.md` (acrescentada na I02).
- (h) **Merge da PR:** escolha "Create a merge commit". Não use Squash nem Rebase, porque o professor avalia os commits de cada integrante. Mantenha as branches das issues depois do merge.

## I01: Fundação da solução e Entity Framework Core

**Status:** concluída e mergeada na `main` pela PR #1 (pipeline oficial 100/100, com Gitleaks).

**Branch:** `i01-foundation-ef`.

### O que foi feito

- Provider SQLite: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 e `Microsoft.EntityFrameworkCore.Design` 10.0.12 (`PrivateAssets=all`) em `ExpenseHub.Api`; `dotnet-ef` 10.0.12 como ferramenta local em `dotnet-tools.json`.
- Entidades `Expense`, `ExpenseCategory`, `ExpenseHistory` e `PaymentRecord` e enums `ExpenseStatus` e `ExpenseHistoryAction` (só dados, sem regra de negócio).
- `ExpenseHubDbContext`, quatro `IEntityTypeConfiguration`, `ExpenseHubDbContextFactory` (uso do `dotnet ef`) e `AddExpenseHubPersistence`, chamada em `Program.cs` com `builder.Environment.ContentRootPath`.
- Conversões puras `MoneyConversion` (decimal e centavos) e `UtcTicks` (instante e ticks UTC), com cascas finas de `ValueConverter`; 13 testes unitários que chamam só as funções estáticas.
- Migration `InitialCreate` em `Persistence/Migrations/`.
- `appsettings.json` com `ConnectionStrings:ExpenseHub = "Data Source=expensehub.db"`; `.gitignore` com `*.db`, `*.db-shm`, `*.db-wal` e `*.sqlite*`.
- README novo na raiz; o README original do professor foi movido para esta pasta.
- Regras novas no `CLAUDE.md` (leitura e atualização deste arquivo; dinheiro em centavos).
- Commits com `(I01)` no fim da primeira linha, reunidos na PR #1 do repositório da equipe (o último preenche o número da PR e registra o score).

### Decisões que afetam as próximas issues

- `Expense.Id` é `Guid`; `ExpenseHistory.Id` e `PaymentRecord.Id` são inteiros autoincrementados.
- `Expense` tem só `CreatedAtUtc` como horário. `UpdatedAtUtc` foi removido (não está em `docs/REQUISITOS.md` e o histórico já registra cada mudança).
- `OwnerId` e `ActorId` são texto com até 450 caracteres, sem FK até o Identity (I02). A FK pode ser adicionada por migration na I02.
- `MaxLength` (descrição 500, justificativa 500, alterações 2000, categoria 100) existe só no modelo do EF: o SQLite não aplica. A validação real é dos DTOs.
- `ExpenseHistory.PreviousStatus` é nulo na criação (o estado anterior é "inexistente"). `Changes` guarda o resumo das edições em Draft (texto, até 2000 caracteres).
- `Expenses.AmountCents` tem `CHECK` de 1 a 214748364700. O DTO da I04 continua sendo a primeira barreira; o `CHECK` é a segunda.
- `PaymentRecords.ExpenseId` tem índice único: um pagamento por despesa. Pagamento repetido continua sendo 409 na regra de negócio.
- Chaves estrangeiras de histórico e pagamento usam `RESTRICT` (não há exclusão de despesa).
- Enums são gravados como texto.
- A aplicação não aplica migrations na inicialização: só `dotnet ef database update` cria ou atualiza o banco.
- `ExpenseHubDbContextFactory` lê `appsettings.json` e variáveis de ambiente a partir do diretório atual (o `dotnet ef` usa a pasta do projeto). Ela não carrega user-secrets; hoje só precisa da connection string.
- Entidades e `DbContext` são `public` com `<summary>` em inglês, para zerar CS1591 sem supressão.

### O que foi medido (EF Core 10.0.12, SQLite)

- `DateTimeOffset` sem conversor: `OrderBy` e `Max` lançam `NotSupportedException`; `Where` com `>=` lança `InvalidOperationException`. Por isso ticks UTC.
- `decimal` sem conversor funciona no EF 10 (`OrderBy` com `COLLATE EF_DECIMAL`, `Where`, `Sum`, `Max`, `Average`). A escolha por centavos é para ter `CHECK` numérico e ordenação nativa independente da collation do EF; a afirmação inicial do plano de que o SQLite não ordena decimal estava errada e não foi para o README.
- `Sum` sobre `Amount` com o conversor retornou o total correto.
- `dotnet ef migrations script --idempotent` não é suportado no SQLite.
- Com a migration aplicada em um banco temporário: `OrderBy` e `Where` em `Amount` e em instantes (offsets +0, +3, -5) corretos; `CHECK` rejeitou 0,00 e valor acima do teto; índice único rejeitou segundo pagamento; FK `RESTRICT` bloqueou exclusão de despesa com histórico.

### Desvios do plano

- O SDK 10 criou o manifesto em `dotnet-tools.json` na raiz, e não em `.config/dotnet-tools.json`. `dotnet tool list --local` reconhece, e o `dotnet tool restore` também procura na raiz.
- `--idempotent` foi trocado por um `migrations script` comum, porque o provider não suporta.

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 13 aprovados.
- `dotnet run` e `GET /health`: HTTP 200 `{"status":"ok"}`, sem criar o `.db` (conferido com `find` no repositório).
- `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks`, antes dos commits: 100/100, 20 em cada categoria, sem bloqueantes; único finding é o informativo FIAP0002 (Gitleaks ignorado).
- O mesmo script, repetido depois dos 10 commits (com os arquivos já rastreados): 100/100, árvore de trabalho limpa.
- Pipeline oficial na PR #1 (workflow `code-quality`, execução #5, evento pull_request): 100/100, 20 em cada categoria, sem bloqueantes e sem achados, com Gitleaks 8.30.1 e dotnet 10.0.401.

### Observações do pipeline

- O job traz uma anotação informativa: o rótulo `ubuntu-latest` passa a apontar para o Ubuntu 26 a partir de 19/10/2026, depois do prazo. Não editar o workflow.
- O relatório do professor sai com o campo "Commit" vazio e com linhas de "Projetos" mal formatadas (`$(@{path=...})`). É um defeito do script dele e não afeta a pontuação. Não editar `scripts/`.

### Pendências

- **Remover `andamento/`** antes do SHA final. O README não cita nem linka esta pasta; o `CLAUDE.md` cita, e a regra correspondente deve ser removida junto.
- **Feitos:** número da PR preenchido no README; Gitleaks conferido no CI; merge da PR #1 na `main` com "Create a merge commit".

### Como validar

```shell
unset ConnectionStrings__ExpenseHub        # PowerShell: Remove-Item Env:ConnectionStrings__ExpenseHub
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx --no-incremental   # 0 avisos, 0 erros
dotnet test ./sources/ExpenseHub.slnx                     # 13 aprovados
dotnet tool restore
dotnet ef migrations script --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
```

Para aplicar a migration sem tocar no banco do repositório, use um arquivo temporário fora dele:

```shell
export ConnectionStrings__ExpenseHub="Data Source=/tmp/expensehub-teste.db"
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
unset ConnectionStrings__ExpenseHub
```

Depois, `dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj` e `curl http://localhost:5245/health`, e `git status` sem nenhum `.db`.

### Modelo de PR (texto da PR #1)

Este é o texto da PR #1, com o checklist como ficou no fim (todas as caixas marcadas, inclusive "Pipeline analisado", que foi marcada depois de ler o resultado do workflow). Use-o como modelo nas próximas PRs, trocando o conteúdo pelo da sua issue. Ao abrir a sua PR, comece com as caixas desmarcadas e marque cada uma só depois de cumprir o item.

Título: `I01: Fundação da solução e Entity Framework Core`

```text
Implementa a fundação de persistência do ExpenseHub.

Issue: Racass/checkpoint-csharpracass-expensehub#1

## Resumo técnico
- SQLite com EF Core 10.0.12; ferramenta dotnet-ef local em dotnet-tools.json.
- Entidades mínimas (Expense, ExpenseCategory, ExpenseHistory, PaymentRecord), DbContext, mapeamentos e migration InitialCreate.
- Valor em centavos (INTEGER com CHECK de faixa) e instantes em ticks UTC (INTEGER), porque DateTimeOffset falha em OrderBy, Max e Where no SQLite.
- Banco criado só por dotnet ef database update; nada acessa o banco no build, nos testes nem na inicialização.

## Decisões e concessões
- Expense.Id é Guid; OwnerId e ActorId são texto sem FK até o Identity (I02).
- UpdatedAtUtc removido: não está nos requisitos e o histórico já registra as mudanças.
- ExpenseCategory mínima (Id e Name), sem endpoint nem vínculo.

## Como validar
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet tool restore
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api

## Evidências
- Build com 0 avisos e 0 erros; 13 testes unitários aprovados.
- Score local do pipeline de qualidade: 100/100 (com -SkipGitleaks), medido também depois dos commits.
- Pipeline code-quality na PR: 100/100, com Gitleaks 8.30.1.
- Experimento descartável com a migration aplicada: OrderBy e Where em Amount e em instantes com offsets diferentes corretos; CHECK, índice único de pagamento e FK RESTRICT rejeitaram dados inválidos.

## Impacto em segurança e autorização
- Nenhum endpoint novo e nenhuma credencial versionada; a connection string é só um caminho de arquivo.
- .db, bin e obj ignorados pelo Git.

## Checklist
- [x] Critérios de aceite atendidos
- [x] Casos negativos validados
- [x] Autorização revisada (não se aplica: sem endpoints)
- [x] Testes unitários adicionados
- [x] Build sem erros
- [x] Pipeline analisado
- [x] Documentação atualizada
```

## I02: Identity, Admin e autenticação

**Status:** concluída e mergeada na `main` pela PR #2 (pipeline oficial 100/100, execução #14, com Gitleaks 8.30.1).

**Branch:** `i02-identity-auth`.

### O que foi feito

- Pacote `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.12 (aprovado pelo Pedro) e `UserSecretsId` no `.csproj`.
- `ExpenseHubDbContext` herda de `IdentityDbContext<IdentityUser>`; `base.OnModelCreating` vem antes dos `ApplyConfiguration` (aviso (a)). O parâmetro se chama `builder`, por causa do CA1725.
- Migration `AddIdentity` (só tabelas `AspNet*`; as tabelas da I01 não mudam).
- `AddExpenseHubIdentity()`: bearer nativo (`IdentityConstants.BearerScheme`), autorização, `AddIdentityCore` com e-mail único, roles, stores do EF e `SignInManager`. Sem `MapIdentityApi`.
- `POST /login` (`AuthController`, DTO `LoginRequest` validado): e-mail inexistente, senha errada e conta bloqueada dão o mesmo `401`; `lockoutOnFailure` ligado.
- `GET /api/admin/users` (`AdminUsersController`, `[Authorize(Roles = "Admin")]`), devolve só `id` e `email`. A listagem e a administração completas são da I03.
- `AddProblemDetails` e `UseStatusCodePages`: `401` e `403` como `ProblemDetails`.
- Seed: `AppRoles`, `AdminSeedOptions` (`Seed:Admin`), `IdentitySeeder` (regra), `IIdentitySeedStore` e `IdentitySeedStore` (acesso), `IdentitySeedHostedService` (roda na inicialização). Registro em `AddExpenseHubIdentitySeed`.
- 25 testes unitários novos (14 do seeder, 11 do `LoginRequest`), com `FakeIdentitySeedStore` escrito à mão. Total: 38.
- Frase "Commit messages in English" acrescentada ao `CLAUDE.md` (aviso (g)).

### Decisões que afetam as próximas issues

- **Senha do Admin:** só de user-secrets ou de `Seed__Admin__Password`. O e-mail (`admin@expensehub.local`) fica no `appsettings.json`, porque não é segredo.
- **Sem senha, a aplicação não inicia** (decisão do Pedro), e nada é gravado. A senha é exigida em toda inicialização, inclusive com o Admin já criado. Senha que viola a política do Identity também impede o início, e a mensagem mostra só os códigos das regras.
- **Um Admin só:** o seed cria o Admin apenas se nenhum usuário estiver na role `Admin`. O seed não altera um Admin existente (trocar a senha no user-secrets não muda a senha de um banco já semeado).
- **I03:** o cadastro deve usar o e-mail também como `UserName`, como o seed e o login assumem (o login busca por `FindByEmailAsync`). Nunca aceitar role no `/register`.
- **I03:** `AppRoles` já tem as cinco roles; usar as constantes em `[Authorize(Roles = ...)]` e na validação de "roles conhecidas". `AdminUsersController` já existe e deve crescer na I03 (listagem e `PUT /api/admin/users/{id}/roles`).
- **I03:** "após uma alteração de role, o usuário deve autenticar novamente". O token do bearer nativo carrega as roles do momento do login; a I03 precisa decidir como invalidar (por exemplo atualizar o `SecurityStamp` e validar no token).
- **I04 em diante:** o `UserManager` e o `SignInManager` são do Identity e podem ser injetados em controllers; os serviços de despesa continuam dependendo só de repositórios. O dono da despesa vem de `ClaimTypes.NameIdentifier` do token (o `Id` do `IdentityUser`).
- **Ordem do pipeline:** `UseStatusCodePages`, `UseAuthentication`, `UseAuthorization`, `MapControllers`.

### O que foi medido (EF Core 10.0.12, SQLite, banco temporário fora do repositório)

- Sem token: `401` `ProblemDetails` com `WWW-Authenticate: Bearer`. Token inválido: `401`. Usuário sem role com token válido: `403`. Admin: `200` com a lista.
- Credencial inexistente e senha errada: `401` sem token. Corpo vazio, e-mail malformado e senha vazia: `400` com erros por campo.
- Seed com senha válida: 5 roles, 1 usuário, 1 vínculo. Reiniciar duas vezes: continua 5, 1, 1.
- Sem senha: a aplicação não sobe e o banco fica com 0 roles e 0 usuários. Senha fraca: não sobe, 5 roles e 0 usuários.
- Busca da senha de teste e do token no log da aplicação: 0 ocorrências.
- Mutação no seeder (sempre criar o Admin): 2 testes falharam (`RunTwice` e `AdminAlreadyExists`); arquivo restaurado, 38 aprovados.
- Para testar o `403` sem `/register`, os usuários descartáveis foram criados por um programa auxiliar fora do repositório, em um banco temporário. Nada disso entrou no código.

### Desvios e cuidados

- O Windows PowerShell 5.1 estraga as aspas do JSON no `curl.exe` (`400` com `is an invalid start of a property name`). Não é defeito da API. Use `Invoke-RestMethod` ou o PowerShell 7.
- Rodar o executável a partir da raiz do repositório faz a aplicação não achar o `appsettings.json` (o content root é o diretório atual). Use `dotnet run --project ...` ou rode a partir de `sources/ExpenseHub.Api`.
- `SignInResult` é ambíguo entre `Microsoft.AspNetCore.Identity` e `Microsoft.AspNetCore.Mvc` no controller; resolvido com alias.

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 38 aprovados.
- Os commits da I02 têm `(I02)` no fim da primeira linha, em inglês.

### Pendências

- **Feitos:** PR #2 aberta e mesclada com "Create a merge commit"; score oficial 100/100 (execução #14, com Gitleaks 8.30.1); status "Concluída" no README.
- **Execução vermelha #12 (já explicada):** na branch `i02-identity-auth`, a regra `FIAP1002` achou valores literais atribuídos a campos sensíveis em arquivos de teste (introduzidos no commit `8b0278f`), e o score foi 9/100 pelo teto "secret-detected". O commit `aac8de8` removeu os literais e as execuções seguintes passaram. Ver "Lições da equipe".
- **Bloqueio por tentativas (`lockoutOnFailure`):** ligado no código, mas não foi testado à mão com várias senhas erradas.

### Como validar

```powershell
dotnet build ./sources/ExpenseHub.slnx --no-incremental   # 0 avisos, 0 erros
dotnet test ./sources/ExpenseHub.slnx                     # 38 aprovados
$env:ConnectionStrings__ExpenseHub = "Data Source=$env:TEMP\expensehub-teste.db"
$env:Seed__Admin__Password = "<senha do Admin>"
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Depois: `POST /login` com o Admin, `GET /api/admin/users` sem token (`401`) e com o token (`200`), e reiniciar a aplicação duas vezes conferindo 5 roles e 1 Admin no banco. Limpe as variáveis ao terminar.

### Modelo de PR

Título: `I02: Identity, Admin e autenticação`

```text
Implementa ASP.NET Core Identity, o login com token bearer, as roles obrigatórias e a conta Admin inicial.

Issue: Racass/checkpoint-csharpracass-expensehub#2

## Resumo técnico
- Identity com EF Core e SQLite; migration AddIdentity só com as tabelas do Identity.
- Bearer nativo do Identity; POST /login devolve o token. E-mail inexistente, senha errada e conta bloqueada respondem o mesmo 401.
- Seed idempotente das cinco roles e de um único Admin; a senha vem só de user-secrets ou de variável de ambiente. Sem a senha a aplicação não inicia e nada é gravado.
- GET /api/admin/users restrito a Admin, para provar 401 (sem token) e 403 (sem a role); respostas em ProblemDetails.
- 25 testes unitários novos (seed e validação do login) com fake escrito à mão, sem banco.

## Decisões e concessões
- Bearer nativo em vez de JWT: sem chave de assinatura para guardar e sem pacote extra.
- Falhar na inicialização sem a senha do Admin, em vez de só avisar.
- A listagem de usuários é mínima (id e e-mail); a administração completa e o /register ficam na I03.

## Como validar
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
(configurar a senha do Admin por user-secrets ou Seed__Admin__Password, aplicar a migration e chamar /login e /api/admin/users; detalhes no README)

## Evidências
- Build com 0 avisos e 0 erros; 38 testes unitários aprovados.
- Login inválido: 401 sem token. Sem token: 401. Sem a role: 403. Admin: 200.
- Seed repetido: 5 roles e 1 Admin após reiniciar duas vezes.
- Senha de teste e token ausentes do log da aplicação.
- Pipeline code-quality na PR: (preencher com o score depois de ler o resultado)

## Impacto em segurança e autorização
- Nenhuma credencial versionada; a senha do Admin só por configuração segura.
- 401 e 403 distintos; login sem enumeração de contas; bloqueio por tentativas.
- Admin não recebe role funcional de despesa.

## Checklist
- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
```

## I03: Cadastro HTTP e gerenciamento de roles

**Status:** concluída e mergeada na `main` pela PR #3 (pipeline oficial 100/100, execução #18, com Gitleaks 8.30.1).

**Branch:** `i03-user-roles`.

### O que foi feito

- `POST /register` (público, em `AuthController`): `RegisterRequest` só com `Email` e `Password`, sem membro de role. Cria o usuário sem roles (`201`); e-mail duplicado `409`; entrada inválida ou senha fora da política `400` (só códigos, nunca a senha).
- `GET /api/admin/users` agora devolve `roles` de cada usuário (duas consultas e junção em memória; sem N+1).
- `PUT /api/admin/users/{id}/roles` (Admin): `UpdateUserRolesRequest` com `Roles` obrigatório. O PUT substitui o conjunto de roles. Role desconhecida: `400`, rejeitando o pedido inteiro; usuário inexistente: `404`; Admin removendo a própria role Admin: `403`.
- `UserAccountService` (regras) atrás de `IUserAccountStore`; `UserAccountStore` (EF Core com `ExpenseHubDbContext` e `UserManager`). A troca de roles é um único `SaveChanges`, com `SecurityStamp` renovado.
- Opção B do "novo login": `SecurityStampValidationMiddleware` (entre `UseAuthentication` e `UseAuthorization`) compara o stamp do token com o do banco; token antigo vira anônimo e a autorização responde `401`. Regra pura em `SecurityStampCheck`.
- 48 testes unitários novos (25 `UserAccountService`, 11 `RegisterRequest`, 4 `UpdateUserRolesRequest`, 8 `SecurityStampCheck`), com `FakeUserAccountStore` escrito à mão. Total: 86.
- README: estado atual, tabelas de issues e endpoints, seção Cadastro e roles (com o aviso de novo login), arquitetura, testes, qualidade, decisões, solução de problemas e o detalhe da I03.

### Decisões que afetam as próximas issues

- **Quem pode criar despesa:** o usuário cadastrado nasce sem role. Nas I04 em diante, `Employee` precisa ser concedido por um Admin antes de qualquer despesa; sem ele a resposta é `403`.
- **Dono da despesa:** continua vindo de `ClaimTypes.NameIdentifier` do token (o `Id` do `IdentityUser`).
- **Token e roles:** qualquer troca de roles invalida os tokens antigos do usuário (`401`). Testes manuais de I04 em diante precisam fazer login de novo depois de conceder roles.
- **Custo:** o middleware lê o usuário uma vez por requisição autenticada (inclusive nas rotas de despesa). Se virar problema de desempenho, é o ponto a otimizar (por exemplo cache curto do stamp); hoje é aceitável.
- **Constantes de roles:** use `AppRoles.*` em `[Authorize(Roles = ...)]`, nunca literais.
- **Ordem das respostas** nas rotas de roles: `401`, `403` pelo atributo, `400` (corpo ou role inválida), `404`, `403` (regra de própria role). Segue a decisão do `CLAUDE.md`.
- **Nunca sem Admin:** a regra "Admin não remove a própria role Admin" garante pelo menos um Admin; ele pode remover a role de outro Admin.

### O que foi medido (EF Core 10.0.12, SQLite, banco temporário fora do repositório)

Script descartável no diretório temporário da sessão, 44 verificações: 43 conforme o esperado e a 4b, que falhou por um defeito da própria verificação (explicado em Desvios) e foi conferida à parte:

- Cadastro: `201` com `roles` vazia; com `roles`, `role` e `isAdmin` no corpo continua sem roles; e-mail duplicado em outra caixa `409`; senha fraca, e-mail inválido e corpo vazio `400`.
- O usuário cadastrado recebe `403` na rota de Admin e `403` ao tentar se promover; sem token `401`.
- Listagem do Admin: `200` com as roles (`Admin`, vazia, vazia).
- `PUT`: `employee` e `approver` gravados como `Approver,Employee`; `Superuser` `400` e o banco segue com 5 roles e 3 usuários; uma role inválida no meio das válidas `400` sem nada aplicado; corpo `{}` e `roles: null` `400` com as roles preservadas; usuário inexistente `404`; usuário inexistente com role inválida `400`; Admin removendo a própria role (lista com outra role e lista vazia) `403` e continua Admin; mesmas roles `200`, sem escrita.
- Novo login: token antigo da alice depois da troca `401`; token novo `403` (sem Admin); depois de promovida a Admin, o token anterior `401` (não `403`) e o novo `200`; alice (Admin) tirando a role Admin do outro Admin `200`, e o token do outro Admin passa a `401`; alice tentando remover a própria role `403`; o Admin volta a ser Admin; o sistema sempre fica com pelo menos um Admin.
- Contagem final: 5 roles e 3 usuários. A senha e os tokens usados não aparecem no log da aplicação.

### Desvios e cuidados

- **CA1861:** arrays constantes de literais repetidos nos testes geram aviso; viraram campos `static readonly`. O `dotnet build` mostrou os 4 avisos; foram corrigidos na causa.
- **FIAP1002 e IDE1006 (lições da I02):** os testes novos usam `_credential = new string('a', 12)`, sem literal atribuído a nome com `password`, e todo campo privado não `const` leva `_`. O pipeline local dá 100/100.
- **PowerShell 7 e `application/problem+json`:** `Invoke-WebRequest` devolve o corpo de respostas `problem+json` como `byte[]`, e não como texto. A verificação 4b comparava texto e falhou por isso; ao decodificar os bytes, o corpo tem só os códigos `PasswordTooShort`, `PasswordRequiresNonAlphanumeric`, `PasswordRequiresDigit` e `PasswordRequiresUpper`, sem o valor da senha. Em scripts, use `[Text.Encoding]::UTF8.GetString($r.Content)` ou `Invoke-RestMethod`.
- **Porta do executável:** rodar `ExpenseHub.Api.exe` direto (sem o perfil de lançamento) sobe em `http://localhost:5000` em Production; para testar na `5245`, defina `ASPNETCORE_URLS=http://localhost:5245`. Um script que saía cedo deixou a API rodando, e o processo foi encerrado depois (era meu).
- A `MaxLength` em `IReadOnlyList<string>` funciona como esperado (teste `Validate_TooManyNames_ReportsRolesError`).

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 86 aprovados.
- `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks`: 100/100, 20 em cada categoria, sem bloqueantes, medido com os arquivos novos ainda sem commit (o script também analisa arquivos não rastreados). Repetir depois de commitar, com a árvore limpa.

### Pendências

- **Feitos:** commits, PR #3 e merge com "Create a merge commit"; score oficial 100/100 (execução #18, com Gitleaks 8.30.1); status "Concluída" no README. Nada pendente nesta issue.

### Modelo de PR

Título: `I03: Cadastro HTTP e gerenciamento de roles`

```text
Implementa o cadastro por HTTP e a administração de roles pelo Admin.

Issue: Racass/checkpoint-csharpracass-expensehub#3

## Resumo técnico
- POST /register cria o usuário sem nenhuma role; o DTO não tem membro de role, então qualquer role enviada é ignorada. E-mail duplicado: 409; entrada inválida ou senha fora da política: 400.
- GET /api/admin/users (Admin) passa a listar também as roles de cada usuário.
- PUT /api/admin/users/{id}/roles (Admin) substitui o conjunto de roles. Aceita só as cinco roles conhecidas, sem criar nenhuma; usuário inexistente: 404; o Admin não remove a própria role Admin (403).
- As regras ficam em UserAccountService, atrás de IUserAccountStore, e são testadas com fake escrito à mão, sem banco. A troca de roles é um único SaveChanges.
- Novo login após alterar roles, de verdade: cada troca renova o SecurityStamp e um middleware rejeita (401) tokens emitidos antes; o README documenta o novo login.
- 48 testes unitários novos (total de 86).

## Decisões e concessões
- O PUT substitui o conjunto de roles; a lista é obrigatória, então um corpo sem roles é 400 e nunca remove tudo por acidente.
- A própria role Admin protegida com 403 (regra de autorização), mantendo pelo menos um Admin.
- Invalidar o token antigo custa uma leitura do usuário por requisição autenticada; a alternativa de só documentar deixaria a role removida valer até o token expirar (1 hora).
- E-mail duplicado devolve 409, o que revela que a conta existe; escolhemos a clareza da resposta.
- Nenhum pacote novo.

## Como validar
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
(configurar a senha do Admin, aplicar a migration e chamar /register, /login e as rotas de Admin; detalhes no README)

## Evidências
- Build com 0 avisos e 0 erros; 86 testes unitários aprovados.
- Score local do pipeline de qualidade: 100/100 (com -SkipGitleaks).
- Cadastro com roles no corpo continua sem roles; usuário cadastrado recebe 403 na rota de Admin.
- Role inexistente: 400, sem nada aplicado e com 5 roles no banco; usuário inexistente: 404; Admin removendo a própria role: 403.
- Token emitido antes da troca de roles devolve 401; depois do novo login traz as roles atuais.
- Pipeline code-quality na PR: (preencher com o score depois de ler o resultado, com Gitleaks)

## Limitações conhecidas
- O cadastro, as roles e a invalidação do token dependem do host e do banco, então foram validados à mão, e não por teste unitário.
- O middleware faz uma leitura por requisição autenticada.

## Impacto em segurança e autorização
- O cadastro nunca concede role; só um Admin concede.
- Roles removidas deixam de valer no próximo uso do token, e não só depois que ele expira.
- Nenhuma credencial versionada.

## Checklist
- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
```

## I04: Criar e editar rascunho

**Status:** concluída e mergeada na `main` pela PR #4 (pipeline oficial 100/100, execução #22, com Gitleaks 8.30.1).

**Branch:** `i04-expense-draft`.

### O que foi feito

- `POST /api/expenses` e `PUT /api/expenses/{id:guid}` (`ExpensesController`, `[Authorize(Roles = AppRoles.Employee)]`). `201` na criação (sem `Location`) e `200` na edição, com `ExpenseResponse`.
- `ExpenseRequest`, um DTO só para os dois endpoints, com `Description`, `Amount` e `ExpenseDate` obrigatórios e anotações de dados (`Required`, `StringLength` e `[MoneyAmount]`). Sem dono, estado, ator nem horários: o que vier a mais no corpo é ignorado.
- `ExpenseRules` (regras puras): descrição de 10 a 500 caracteres depois de aparada, valor de 0,01 a `Int32.MaxValue` com no máximo duas casas decimais (rejeita, nunca arredonda) e data não posterior a hoje.
- `BrazilTime`: a "data de hoje" é a de Brasília (`America/Sao_Paulo`, com o nome do Windows como alternativa).
- `ExpenseService` (`IExpenseRepository` e `TimeProvider`): criar gera o `Guid`, define dono e ator pelo token, estado `Draft`, horário do relógio e o histórico `Created` na mesma gravação; editar valida, procura a despesa do dono, exige `Draft` e grava o histórico `Edited` com o resumo (`Changes`).
- `IExpenseRepository` e `ExpenseRepository` (EF Core), criados nesta issue como o `CLAUDE.md` manda. `FindOwnedAsync(id, ownerId)` filtra o dono na consulta. O serviço nunca recebe `DbContext`.
- `AddExpenseHubExpenses()` registra `TimeProvider.System`, o repositório e o serviço.
- 110 testes unitários novos (42 `ExpenseRules`, 4 `BrazilTime`, 26 serviço na criação, 20 na edição, 18 `ExpenseRequest`), com `FakeExpenseRepository` e `FixedTimeProvider` escritos à mão. Total: 196.
- Nenhuma migration nova e nenhum pacote novo.
- README: estado atual, tabelas de issues e endpoints, arquitetura, testes, qualidade, decisões, solução de problemas e o detalhe da I04.

### Decisões que afetam as próximas issues

- **Quem cria despesa:** só `Employee`. Um usuário cadastrado nasce sem role; um Admin concede `Employee` e o usuário faz login de novo. O Admin sozinho não cria despesa (`403`).
- **Decisões do Pedro nesta issue:** (1) o `PUT` com os mesmos valores grava histórico `Edited` com `No field changed.`; (2) `201` sem `Location`; (3) a data não futura é comparada com a data do Brasil, e não a UTC; (4) o `PUT` substitui os três campos, todos obrigatórios.
- **I05:** crie o `GET /api/expenses/{id}` e, se quiser, passe a devolver `Location` no `201` da criação. O filtro de visibilidade (Employee: próprias; Approver: `Submitted`; Finance: `Approved` e `Paid`; Auditor: todas) deve ir na consulta do repositório, antes de materializar, como `FindOwnedAsync` já faz para o dono. `ExpenseResponse` já existe e serve para o detalhe e para a lista.
- **I05 e I07: concorrência.** Hoje a edição lê o estado e grava depois, sem proteção contra uma transição simultânea (por exemplo editar enquanto outro pedido envia). Para as transições, avalie um token de concorrência no estado (`IsConcurrencyToken` no `Status`, que muda o snapshot do modelo e pede uma migration vazia de esquema) ou um `UPDATE` condicional (`WHERE Status = 'Draft'`).
- **I06:** a despesa de outro usuário responde `404` porque `FindOwnedAsync` só enxerga as do dono. Um usuário com `Employee` e outra role (por exemplo `Auditor`) que tente editar a despesa de outra pessoa também recebe `404` aqui; a I06 deve refinar para `403` quando a despesa é visível por outra role.
- **Histórico:** o padrão é adicionar a linha em `expense.History` e salvar uma vez, e não gravar o histórico em outra operação. As transições da I05, I07 e I08 devem seguir o mesmo padrão (a atomicidade lógica é critério da I08).
- **Dinheiro:** as validações de valor estão em `ExpenseRules` e no atributo `[MoneyAmount]`. Reaproveite-os se outro DTO receber valor.
- **Relógio:** injete `TimeProvider` e use o fake `FixedTimeProvider` dos testes. Não use `DateTime.UtcNow` direto nos serviços.

### O que foi medido (EF Core 10.0.12, SQLite, banco temporário fora do repositório)

Script descartável no diretório temporário da sessão, 60 verificações, todas conforme o esperado depois da correção da leitura do banco no próprio script:

- Criação: sem token `401`; usuário sem role e o Admin `403`; Employee `201`, `Draft`, dono igual ao usuário do token, sem cabeçalho `Location`.
- Mass assignment: corpo com `ownerId` (de outro usuário), `status` `Approved`, `createdAtUtc` antigo, `actorId`, `id` e `payment` resulta em despesa do dono do token, `Draft`, com id e horário gerados pelo servidor.
- Valor: `0`, `0.009`, `12.345`, `-1` e `2147483647.01` dão `400`; `0.01` e `2147483647` dão `201`. O erro de casas decimais aponta o campo `Amount`.
- Descrição: 9 caracteres, 501 e só espaços dão `400`; 10 e 500 dão `201`. Data: amanhã em Brasília `400`, hoje `201`, `2026-02-30` `400`. Corpo vazio e `amount` como texto: `400`.
- Banco: valor 87,50 gravado como 8750 centavos; um histórico `Created` com ator igual ao dono, anterior nulo e novo `Draft`; toda despesa criada tem exatamente 1 histórico.
- Edição: dono `200`; campos substituídos; dono, estado e `createdAtUtc` intactos; histórico `Created` e `Edited` (de `Draft` para `Draft`) com `Changes` listando descrição e valor; 120,4 gravado como 12040 centavos.
- Negativas: outro Employee `404` e a despesa intacta; `id` inexistente e `id` que não é Guid `404`; mass assignment no `PUT` não muda dono nem estado; valor inválido, data futura e campo ausente `400` sem gravar histórico; `PUT` com os mesmos valores `200` com `No field changed.`.
- Fora de `Draft` (estado mudado direto no banco temporário, só para a evidência): `Submitted`, `Approved`, `Rejected` e `Paid` dão `409` sem gravar histórico; a despesa `Submitted` de outro usuário dá `404`, e não `409`.
- A senha e os tokens usados não aparecem no log da aplicação.

### Desvios e cuidados

- **Atributo próprio e nome do campo:** `MoneyAmountAttribute` primeiro devolvia o erro sem o nome do campo (`MemberNames` vazio); o MVC atribui a propriedade, mas o teste com `Validator.TryValidateObject` falhou. Corrigido informando `validationContext.MemberName`.
- **`DataRow` e `decimal`:** atributos não aceitam `decimal` como constante; os testes passam o valor como texto e convertem com `CultureInfo.InvariantCulture` (`ExpenseTestData.Dec`).
- **Script de validação:** `ConvertFrom-Json` achata o resultado de uma consulta de uma só linha; use `-NoEnumerate`. Cinco verificações falharam por causa disso, e não por defeito do código. No PowerShell 7, `Invoke-WebRequest` devolve o corpo de `application/problem+json` como `byte[]`.
- **Porta do executável:** rodar `ExpenseHub.Api.exe` direto sobe em `http://localhost:5000`; defina `ASPNETCORE_URLS=http://localhost:5245` para testar na `5245`.
- **Lições anteriores seguidas:** nenhum literal atribuído a nome com `password`, campos privados `static readonly` com `_`, arrays constantes em campos (CA1861), um tipo por arquivo. O pipeline local deu 100/100 de primeira.

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 196 aprovados.
- `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks`: 100/100, 20 em cada categoria, sem bloqueantes, medido com os arquivos novos ainda sem commit (o script também analisa arquivos não rastreados). Repetir depois de commitar, com a árvore limpa.

### Pendências

- **Feitos:** commits, PR #4 e merge com "Create a merge commit"; score oficial 100/100 (execução #22, com Gitleaks 8.30.1); status "Concluída" no README. Nada pendente nesta issue.
- **Superado na I07:** o 404 do `PUT` para despesa de outro usuário virou 403; o 404 do `PUT` vale só para despesa inexistente.

### Modelo de PR

Título: `I04: Criar e editar rascunho`

```text
Implementa a criação e a edição de rascunhos de despesa pelo Employee.

Issue: Racass/checkpoint-csharpracass-expensehub#4

## Resumo técnico
- POST /api/expenses (Employee) cria um rascunho (Draft) cujo dono vem do token; PUT /api/expenses/{id} substitui descrição, valor e data de um rascunho do próprio usuário.
- O DTO tem só descrição, valor e data, todos obrigatórios: dono, estado, ator e horários são do servidor, e o que o cliente enviar a mais é ignorado (mass assignment impedido).
- Contrato de validação: descrição de 10 a 500 caracteres (aparada), valor de 0,01 até Int32.MaxValue com no máximo duas casas decimais (rejeita, nunca arredonda) e data não posterior a hoje em Brasília.
- Edição: dono e Draft. Despesa de outro usuário responde 404 (o filtro de dono está na consulta) e fora de Draft responde 409, sem alterar nada nem gravar histórico.
- A criação e cada edição gravam a linha de histórico (Created e Edited, com o resumo das alterações) na mesma gravação da despesa.
- IExpenseRepository nasce nesta issue; ExpenseService e ExpenseRules são classes simples testadas com fake do repositório e relógio fixo, sem banco.
- 110 testes unitários novos (total de 196). Nenhuma migration nem pacote novo.

## Decisões e concessões
- O PUT é uma substituição completa: campo ausente é 400, e nunca "mantém o valor antigo".
- Editar com os mesmos valores grava histórico com "No field changed.".
- A data não futura usa a data do Brasil, e não a UTC.
- 201 da criação sem cabeçalho Location, porque o GET por id nasce na I05.
- Usuário com Employee e outra role que edita a despesa de outra pessoa recebe 404; a visibilidade completa por role é da I06.

## Como validar
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
(configurar a senha do Admin, aplicar a migration, conceder Employee a um usuário e fazer login de novo; detalhes no README)

## Evidências
- Build com 0 avisos e 0 erros; 196 testes unitários aprovados.
- Score local do pipeline de qualidade: 100/100 (com -SkipGitleaks).
- Criação: 201 com Draft e dono do token, mesmo com ownerId e status no corpo; sem token 401; sem a role 403.
- Valor 0, 0.009, 12.345, -1 e 2147483647.01: 400; 0.01 e 2147483647: 201. Descrição de 9 e 501 caracteres, data futura e data inválida: 400.
- Banco: valor gravado em centavos (87,50 vira 8750); um histórico Created por despesa e um Edited por edição.
- Outro Employee edita o rascunho: 404 e a despesa continua igual. Submitted, Approved, Rejected e Paid: 409 sem histórico novo.
- A senha e os tokens usados não aparecem no log da aplicação.
- Pipeline code-quality na PR: (preencher com o score depois de ler o resultado, com Gitleaks)

## Limitações conhecidas
- Ainda não há GET de despesas (I05); a gravação foi conferida direto no banco, e o 409 foi provado mudando o estado no banco temporário.
- Edição e envio simultâneos podem disputar o estado; a proteção de concorrência é avaliada nas transições da I05 e da I07.
- O criar, o editar e a gravação dependem do host e do banco, então foram validados à mão, e não por teste unitário.

## Impacto em segurança e autorização
- Só Employee cria e edita; o Admin não ganha acesso funcional a despesas.
- O dono vem sempre do token; o filtro de dono vai na consulta, e a despesa de outro usuário nunca é carregada.
- Dono, estado, ator e horários nunca vêm do cliente.
- Nenhuma credencial versionada.

## Checklist
- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
```

## I05: Enviar, listar e consultar

**Status:** concluída e mergeada na `main` pela PR #5 (pipeline oficial 100/100, execução #26, com Gitleaks 8.30.1).

**Branch:** `i05-submit-query`.

### O que foi feito

- `POST /api/expenses/{id:guid}/submit` (Employee): `Draft` para `Submitted`, sem corpo. Grava o histórico `Submitted` (anterior `Draft`, ator = dono, horário do servidor) na mesma gravação.
- `GET /api/expenses` e `GET /api/expenses/{id:guid}` (Employee, Approver, Finance ou Auditor): lista e detalhe filtrados pelo perfil.
- `POST /api/expenses` agora devolve o cabeçalho `Location` (`/api/expenses/{id}`), porque o `GET` por id passou a existir.
- Visibilidade (`ExpenseCaller`, `ExpenseScope`, `ExpenseVisibility`): regra pura. Employee lê as próprias; Approver as `Submitted`; Finance as `Approved` e `Paid`; Auditor todas; o Admin sozinho nada. Roles acumuladas somam os escopos. O escopo é um predicado (`Expression`) com valores capturados, aplicado pelo repositório dentro do `WHERE`.
- `IExpenseRepository` ganhou `ListAsync(scope)` (mais nova primeiro, sem rastreamento) e `FindVisibleAsync(id, scope)`. `FindOwnedAsync` continua servindo ao `PUT`.
- `ExpenseService`: `SubmitAsync`, `ListAsync` e `GetAsync`. O envio responde `404` (fora do escopo de leitura), `403` (`NotOwner`: visível, mas de outro dono) e `409` (`NotDraft`), nessa ordem, como o `CLAUDE.md` pede.
- `Status` virou token de concorrência do EF (`.IsConcurrencyToken()`); `ExpenseRepository.SaveChangesAsync` converte `DbUpdateConcurrencyException` em `ExpenseConflictException`, e o serviço responde `409` (no envio e no `PUT`). Migration `ExpenseStatusConcurrencyToken`, vazia (o esquema não muda; só o snapshot).
- `ExpensesController` deixou de ter `[Authorize]` na classe: cada ação tem o seu (`AppRoles.Employee` para escrever, `AppRoles.ExpenseReaders` para ler).
- A mensagem do `409` passou a ser "The expense is not a draft." (vale para editar e enviar).
- 54 testes unitários novos (19 `ExpenseVisibility`, 18 envio, 16 consulta, 1 de conflito na edição), com o `FakeExpenseRepository` aplicando o predicado do escopo. Total: 250.
- README: estado atual, tabelas de issues e endpoints, arquitetura, testes, qualidade, decisões, solução de problemas e o detalhe da I05.

### Decisões que afetam as próximas issues

- **Decisões do Pedro nesta issue:** (1) token de concorrência no `Status`; (2) `Location` no `201` da criação; (3) `403` quando a despesa é visível por outra role mas é de outro dono; (4) lista da mais nova para a mais antiga, sem paginação; (5) manter o `ownerId` na resposta.
- **I06 (ownership e matriz):** o `PUT` ainda usa `FindOwnedAsync`, então um usuário com `Employee` e outra role (por exemplo `Auditor`) que edite a despesa de outra pessoa recebe `404`. Leve a regra do envio ao `PUT` (`FindVisibleAsync`, depois `403` se não for dono, depois `409`). A visibilidade já está pronta em `ExpenseVisibility`; a I06 deve reusá-la, e não recriá-la. Reforce com testes de matriz completa por role.
- **I07 e I08 (aprovar, reprovar e pagar):** reutilize o padrão do envio: `FindVisibleAsync`, `404`, `403` e `409`, uma única gravação do estado e do histórico, e o `Status` como token de concorrência (já configurado), de modo que duas decisões simultâneas deem um `200` e um `409`. A regra "ninguém aprova nem paga a própria despesa" é `403`. Approver enxerga as `Submitted` e Finance as `Approved` e `Paid`, que são exatamente os escopos já implementados.
- **Escopo e filtro na consulta:** qualquer endpoint novo de leitura (como o histórico da I08, "mesma visibilidade do reembolso") deve usar `ExpenseScope.Predicate` dentro da consulta, nunca carregar e filtrar em memória.
- **Resposta:** `ExpenseResponse` não traz e-mail, histórico nem pagamento; mantenha assim em listagem e detalhe.
- **Teste manual:** para ver `Approved`, `Paid` e `Rejected` antes da I07 e da I08, mude o `Status` direto no banco temporário.

### O que foi medido (EF Core 10.0.12, SQLite, banco temporário fora do repositório)

Script descartável no diretório temporário da sessão, 56 verificações, todas conforme o esperado depois de corrigir defeitos do próprio script (explicados em Desvios):

- Envio: sem token `401`; sem role, Admin, Approver, Finance e Auditor sozinhos `403`; outro Employee `404`; Employee mais Auditor `403`; Employee mais Approver na `Submitted` de outro `403` (antes de `409`); dono `200`, `Submitted`, dono inalterado; histórico `Created` e `Submitted` com anterior `Draft`, novo `Submitted` e ator igual ao dono; reenvio `409` sem histórico novo; `Submitted`, `Approved`, `Rejected` e `Paid` dão `409` sem histórico; `id` inexistente e `id` que não é Guid `404`; `status` e `ownerId` no corpo do envio são ignorados.
- Concorrência: 12 pares de envios simultâneos do mesmo rascunho deram sempre um `200` e um `409`, e cada despesa ficou com exatamente uma linha `Submitted` no histórico (conferido no banco). O log do EF mostra o `UPDATE` do estado com `WHERE "Id" = ... AND "Status" = ...`, que é o token em ação.
- Listagem: Employee vê só as próprias (e nenhuma de outro dono); Approver vê exatamente as `Submitted` de mais de um dono (e nenhum `Draft` nem `Rejected`); Finance vê exatamente as `Approved` e `Paid`; Auditor vê todas; Employee mais Approver vê a união; Employee mais Auditor vê todas; Admin sozinho e usuário sem role `403`; sem token `401`; ordenada da mais nova para a mais antiga.
- Detalhe: Employee lê a própria e recebe `404` para as de outros; Approver lê a `Submitted` de outro e não o `Draft`; Finance lê a `Paid` e não a `Submitted` nem a `Rejected`; Auditor lê `Draft`, `Rejected` e `Paid`; o `404` de uma despesa inexistente e o de uma invisível têm o mesmo corpo; a resposta tem só os sete campos esperados, sem `@` e sem histórico nem pagamento; o `Location` da criação leva ao `GET` da despesa.
- A senha e os tokens usados não aparecem no log da aplicação.

### Desvios e cuidados

- **Script de validação:** `ConvertFrom-Json -NoEnumerate` devolve o resultado do SQLite sem desenrolar, e passar isso por um pipe entrega só a primeira linha; a contagem do histórico e as listas esperadas saíram erradas por isso, e não por defeito do código (o banco confirmou 1 linha `Submitted` por despesa). No PowerShell 7, `$r.Headers['Location']` é um array, então se compara o `[0]`. Corrigi o script e repeti tudo.
- **`ForEach-Object -Parallel`:** o `$using:` não aceita uma expressão (como `$using:($t.emp1)`); copie o valor para uma variável antes.
- **API pelo `dotnet`, e não pelo `.exe`:** o Smart App Control do Windows desta máquina bloqueia `ExpenseHub.Api.exe` e a DLL de testes; `dotnet ExpenseHub.Api.dll` (o que o `dotnet run` faz) continua funcionando.
- **Predicado do escopo:** o EF traduz o predicado como `@seesAll = 1 OR (@owner IS NOT NULL AND OwnerId = @owner) OR ...`, com os valores como parâmetros. Se o desempenho virar problema, dá para montar o predicado só com as partes que se aplicam (uma consulta por combinação de roles).
- **Lições anteriores seguidas:** nenhum literal atribuído a nome com `password`; campos privados com `_`; arrays constantes dos testes de consulta viraram textos ordenados (CA1861); um tipo por arquivo. Um script de substituição que eu rodei sobre um arquivo de teste deixou valores esperados vazios, e eu os corrigi à mão; confira sempre o `git diff` de arquivos alterados por script.

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 250 aprovados.
- `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks`: 100/100, 20 em cada categoria, sem bloqueantes, medido com os arquivos novos ainda sem commit (o script também analisa arquivos não rastreados). Depois de commitar, o Smart App Control pode bloquear a DLL de testes e produzir um falso 96 (FIAP4001); o score oficial é o do workflow.

### Pendências

- **Feitos:** commits, PR #5 e merge com "Create a merge commit"; score oficial 100/100 (execução #26, com Gitleaks 8.30.1); status "Concluída" no README. Nada pendente nesta issue.
- **Superado na I07:** o 404 do envio para despesa de outro Employee virou 403; o 404 do envio vale só para despesa inexistente. O `GET` por id e a listagem não mudaram (404 fora do escopo de leitura).
- **Superado na I08:** não é mais preciso mudar o `Status` direto no banco para ver `Approved`, `Paid` e `Rejected`, porque aprovar, reprovar e pagar têm endpoint (o item "Teste manual" acima vale só para a época da I05). O padrão "`FindVisibleAsync`, 404, 403 e 409" que a I05 indicou para as decisões foi trocado na I07 pela busca por identificador.

### Modelo de PR

Título: `I05: Enviar, listar e consultar`

```text
Implementa o envio de rascunhos e a consulta de despesas conforme o perfil.

Issue: Racass/checkpoint-csharpracass-expensehub#5

## Resumo técnico
- POST /api/expenses/{id}/submit (Employee) executa Draft para Submitted e grava o histórico Submitted na mesma gravação. Só o dono envia: outro Employee recebe 404, quem enxerga a despesa por outra role mas não é o dono recebe 403, e fora de Draft (inclusive reenvio) é 409 sem histórico novo.
- GET /api/expenses lista, e GET /api/expenses/{id} detalha, apenas o que o perfil lê: Employee as próprias, Approver as Submitted, Finance as Approved e Paid, Auditor todas. Roles acumuladas somam os filtros, e o Admin sozinho não lê nada (403).
- O filtro do perfil é um predicado aplicado dentro da consulta (WHERE), antes de materializar; nunca se carrega tudo para filtrar na memória.
- O detalhe não vaza: despesa inexistente e despesa fora do escopo recebem o mesmo 404, e a resposta não traz e-mail, histórico nem pagamento.
- O Status é token de concorrência do EF: dois envios simultâneos do mesmo rascunho dão um 200 e um 409, e um único histórico. Migration vazia, só para o snapshot do modelo.
- POST /api/expenses passa a devolver o cabeçalho Location.
- 54 testes unitários novos (total de 250), com fake do repositório que aplica o escopo; sem banco. Nenhum pacote novo.

## Decisões e concessões
- 404 fora do escopo de leitura, 403 quando é visível mas de outro dono, 409 quando o estado não aceita (regra do CLAUDE.md).
- Listagem da mais nova para a mais antiga, sem paginação (fora de escopo).
- O ownerId fica na resposta (identificador interno, sem e-mail).
- O PUT da I04 continua respondendo 404 para um usuário com Employee e outra role que edita a despesa de outro; levar a regra completa ao PUT é da I06.

## Como validar
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet tool restore
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
(configurar a senha do Admin, conceder roles diferentes a usuários e fazer login de novo; detalhes no README)

## Evidências
- Build com 0 avisos e 0 erros; 250 testes unitários aprovados.
- Envio: dono 200 com Submitted; outro Employee 404; Employee mais Auditor 403; Approver, Finance, Auditor e Admin sozinhos 403; reenvio e estados Submitted, Approved, Rejected e Paid 409 sem histórico novo.
- 12 pares de envios simultâneos: sempre um 200, um 409 e uma única linha Submitted no histórico; o UPDATE do estado leva a condição do estado lido.
- Listagem por perfil conferida contra o banco para Employee, Approver, Finance, Auditor e as combinações; Admin sozinho 403.
- Detalhe: fora do escopo e inexistente têm o mesmo corpo de 404.
- A senha e os tokens usados não aparecem no log da aplicação.
- Pipeline code-quality na PR: (preencher com o score depois de ler o resultado, com Gitleaks)

## Limitações conhecidas
- Aprovar, reprovar e pagar são das issues seguintes; para ver Approved, Paid e Rejected foi preciso mudar o estado direto no banco temporário.
- A listagem não tem paginação (fora de escopo).
- As consultas, o envio e a concorrência dependem do host e do banco, então foram validados à mão, e não por teste unitário.
- Na máquina de desenvolvimento, o Smart App Control do Windows pode bloquear a DLL de testes e gerar um falso "teste falhou" no script local; o score oficial é o do workflow desta PR.

## Impacto em segurança e autorização
- Só Employee cria, edita e envia; só o dono envia o próprio rascunho.
- Cada perfil lê somente o seu escopo, aplicado na consulta; recursos fora dele dão 404 sem revelar a existência.
- O Auditor lê tudo e nunca escreve; o Admin sozinho não ganha acesso funcional.
- Estado, dono, ator e horários nunca vêm do cliente.
- Nenhuma credencial versionada.

## Checklist
- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
```

## I06: Ownership e matriz de acesso

**Status:** concluída e mergeada na `main` pela PR #6 (pipeline oficial 100/100, execução #29, com Gitleaks 8.30.1). Parte do comportamento desta issue (404 por escopo de leitura nas ações de escrita) foi superada pelas decisões do professor e muda na I07.

**Branch:** `i06-ownership-access`.

### O que foi feito

- `ExpenseAccess` (classe estática pura, sem EF nem controller): a matriz de autorização de despesas em uma regra só. `Evaluate(caller, ação, despesa)` devolve `AccessDecision` (`Allowed`, `NotFound`, `Forbidden`, `WrongState`), sempre na ordem: role da ação (`403`), escopo de leitura (`404`), regra de dono (`403`), estado (`409`). `HasRoleFor(caller, ação)` expõe só a checagem de role.
- `ExpenseAction` (`Create`, `Edit`, `Submit`, `Approve`, `Reject`, `Pay`) e `AccessDecision`, cada um no seu arquivo.
- Matriz: criar, editar e enviar exigem `Employee` e, para editar e enviar, o dono e `Draft`. Aprovar e reprovar exigem `Approver`, nunca o dono, e `Submitted`. Pagar exige `Finance`, nunca o dono, e `Approved`.
- `ExpenseScope.Allows(despesa)`: o mesmo predicado do escopo de leitura, compilado uma vez (`Lazy`), para decidir sobre uma despesa já carregada. Nunca é usado para filtrar listas.
- `ExpenseService`: `CreateAsync`, `UpdateAsync` e `SubmitAsync` agora recebem o `ExpenseCaller` (e não só o id) e decidem por `ExpenseAccess`. O serviço exige a role `Employee` sozinho, antes de validar e de consultar.
- O `PUT` passou a usar `FindVisibleAsync` e a regra completa: `404` (fora do escopo), `403` (visível, mas de outro dono), `409` (fora de `Draft`). Isso resolve a limitação registrada na I04 e na I05.
- `ExpenseOperationStatus.NotOwner` virou `Forbidden`, um `403` único (falta de role ou regra de dono), com a mensagem "You are not allowed to do this with this expense.".
- `IExpenseRepository.FindOwnedAsync` saiu (repositório, interface e fake): nada mais o usa.
- 100 testes unitários novos: 83 de `ExpenseAccess` (casos nomeados de edição e envio, aprovar e reprovar, pagar e criar, mais cinco verificações de propriedades de segurança sobre todas as combinações de roles, ações, estados e donos), 8 de criação e 9 de edição. Total: 350.
- README: estado atual, tabelas de issues e endpoints, arquitetura, testes, qualidade, decisões, solução de problemas e o detalhe da I06, com a matriz preenchida. Nenhuma migration e nenhum pacote novo.

### Decisões que afetam as próximas issues

- **Decisões do Pedro nesta issue:** (1) só a regra de aprovar, reprovar e pagar, sem os endpoints (que são da I07 e da I08); (2) `NotOwner` renomeado para `Forbidden`; (3) `PUT` com a regra completa (`404`, `403`, `409`); (4) o histórico é da I08.
- **I07 (aprovar e reprovar) e I08 (pagar):** chamem `ExpenseAccess.Evaluate(caller, ExpenseAction.Approve | Reject | Pay, despesa)` dentro do serviço, com a despesa vinda de `FindVisibleAsync(id, scope)`. Mapeie: `NotFound` para `404`, `Forbidden` para `403` (inclui "aprovar ou pagar a própria despesa"), `WrongState` para `409`. A role e o dono já estão decididos pela regra; não os confira de novo no controller, só no atributo (`[Authorize(Roles = AppRoles.Approver)]` ou `AppRoles.Finance`).
- **Concorrência (I07 e I08):** o `Status` já é token de concorrência; use o mesmo padrão do envio (`try` em `SaveChangesAsync` e `ExpenseConflictException` vira `409`), com o estado e o histórico na mesma gravação.
- **Resposta de decisão:** reprovar exige justificativa de 10 a 500 caracteres (REQUISITOS); a regra de acesso não cobre isso (é validação de DTO).
- **Histórico (I08):** `GET /api/expenses/{id}/history` deve usar `ExpenseScope` na consulta (a mesma visibilidade da despesa: dono, Approver nas `Submitted`, Finance nas `Approved` e `Paid`, Auditor em todas). Despesa fora do escopo dá `404`; quem não lê despesas (Admin sozinho) dá `403`.
- **Mensagens de erro:** o `403` do serviço de despesas tem sempre a mesma mensagem; o `409` é "The expense is not a draft." para editar e enviar e deve ganhar mensagens próprias nas decisões ("The expense is not submitted." e "The expense is not approved.").
- **Matriz de testes:** acrescentem casos ao `ExpenseAccessMatrixTests` se mudarem alguma regra; ele é a especificação executável da matriz.

### O que foi medido (EF Core 10.0.12, SQLite, banco temporário fora do repositório)

Script descartável no diretório temporário da sessão, 40 verificações, todas conforme o esperado depois de corrigir defeitos do próprio script (explicados em Desvios):

- Isolamento entre Employees: o emp2 recebe `404` ao ler, editar e enviar despesas do emp1 (e vice-versa), trocando o id na URL, e as despesas atacadas não mudam (estado, descrição e histórico iguais); o `404` de uma despesa alheia tem o mesmo corpo do de uma inexistente; cada um lista só as suas.
- Employee mais outra role: Employee mais Auditor edita e envia o próprio rascunho (`200`), lê o de outro (`200`) e recebe `403` ao editar ou enviar o de outro; Employee mais Approver recebe `403` na `Submitted` de outro (antes do `409`) e `404` no `Draft` de outro; Employee mais Finance recebe `403` na `Approved` de outro e `404` no `Draft` de outro.
- Quem não tem `Employee` (Auditor, Approver, Finance e sem role) recebe `403` ao criar, editar e enviar; o Admin sozinho recebe `403` em criar, editar, enviar, listar e ler; sem token, `401` nas cinco rotas; as contagens de despesas e de histórico não mudam com as tentativas do Auditor.
- Filtros: Approver lista exatamente as `Submitted`, Finance as `Approved` e `Paid`, Auditor todas (conferido contra o banco).
- Log do EF: todas as consultas de despesas têm `WHERE`, com as condições de escopo; a única sem `WHERE` é a listagem do Auditor, cujo escopo é "todas". A senha e os tokens não aparecem no log.
- A prova de autoaprovação e de autopagamento é por teste unitário, porque aprovar e pagar ainda não têm endpoint.

### Desvios e cuidados

- **Build incremental e restauração de arquivo:** depois de uma quebra proposital de `ExpenseAccess` (21 testes falharam, como esperado), restaurei o arquivo com `Copy-Item`, que manteve a data antiga, e o `dotnet test` reutilizou a DLL quebrada (os 21 continuaram falhando). Um rebuild completo (`--no-incremental`) resolveu. Ao restaurar um arquivo, atualize a data de modificação ou rode `--no-incremental`.
- **Script de validação:** uma função chamada `Where` colidiu com o alias do PowerShell para `Where-Object` e produziu SQL vazio, o que deixou o `SetStatus` sem efeito e derrubou algumas verificações; renomeei para `ExpWhere` e repeti tudo. A verificação "nenhuma consulta sem WHERE" estava mal formulada: a listagem do Auditor não tem `WHERE` por ter escopo total. Evite nomes de função que colidam com aliases do PowerShell.
- **API pelo `dotnet`, e não pelo `.exe`:** o Smart App Control desta máquina pode bloquear `ExpenseHub.Api.exe`; `dotnet ExpenseHub.Api.dll` continua funcionando.
- **Lições anteriores seguidas:** nenhum literal atribuído a nome com `password`, campos privados com `_`, arrays constantes dos testes sem `new[]` em argumentos (CA1861), um tipo por arquivo.

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 350 aprovados.
- `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks`: 100/100, 20 em cada categoria, sem bloqueantes, medido com os arquivos novos ainda sem commit. Depois de commitar, o Smart App Control pode bloquear a DLL de testes e produzir um falso 96 (FIAP4001); o score oficial é o do workflow.
- Verificação de mutação: a regra de dono de `ExpenseAccess` foi desligada de propósito e 21 testes falharam; restaurada, os 350 passam.

### Pendências

- **Feitos:** commits, PR #6 e merge com "Create a merge commit"; score oficial 100/100 (execução #29, com Gitleaks 8.30.1); status "Concluída" no README.
- **Superado na I07:** a ordem "role, escopo de leitura (404), dono (403), estado (409)" de `ExpenseAccess` passa a valer só para leituras. Nas ações de escrita a ordem é role, despesa inexistente (404), dono (403), estado (409), conforme as respostas do professor. Isso muda `ExpenseAccess`, o serviço (`UpdateAsync` e `SubmitAsync`), os testes da matriz e a documentação.
- **Superado na I08:** as limitações "aprovar, reprovar e pagar ainda não têm endpoint" e "o histórico e a consulta dele são da I08" não valem mais. Aprovar e reprovar têm endpoint desde a I07, e o pagamento e o histórico desde a I08, então a proibição de autopagamento passou a ser provada também por requisição. A indicação de usar `FindVisibleAsync` nas decisões (Decisões desta issue) foi superada na I07.

### Modelo de PR

Título: `I06: Ownership e matriz de acesso`

```text
Aplica a matriz de acesso combinando role, dono e estado na camada de serviço.

Issue: Racass/checkpoint-csharpracass-expensehub#6

## Resumo técnico
- ExpenseAccess concentra a matriz de autorização em uma regra pura (sem EF nem controller): a ordem é role (403), escopo de leitura (404), regra de dono (403) e estado (409). Criar, editar e enviar exigem Employee (editar e enviar, só o dono e só em Draft); aprovar e reprovar exigem Approver e pagar exige Finance, e ninguém decide sobre a própria despesa.
- Acumular roles soma permissões, mas não remove a proibição sobre o recurso próprio: Employee mais Approver não aprova a própria despesa, e Employee mais Finance não paga a própria.
- O ExpenseService decide por ExpenseAccess e exige a role por conta própria; o atributo do controller é só a primeira barreira.
- O PUT passa a seguir a mesma regra do envio (404 fora do escopo de leitura, 403 quando é visível mas de outro dono, 409 fora de Draft). Isso resolve a limitação registrada na I04 e na I05.
- NotOwner virou Forbidden, um 403 único.
- Aprovar, reprovar e pagar entram como regra provada por teste unitário; os endpoints são da I07 e da I08.
- 100 testes unitários novos (total de 350), com a matriz completa e verificações de propriedades de segurança sobre todas as combinações. Nenhum pacote novo nem migration.

## Decisões e concessões
- Só a regra de aprovar, reprovar e pagar, sem os endpoints, que ficam com as issues seguintes.
- O serviço exige a role Employee por dentro, e quem não a tem recebe 403 antes de qualquer validação ou consulta.
- Um Approver só enxerga as Submitted e um Finance só as Approved e Paid, então decidir fora desses estados dá 404, e não 409.
- O histórico (consulta autorizada) é da I08 e deve reaproveitar o mesmo escopo de leitura.

## Como validar
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet tool restore
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
(configurar a senha do Admin, conceder perfis diferentes a vários usuários e fazer login de novo; detalhes no README)

## Evidências
- Build com 0 avisos e 0 erros; 350 testes unitários aprovados.
- Isolamento entre Employees: trocar o id na URL dá 404 em GET, PUT e envio, com o mesmo corpo de uma despesa inexistente, e a despesa atacada não muda.
- Employee mais Auditor: edita e envia o próprio rascunho, lê o de outro e recebe 403 ao editar ou enviar o de outro. Employee mais Approver e Employee mais Finance: 403 no que está visível pela outra role e 404 no que não está.
- Auditor, Approver, Finance e sem role nunca criam, editam nem enviam (403); o Admin sozinho recebe 403 em todas as rotas de despesa; sem token, 401.
- Log do EF: todas as consultas de despesas têm WHERE com o escopo; a única sem WHERE é a listagem do Auditor (escopo total).
- Verificação de mutação: desligar a regra de dono de ExpenseAccess derruba 21 testes.
- A senha e os tokens usados não aparecem no log da aplicação.
- Pipeline code-quality na PR: (preencher com o score depois de ler o resultado, com Gitleaks)

## Limitações conhecidas
- Aprovar, reprovar e pagar ainda não têm endpoint: a proibição de autoaprovação e de autopagamento é provada por teste unitário, e não por requisição.
- O histórico e a consulta autorizada dele são da I08.
- As consultas e o isolamento entre perfis dependem do host e do banco, então foram validados à mão, além dos testes unitários.
- Na máquina de desenvolvimento, o Smart App Control do Windows pode bloquear a DLL de testes e gerar um falso "teste falhou" no script local; o score oficial é o do workflow desta PR.

## Impacto em segurança e autorização
- A decisão sobre cada despesa combina role, dono e estado no serviço, e não só no atributo de role.
- Ninguém aprova, reprova nem paga a própria despesa, mesmo acumulando roles.
- Recursos fora do escopo de leitura respondem 404, igual a um inexistente, sem revelar que existem.
- O Auditor lê tudo e nunca escreve; o Admin sozinho não ganha acesso funcional.
- Nenhuma credencial versionada.

## Checklist
- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
```

## I07: Aprovar e reprovar com justificativa

**Status:** concluída e mergeada na `main` pela PR #7, no commit `b898f42` (pipeline oficial da PR 100/100, execução #37; na `main`, execução #40, 100/100, com Gitleaks 8.30.1 e sem bloqueantes). O workflow da branch também passou com 100/100 e Gitleaks a cada push (execuções #34 e #35, entre outras).

**Branch:** `i07-approve-reject`.

### O que foi feito

- **Regra única de existência para as ações que escrevem:** `ExpenseAccess.Evaluate` não usa mais o escopo de leitura. Ordem: role (403), despesa inexistente (404), dono (403), estado (409), para editar, enviar, aprovar, reprovar e pagar. `ExpenseScope.Allows` (sem uso) foi removido. Novo `IExpenseRepository.FindByIdAsync` (busca só por `Id`, com tracking); `FindVisibleAsync` ficou só nas leituras.
- **`PUT` e envio:** passam a carregar por identificador. Despesa de outro dono dá 403 (inclusive para Employee sem outra role), inexistente dá 404 e fora de `Draft` dá 409.
- **Conflito genérico:** `NotDraft` virou `WrongState`; o título do 409 vem do controller por ação.
- **Justificativa:** `ExpenseRules.ValidateRejectionReason` (aparada, 10 a 500), `RejectionReasonAttribute` e `RejectExpenseRequest` (só `Reason`).
- **Decisão:** `ExpenseService.ApproveAsync` e `RejectAsync` com um caminho privado único (`DecideAsync`): role, justificativa (só no reject), busca por identificador, `Evaluate`, `Status`, linha de histórico (ator do token, horário do `TimeProvider`, estado anterior e novo, `Reason`) e um único `SaveChanges`. O conflito de concorrência vira `WrongState`.
- **Endpoints:** `POST /api/expenses/{id}/approve` (sem corpo) e `/reject` (corpo `{"reason": "..."}`), ambos `[Authorize(Roles = Approver)]`.
- **Testes:** 486 no total, 136 novos (28 na matriz de `ExpenseAccess`, 9 de edição e envio, 14 de `ExpenseRules`, 12 de `RejectExpenseRequest`, 30 de aprovação e 43 de reprovação). Três propriedades novas na matriz protegem a regra: nunca há 404 para despesa existente, a decisão depende só da role da ação, do dono e do estado, e o que se escreve está dentro do que se lê.
- **Documentação:** `CLAUDE.md` (seção Decisions), README (endpoints, decisões, detalhe da I07 e as seções da I04 a I06 que falavam de 404 para despesa alheia) e este arquivo.

### Decisões que afetam as próximas issues

- **Respostas do professor (Teams):** em toda ação que escreve, despesa existente em outro estado dá 409 (inclusive `Draft`); o 404 é só para despesa inexistente; despesa de outro dono dá 403 (o erro é de auth/authz, não de not found); a leitura continua com 404 fora do escopo.
- **Pagamento (I08):** `Pay` já está na regra de existência e nos testes da matriz: Finance sobre `Draft`, `Submitted`, `Rejected` ou `Paid` dá 409. Falta o serviço de pagamento, o `PaymentRecord`, o endpoint e o histórico.
- **Justificativa:** campo `reason`, aparado, 10 a 500, só no histórico; `ExpenseResponse` não a devolve.
- **Título do 409:** "The expense is not a draft." (`PUT`, envio) e "The expense is not submitted." (aprovar, reprovar). Para o pagamento, usar um título próprio, sem revelar o estado.
- **Observação do 415:** `POST` com `[FromBody]` sem corpo e sem `Content-Type: application/json` devolve 415, não 400 (padrão do ASP.NET, igual a `/login` e `/register`). Com o cabeçalho e corpo vazio, a resposta é 400.

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 486 aprovados. `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks`: 100/100, só o FIAP0002 informativo.
- **Mutação:** 10 de 10 mutações pegas (dono, estado, escopo em `Evaluate`, escopo em edição e envio, escopo na decisão, justificativa sem aparar, histórico pulado, `catch` de concorrência, limites 9 e 501). Cada arquivo foi restaurado com a árvore limpa, com `dotnet build --no-incremental` antes de testar.
- **HTTP em banco temporário (senha aleatória só em memória):** 60 cenários OK, incluindo aprovar, repetir, reprovar com `reason` válido e inválido, `Draft` e `Paid` com 409, id inexistente com 404, autoaprovação com 403, token antigo com 401, `PUT` e envio de despesa alheia com 403 (também Employee mais Auditor) e `GET` fora do escopo com 404.
- **Concorrência:** 10 de 10 rodadas com dois Approvers aprovando a mesma despesa deram um 200 e um 409, sem "database is locked" nem 500.
- **Banco:** histórico `Created`, `Submitted` e `Approved` (ou `Rejected`) com ator, estado anterior e novo e horário certos; `Reason` aparada; 13 `Approved` e 3 `Rejected` no total, nenhuma despesa com duas decisões; contagem de histórico inalterada nas requisições que deram 400, 403, 404 ou 409.
- **SQL:** a busca por identificador é `WHERE "Id" = @id LIMIT 1`; o `UPDATE` leva `AND "Status" = @p2`; o `INSERT` do histórico vem imediatamente antes dele, no mesmo `SaveChanges` (35 de 35).

### Pendências

- **Feitos:** PR #7 aberta (`I07: Aprovar e reprovar com justificativa`), número no README e score oficial registrado (execução #37, 100/100, com Gitleaks 8.30.1, sem bloqueantes e sem achados).
- **Merge:** feito com "Create a merge commit", e a branch foi mantida (`b898f42` na `main`).
- **Superado na I08:** o serviço de pagamento, o `PaymentRecord`, `POST /api/expenses/{id}/pay` e `GET /api/expenses/{id}/history` foram entregues (a seção "Pagamento (I08)" das decisões acima descrevia o que faltava). O pagamento seguiu a regra de existência (409 para `Draft`, `Submitted`, `Rejected` e `Paid`), sem mudança em `ExpenseAccess`.

### Como validar

```shell
unset ConnectionStrings__ExpenseHub        # PowerShell: Remove-Item Env:ConnectionStrings__ExpenseHub
dotnet build ./sources/ExpenseHub.slnx --no-incremental   # 0 avisos, 0 erros
dotnet test ./sources/ExpenseHub.slnx                     # 486 aprovados
```

Para as requisições, use um banco fora do repositório e uma senha do Admin gerada na hora (nunca em arquivo): `ConnectionStrings__ExpenseHub="Data Source=<arquivo temporário>"`, `Seed__Admin__Password=<senha gerada>`, `dotnet ef database update`, `dotnet run`. Depois siga a seção "Como validar" do detalhe da I07 no README e apague o banco e as variáveis.

### Modelo de PR

Título: `I07: Aprovar e reprovar com justificativa`

```text
Implementa a aprovação e a reprovação (com justificativa) de despesas enviadas e unifica a regra de resposta das ações que escrevem.

Issue: Racass/checkpoint-csharpracass-expensehub#7

## Resumo técnico
- POST /api/expenses/{id}/approve e /reject, só para Approver que não é dono; estado e histórico (ator, horário, justificativa) na mesma gravação, com o Status como token de concorrência.
- Regra única de existência em ExpenseAccess: 404 só para despesa inexistente, 403 para despesa de outro dono e 409 para qualquer estado errado, em editar, enviar, aprovar, reprovar e pagar. O escopo de leitura vale só para as leituras.
- Justificativa (reason) aparada, de 10 a 500 caracteres, só no histórico.

## Decisões e concessões
- Respostas do professor no Teams: 409 para despesa existente em outro estado (inclusive Draft), 404 só para inexistente, 403 para despesa de outro dono.
- O Guid de uma despesa alheia passa a ser distinguível (403 ou 409) por quem tem a role da ação; o Guid não é adivinhável e a resposta não revela conteúdo nem estado.
- Pay já segue a regra; o endpoint de pagamento é da I08.

## Como validar
dotnet build ./sources/ExpenseHub.slnx --no-incremental
dotnet test ./sources/ExpenseHub.slnx
(requisições em banco temporário: ver o detalhe da I07 no README)

## Evidências
- Build com 0 avisos e 0 erros; 486 testes unitários aprovados (136 novos).
- Verificação de mutação: 10 de 10 mutações pegas pelos testes.
- 60 cenários HTTP em banco temporário; 10 rodadas simultâneas com um 200 e um 409.
- Score local do pipeline de qualidade: 100/100 (com -SkipGitleaks); pipeline code-quality da branch: 100/100, com Gitleaks.

## Impacto em segurança e autorização
- PUT e envio de despesa alheia passam de 404 para 403; decisões e leituras seguem as roles e o dono.
- Nenhum segredo versionado; a senha usada nos testes manuais era aleatória e só existiu em memória.

## Checklist
- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
```

## I08: Pagamento e histórico

**Status:** concluída na branch `i08-payment-history`, com a PR #8 aberta e o pipeline oficial em 100/100 (execução #46, com Gitleaks 8.30.1, sem bloqueantes e sem achados). O workflow da branch também passou com 100/100 e Gitleaks, sem achados, a cada push (execuções #41 a #45). Passa a mergeada depois do merge.

**Branch:** `i08-payment-history`, a partir da `main` em `b898f42` (I07 mesclada pela PR #7; execução #40, 100/100, com Gitleaks 8.30.1 e sem bloqueantes).

### O que foi feito

- **Conflito do índice único:** `SqliteConstraintErrors.IsUniqueConstraint(int, int)` (função pura, verdadeira só para o código 19 com o estendido 2067) e `ExpenseRepository.SaveChangesAsync` com dois `catch`: `DbUpdateConcurrencyException` e `DbUpdateException` cuja causa é uma violação única do SQLite. Os dois viram `ExpenseConflictException`, e o serviço responde `409`.
- **Pagamento:** `ExpenseService.PayAsync(caller, id)` pelo mesmo `DecideAsync` de aprovar e reprovar, agora generalizado (`StatusAfter` e `HistoryActionOf` por ação). Confere a role `Finance`, carrega por `FindByIdAsync`, decide por `ExpenseAccess.Evaluate` (403, 404, 403, 409), muda o `Status` para `Paid`, cria o `PaymentRecord` (`ActorId` do token e `PaidAtUtc` do `TimeProvider`, o mesmo instante da linha de histórico) e acrescenta a linha `Paid` com o estado anterior real. Um único `SaveChangesAsync`.
- **Histórico:** `IExpenseRepository.FindVisibleWithHistoryAsync(id, scope)` (sem rastreamento, `Where(scope.Predicate)` antes do `Include` do histórico) e `ExpenseService.GetHistoryAsync(caller, id)`, que devolve as entradas ordenadas por `OccurredAtUtc` e depois `Id`, ou `null` para despesa inexistente, fora do escopo ou com escopo vazio (Admin sozinho e sem role, sem consultar o banco).
- **Respostas:** `ExpenseHistoryResponse` (`id`, `expenseId`, `action`, `actorId`, `occurredAtUtc`, `previousStatus`, `newStatus`, `reason`, `changes`; sem e-mail) e `PaymentResponse` (`expenseId`, `status`, `actorId`, `paidAtUtc`).
- **Endpoints:** `POST /api/expenses/{id}/pay` (`[Authorize(Roles = Finance)]`, sem corpo, `409` com o título "The expense is not approved.") e `GET /api/expenses/{id}/history` (mesmas roles de leitura).
- **Testes:** 563 no total, 77 novos: 6 de `SqliteConstraintErrors`, 32 do pagamento, 32 do histórico, 4 de `ExpenseHistoryResponse` e 3 de `PaymentResponse`. O `FakeExpenseRepository` ganhou falha programável na gravação e o registro do estado em cada tentativa de gravação.
- **Documentação:** `CLAUDE.md` (seção Decisions), README (endpoints, testes, qualidade, decisões, solução de problemas, o detalhe da I08 e as seções anteriores que falavam do pagamento e do histórico como futuros) e este arquivo. Nenhuma migration e nenhum pacote novo.

### Decisões que afetam as próximas issues

- **Histórico segue a visibilidade da despesa:** confirmado pelo professor no Teams ("quem vai ler elas vai ser o auditor"). O Approver não lê o histórico depois de decidir, e o Finance só lê o de `Approved` e `Paid`.
- **Duas barreiras no pagamento simultâneo:** o `Status` como token de concorrência barra a corrida real (o `UPDATE` leva `WHERE Status = <valor lido>`); o índice único em `PaymentRecords.ExpenseId` é uma defesa adicional, provada por um registro inserido à força e pelo controle negativo (`409` com a conversão, `500` sem ela).
- **Só o índice único vira conflito:** chave estrangeira, `CHECK`, gatilho e banco ocupado não são convertidos e viram `500`. A classificação é específica do SQLite.
- **Pagamento sem corpo e `PaymentResponse`:** ator, estado e horário nunca vêm do cliente; `ExpenseResponse` continua sem pagamento.
- **`PaymentRecord` sem valor:** os requisitos só citam a entidade, sem campos. Guardar o valor pago exigiria uma migration e ficou fora da issue.
- **Falha de persistência que não é conflito:** sobe como `500` (comportamento padrão do ASP.NET), a transação é desfeita e a despesa continua `Approved`. Não foi alterado.
- **Anotado e não feito (fora da I08):** o seed que impede a aplicação de subir sem a senha do Admin, a diferença de tempo no login e a paginação da listagem e do histórico.

### O que foi medido (EF Core 10.0.12, SQLite, banco temporário fora do repositório)

Script descartável no diretório temporário da sessão, 60 verificações, todas conforme o esperado; senha aleatória só em memória, nunca impressa nem gravada (0 ocorrências nos logs da aplicação):

- **Pagamento:** `fin1` paga `Approved` de `emp1` (`200`, `Paid`); repetir `409`; `Draft`, `Submitted`, `Rejected` e `Paid` `409`; `id` inexistente e `id` que não é Guid `404`; `finemp` (Employee mais Finance) pagando a própria `Approved` e o próprio `Draft` `403`; Employee, Approver, Auditor, Admin e sem role `403`; sem token `401`.
- **Pagamento simultâneo:** `fin1` e `fin2` na mesma despesa, 10 rodadas: sempre um `200` e um `409`, uma única linha em `PaymentRecords` e uma única linha `Paid`, sem `500` nem "database is locked".
- **Falha forçada:** com um gatilho `BEFORE INSERT` em `PaymentRecords` que aborta, o pagamento dá `500`, e a despesa continua `Approved`, sem `PaymentRecord` e sem linha `Paid`; sem o gatilho, o mesmo pagamento dá `200`.
- **Histórico:** o dono lê o completo de uma despesa criada, editada, enviada, aprovada e paga, em ordem e com ator e horários certos, e o de uma reprovada, com a justificativa; outro Employee `404`; Approver lê `Submitted` e recebe `404` em `Approved`; Finance lê `Approved` e `Paid` e recebe `404` em `Submitted`; Auditor lê todos; Admin sozinho e sem role `403`; sem token `401`; inexistente e `id` que não é Guid `404`; o corpo não tem e-mail.
- **Banco:** despesas `Paid`, linhas em `PaymentRecords` e linhas `Paid` no histórico em igual número; nenhuma despesa com dois pagamentos; `ActorId` de quem pagou e `PaidAtUtc` perto do horário da requisição.
- **SQL:** a busca do pagamento é `WHERE "e"."Id" = @id LIMIT 1`, sem escopo; a do histórico leva o predicado do escopo antes do `Id` e o `LEFT JOIN` com `ExpenseHistories` (sem predicado só no Auditor); o `UPDATE` leva `WHERE "Id" = @p1 AND "Status" = @p2 RETURNING 1`, na ordem `INSERT` do histórico, `UPDATE` da despesa e `INSERT` do `PaymentRecord`. Os únicos comandos que falharam foram dois `INSERT` em `PaymentRecords`, dos dois cenários forçados.
- **Mutação:** 8 mutações de código, todas pegas pelos testes (sem `PaymentRecord`; sem mudar o `Status`; sem a linha de histórico; pagamento carregando pelo escopo de leitura; sem a captura do conflito nas decisões; código 2067 trocado por 2068; histórico lido por `FindByIdAsync`; histórico sem ordenação), mais 1 controle negativo por HTTP, que tira o `catch` da violação do índice único do repositório (`500` sem ele, `409` com ele). Cada arquivo foi restaurado com `git restore` e a árvore limpa, com `dotnet build --no-incremental` antes de testar.

### Desvios e cuidados

- **A hipótese do plano estava errada:** o plano dizia que o pagamento perdedor falharia no índice único. O log de SQL mostrou a ordem `INSERT` do histórico, `UPDATE` da despesa (com o token) e `INSERT` do `PaymentRecord`, então o perdedor falha no `UPDATE`. A conversão do índice único continua no código como defesa adicional e foi provada à parte.
- **Estimativa de testes:** o plano previa cerca de 80 testes novos (35 de histórico); saíram 77 (32 de histórico), porque `ExpenseStatus` tem 5 estados, e não 6.
- **Avisos corrigidos na causa:** CA1826 (`history[^1]` no lugar de `.Last()` em lista indexável) e CA1861 (array constante de argumento virou variável local).
- **Lições anteriores seguidas:** nenhum literal atribuído a nome com `password`, `token` ou `secret`; credenciais de teste montadas em tempo de execução; um tipo por arquivo.
- **Mutação em arquivo de produção:** o `git restore` foi sempre só do arquivo mutado, com a árvore limpa antes, e o `dotnet build --no-incremental` evitou reaproveitar DLL quebrada (lição da I06).

### Evidências

- `dotnet build --no-incremental`: 0 avisos e 0 erros. `dotnet test`: 563 aprovados.
- `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks`: 100/100, 20 em cada categoria, sem bloqueantes, só o FIAP0002 informativo. Execuções #41 a #45 do workflow da branch: 100/100, com Gitleaks e sem achados.

### Pendências

- **Feitos:** PR #8 aberta (`I08: Pagamento e histórico`), número na tabela de issues do README e score oficial registrado na linha da I08 de Qualidade (execução #46, 100/100, com Gitleaks 8.30.1, sem bloqueantes e sem achados).
- **Merge:** marcar "Pipeline analisado" na descrição da PR, fazer o merge com "Create a merge commit" e manter a branch.

### Como validar

```shell
unset ConnectionStrings__ExpenseHub        # PowerShell: Remove-Item Env:ConnectionStrings__ExpenseHub
dotnet build ./sources/ExpenseHub.slnx --no-incremental   # 0 avisos, 0 erros
dotnet test ./sources/ExpenseHub.slnx                     # 563 aprovados
```

Para as requisições, use um banco fora do repositório e uma senha do Admin gerada na hora (nunca em arquivo): `ConnectionStrings__ExpenseHub="Data Source=<arquivo temporário>"`, `Seed__Admin__Password=<senha gerada>`, `dotnet ef database update`, `dotnet run`. Depois siga a seção "Como validar" do detalhe da I08 no README e apague o banco e as variáveis.

### Modelo de PR

Título: `I08: Pagamento e histórico`

```text
Implementa o pagamento de despesas aprovadas e a consulta do histórico de cada despesa.

Issue: Racass/checkpoint-csharpracass-expensehub#8

## Resumo técnico
- POST /api/expenses/{id}/pay, só para Finance que não é dono, sem corpo: Approved para Paid, com o PaymentRecord (ator do token e horário do servidor), a mudança de estado e a linha Paid do histórico na mesma gravação. Pagamento repetido ou simultâneo dá 409.
- GET /api/expenses/{id}/history com a visibilidade da despesa: fora do escopo é 404, o Auditor lê todos e o Admin sozinho recebe 403. Entradas da mais antiga para a mais nova, só com identificadores internos, sem e-mail.
- Pagamento simultâneo com duas barreiras: o Status como token de concorrência barra a corrida real, e o índice único em PaymentRecords.ExpenseId é uma defesa adicional. O repositório converte as duas falhas em conflito (409).
- O pagamento usa o mesmo caminho de aprovar e reprovar no serviço; o histórico usa o escopo de leitura.
- 77 testes unitários novos (total de 563), com fakes escritos à mão; sem banco. Nenhuma migration nem pacote novo.

## Decisões e concessões
- O professor confirmou no Teams que o histórico segue a visibilidade da despesa; por isso o Approver não lê o histórico depois de decidir.
- Só a violação do índice único vira conflito entre os erros do SQLite; as demais falhas de persistência são 500 e desfazem a gravação.
- O PaymentRecord não guarda o valor pago, porque os requisitos não o pedem.
- A hipótese inicial era que o pagamento perdedor cairia no índice único; a evidência mostrou que é o token de Status.

## Como validar
dotnet build ./sources/ExpenseHub.slnx --no-incremental
dotnet test ./sources/ExpenseHub.slnx
(requisições em banco temporário: ver o detalhe da I08 no README)

## Evidências
- Build com 0 avisos e 0 erros; 563 testes unitários aprovados (77 novos).
- Verificação de mutação: 8 mutações de código pegas pelos testes, mais 1 controle negativo por HTTP (500 sem a conversão do índice único, 409 com ela).
- 60 cenários HTTP em banco temporário; 10 rodadas de pagamento simultâneo com um 200 e um 409; falha forçada por gatilho sem estado nem histórico divergentes.
- Score local do pipeline de qualidade: 100/100 (com -SkipGitleaks); pipeline code-quality da branch: 100/100, com Gitleaks.

## Impacto em segurança e autorização
- Só Finance paga, nunca o dono; ator, estado e horário vêm do token e do servidor.
- O histórico respeita o escopo de leitura aplicado na consulta; nada fora dele é carregado, e a resposta não traz e-mail.
- Nenhum segredo versionado; a senha usada nos testes manuais era aleatória e só existiu em memória.

## Checklist
- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
```

## Roteiro por issue

Serve para quem começa a próxima issue sem ter acompanhado as anteriores.

1. **Ler, nesta ordem:** o README (estado atual e tabela de issues), este arquivo, o `CLAUDE.md`, a issue no backlog central (`Racass/checkpoint-csharpracass-expensehub#N`) e, em `docs/`, `REQUISITOS.md`, `MATRIZ-AUTORIZACAO.md` e `code-quality-rules.md`.
2. **Preparar a branch** a partir da `main` atualizada:

```shell
git switch main
git pull
dotnet tool restore
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
git switch -c iNN-nome-curto
```

   Nomes sugeridos nas issues: `i02-identity-auth`, `i03-user-roles`, `i04-expense-draft`, `i05-submit-query`, `i06-ownership-access`, `i07-approve-reject`, `i08-payment-history`, `i09-unit-tests`, `i10-code-quality`.
3. **Com o Claude Code** (recomendação da equipe): abrir na raiz, na branch da issue; começar em modo Plan e ler o plano antes de aprovar; aprovar as edições uma a uma ou com o modo de aceitar edições, sem auto mode; exigir zero avisos sem supressão. O agente não faz commit nem push (o `CLAUDE.md` proíbe).
4. **Revisar e commitar:** ler o `git diff`, usar `git add` arquivo por arquivo (nunca `git add .`) e fazer commits pequenos, em inglês, com `(I0N)` no fim da primeira linha.
5. **Antes do push:** `dotnet build ./sources/ExpenseHub.slnx --no-incremental` sem avisos, `dotnet test ./sources/ExpenseHub.slnx` e `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks` com 100/100, medido depois dos commits.
6. **Atualizar a documentação na própria branch:** a linha "Estado atual", a tabela de issues, a tabela de endpoints e a seção da issue no README, além deste arquivo. O número da PR só existe depois de abri-la, então preencha-o num commit seguinte, na mesma branch.
7. **PR:** título `I0N: Título da issue`, descrição no modelo acima, citando `Racass/checkpoint-csharpracass-expensehub#N` sem `Closes`, `Fixes` ou `Resolves`. Esperar o workflow `code-quality` ficar verde. O score oficial está na aba Actions, na execução da PR, em Summary e no artefato `code-quality-report`.
8. **Merge:** marcar "Pipeline analisado" na descrição, escolher "Create a merge commit" (nunca Squash nem Rebase) e não apagar a branch.

## Lições da equipe

- **FIAP1002 (segredo versionado):** é uma checagem própria do `scripts/Invoke-CodeQuality.ps1` (função `Test-CustomSecrets`), e não do Gitleaks. Procura, em arquivos rastreados (`json`, `yml`, `xml`, `config`, `props`, `targets`, `cs` e `env`), nomes com `password`, `passwd`, `pwd`, `senha`, `secret`, `token` ou `api_key` seguidos de um valor literal de 4 ou mais caracteres (em `.cs`, a forma `nome = "valor"`). Um achado limita o score a 9 e derrubaria o I10 (25% da nota) para cerca de 0,2 ponto. Em arquivo ainda não rastreado, o mesmo achado vira FIAP2102 (aviso, 2 pontos de Segurança).
- **O caso da I02:** os literais entraram no commit `8b0278f` (testes) e saíram em `aac8de8`. Padrão adotado depois: credencial de teste montada em tempo de execução, por exemplo `new string('a', 12)`, ou lida de configuração. Nenhum literal atribuído a campo ou variável com esses nomes, nem em testes.
- **O `-SkipGitleaks` não esconde a FIAP1002:** ele só dispensa o Gitleaks, que varre o histórico do Git. Rodar o script depois do `git add` ou do commit e antes do push, ler os IDs dos findings (não só o score) e conferir o CI da PR antes do merge.
- **Build incremental:** `dotnet ef` com `--no-build` usa o assembly antigo, e o build incremental pode reaproveitar uma DLL quebrada. Depois de qualquer `git restore` ou mutação de teste, rodar `dotnet build --no-incremental` antes de testar.
- **Banco:** `ConnectionStrings__ExpenseHub` exige o prefixo `Data Source=`; o script de migrations do SQLite não é idempotente; um arquivo `.db` vazio (0 bytes), deixado por uma execução sem migrations, pode ficar onde está e o `dotnet ef database update` o aproveita.
- **Commits direto na `main`, pelo site do GitHub:** `36307af`, `3192e5f`, `4937e7a` e `fdaf1fe`, só para preencher links de PR no README (autor "Pedro Almeida e Camacho"). Preferir sempre commits na branch da issue; os demais seguem o fluxo e o padrão combinado.

## Passagem para a I09

Responsável: Renan, em uma branch nova a partir da `main` com a I08 mesclada (nome sugerido no roteiro: `i09-unit-tests`). Critérios no backlog central (`Racass/checkpoint-csharpracass-expensehub#9`); leia-os antes de planejar, porque este arquivo não os copia.

- **Leitura:** README (estado atual, Testes e Qualidade), este arquivo, `CLAUDE.md` (seção Build & code-quality constraints e regras de testes), `docs/RUBRICA.md` e `docs/code-quality-rules.md`.
- **Base pronta:**
  - 563 testes unitários (MSTest 4) que passam, com build em 0 avisos e 0 erros e score 100/100.
  - Fakes escritos à mão: `FakeExpenseRepository` (com `FindByIdCalls`, `FindVisibleCalls`, `FindVisibleWithHistoryCalls`, falha programável na gravação e o estado visto em cada tentativa de gravação), `FixedTimeProvider`, `FakeUserAccountStore` e `FakeIdentitySeedStore`.
  - Regras puras testáveis sem EF: `ExpenseAccess`, `ExpenseRules`, `ExpenseVisibility`, `BrazilTime`, `MoneyConversion`, `UtcTicks`, `SecurityStampCheck` e `SqliteConstraintErrors`.
- **O que depende de host e banco e não tem teste unitário:** os controllers, o `ExpenseRepository` e o `UserAccountStore` com EF, o middleware de `SecurityStamp`, o seed no host e a conversão real do índice único. Foram validados à mão, com banco temporário, em cada issue. Ao planejar a I09, decida com o professor o que fazer com isso, sem usar EF Core InMemory nem SQLite em memória.
- **Atenção:**
  - Teste com nome que diga a regra checada, com casos válidos e inválidos.
  - Nenhum literal atribuído a campo, variável ou chave com `password`, `senha`, `secret`, `token` ou `api key`, nem em testes (FIAP1002 limita o score a 9); monte credenciais em tempo de execução.
  - Zero avisos sem supressão; depois de qualquer `git restore` ou mutação, `dotnet build --no-incremental` antes de testar.
  - Só testes unitários contam para a nota.
- **Depois da I09:** a I10 (Qualidade de Código, 25%) fecha o score, o README e o SHA final; remover a pasta `andamento/` e a regra que a cita no `CLAUDE.md` antes do SHA final.
- **Prazo:** quarta-feira, 14/10, às 23:59, no Teams.