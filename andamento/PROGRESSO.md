# PROGRESSO

Registro temporário do andamento por issue. Esta pasta será removida antes do SHA final. Leia este arquivo antes de começar uma issue e atualize-o ao terminar (junto com a linha "Estado atual", a tabela de status, a tabela de endpoints e a seção da issue no README).

## Prazo e decisões do professor

- **Prazo:** quarta-feira, 14/10/2026, às 23:59, no Teams (confirmado pelo professor; o ENUNCIADO e o README dele ainda dizem 13/10). Vale planejar para fechar antes e deixar folga para o score, o README e o SHA final.
- **ExpenseCategory:** nenhuma issue cobra categoria. Fica só a entidade mínima (`Id` e `Name`), sem endpoint, seed nem vínculo com `Expense`.
- **Testes unitários:** sem EF Core InMemory e sem SQLite em memória. O acesso a dados é simulado por interface de repositório injetada, com fakes ou mocks. O banco em si seria coberto por testes funcionais, que são opcionais e não pontuam.
- **Códigos de status:** o professor pediu que a equipe decida pelo significado de cada código (401 é autenticação, 403 é autorização, 404 é recurso inexistente ou fora do escopo, 409 é conflito de estado). As escolhas atuais estão na seção Decisions do `CLAUDE.md`. Documentar o raciocínio no README quando a I06 for concluída.

## Divisão e marcos

- **Responsáveis previstos** (tabela do README): Renan fica com a I01 e da I07 à I10; Pedro fica com da I02 à I06.
- **Meta proposta, a confirmar entre os dois:** Pedro fecha da I02 à I06 até domingo, 04/10, que serve de ponto de controle. A I07 e a I08 dependem do desenho de autorização da I06, então quem terminar a I06 avisa o outro na hora. Se a I06 atrasar, dividir o restante em vez de esperar.
- **Reta final:** funcionalidades prontas até domingo, 11/10; segunda e terça (12 e 13/10) para score, README e SHA final; entrega até quarta, 14/10, com folga.
- Quem sentir aperto avisa cedo. Cada issue mergeada na `main` libera a seguinte.

## Avisos para a I02 a I06

Estes pontos vêm da I01 e evitam retrabalho:

- (a) **I02, Identity:** ao herdar de `IdentityDbContext`, chame `base.OnModelCreating(modelBuilder)` antes das `ApplyConfiguration`. Hoje `ExpenseHubDbContext` herda de `DbContext` e não chama a base.
- (b) **I04, DTO de despesa:** rejeitar valores com mais de duas casas decimais. `MoneyConversion.ToCents` arredonda meio centavo para longe do zero, mas isso é só defesa; o conversor nunca deve arredondar entrada de usuário.
- (c) **I04:** as interfaces de repositório (por exemplo `IExpenseRepository`) nascem na I04. Serviços nunca recebem `DbContext`.
- (d) **Rotas:** `Expense.Id` é `Guid`. Use `{id:guid}` nas rotas de `/api/expenses/{id}`.
- (e) **Instantes:** qualquer novo `DateTimeOffset` persistido precisa de `UtcDateTimeOffsetConverter` (ticks UTC em `INTEGER`), senão `OrderBy`, `Max` e `Where` falham no SQLite. Atenção na I02: `IdentityUser.LockoutEnd` é `DateTimeOffset?`; só precisa de conversor se alguma consulta ordenar ou filtrar por ele.
- (f) **Testes unitários:** sem banco, sem EF Core InMemory e sem SQLite em memória. Use fakes escritos à mão dos repositórios.
- (g) **Commits:** em inglês, no padrão convencional, com o identificador da issue entre parênteses no fim da primeira linha, por exemplo `feat(identity): add login endpoint (I02)`. Não use `#2` no texto, porque o GitHub liga a um item do repositório da equipe. Acrescente a frase "Commit messages in English" ao `CLAUDE.md` na primeira edição que fizer nele.
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

**Status:** implementada e commitada na branch; aguardando o score do pipeline e a abertura da PR. Só passa a "Concluída" (README e este arquivo) depois do merge.

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

- **Score do pipeline:** o `pwsh` (PowerShell 7) não está instalado na máquina do Pedro, então `pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks` ainda não foi rodado para a I02. Rodar antes do push final, ou conferir o score do workflow na PR. O README só registra build e testes para a I02.
- **Bloqueio por tentativas (`lockoutOnFailure`):** ligado no código, mas não foi testado à mão com várias senhas erradas.
- **Número da PR:** preencher na tabela do README e no texto da PR, em um commit seguinte, depois de abrir a PR.
- **Status:** trocar "Implementada, aguardando PR" por "Concluída" na tabela do README depois do merge, e marcar aqui.
- **Merge:** "Create a merge commit", sem Squash nem Rebase, e sem apagar a branch.

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

**Status:** implementada na branch `i03-user-roles`, sem commit ainda (o Pedro revisa o diff e commita); aguardando commits, pipeline oficial e PR. Só passa a "Concluída" depois do merge.

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

- **Commits:** nada commitado. Sugestão de divisão: store e serviço, cadastro, rotas de roles com o middleware, testes, documentação.
- **Score oficial:** conferir o workflow na PR (com Gitleaks) e preencher aqui, no README e no texto da PR.
- **Número da PR e status:** depois de abrir a PR, preencher o número e, depois do merge, trocar "Implementada, aguardando PR" por "Concluída" na tabela do README.
- **Linha da I02 no README:** continua "Implementada, aguardando PR" e a seção de qualidade diz que o score da I02 "é o da execução na PR". A I02 já foi mergeada (PR #2); trocar para "Concluída" e registrar o score oficial, num commit à parte.
- **Merge:** "Create a merge commit", sem Squash nem Rebase, e sem apagar a branch.

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

## Primeira issue do Pedro: I02

Critérios no backlog central (`Racass/checkpoint-csharpracass-expensehub#2`). Atenção aos avisos (a), (e) e (g). A senha inicial do Admin vem de configuração segura (user-secrets ou variável de ambiente) e nunca de arquivo versionado; o `.gitignore` já ignora `appsettings.Development.json`, `appsettings.Local.json` e `secrets.json`.