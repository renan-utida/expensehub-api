# ExpenseHub

[![code-quality](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml/badge.svg)](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml)

API REST corporativa de reembolso de despesas, desenvolvida para o Checkpoint 2 de C# da FIAP (turma 3ESPW). A aplicação usa ASP.NET Core (.NET 10) e Entity Framework Core em banco relacional. O trabalho é entregue por issue, uma por vez, e este README cresce junto com o código: cada seção descreve apenas o que já existe.

Estado atual: a fundação (I01), a autenticação (I02), o cadastro com a administração de roles (I03) e a criação e edição de rascunhos de despesa (I04) e o envio, a listagem e a consulta por perfil (I05) estão prontos: persistência com SQLite, ASP.NET Core Identity, cadastro público sem roles, login com token bearer, as cinco roles, a conta Admin inicial, a atribuição de roles pelo Admin e o `Employee` criando e editando os próprios rascunhos. Ainda não existem aprovação, reprovação, pagamento nem consulta do histórico; os endpoints atuais são `GET /health`, `POST /register`, `POST /login`, `GET /api/admin/users`, `PUT /api/admin/users/{id}/roles`, `POST /api/expenses`, `PUT /api/expenses/{id}`, `POST /api/expenses/{id}/submit`, `GET /api/expenses` e `GET /api/expenses/{id}`.

## Sumário

- [Equipe](#equipe)
- [Issues](#issues)
- [Endpoints](#endpoints)
- [Como rodar](#como-rodar)
- [Banco de dados](#banco-de-dados)
- [Arquitetura](#arquitetura)
- [Testes](#testes)
- [Qualidade](#qualidade)
- [Decisões de projeto](#decisões-de-projeto)
- [Solução de problemas](#solução-de-problemas)
- [Fora de escopo](#fora-de-escopo)
- [Uso de IA](#uso-de-ia)
- [Fluxo de trabalho da equipe](#fluxo-de-trabalho-da-equipe)
- [Detalhe da I01: Fundação da solução e Entity Framework Core](#detalhe-da-i01-fundação-da-solução-e-entity-framework-core)
- [Detalhe da I02: Identity, Admin e autenticação](#detalhe-da-i02-identity-admin-e-autenticação)
- [Detalhe da I03: Cadastro HTTP e gerenciamento de roles](#detalhe-da-i03-cadastro-http-e-gerenciamento-de-roles)
- [Detalhe da I04: Criar e editar rascunho](#detalhe-da-i04-criar-e-editar-rascunho)
- [Detalhe da I05: Enviar, listar e consultar](#detalhe-da-i05-enviar-listar-e-consultar)
- [Próximas issues](#próximas-issues)

## Equipe

| Nome | RM | GitHub |
|---|---|---|
| Renan Dias Utida | 558540 | [renan-utida](https://github.com/renan-utida) |
| Pedro Almeida e Camacho | 556831 | [Pedro-Camacho](https://github.com/Pedro-Camacho) |

## Issues

| Issue | Título | Peso | Status | Responsável | PR |
|---|---|---:|---|---|---|
| I01 | Fundação da solução e Entity Framework Core | 4% | Concluída | Renan | [#1](https://github.com/renan-utida/expensehub-api/pull/1) |
| I02 | Identity, Admin e autenticação | 9% | Implementada, aguardando PR | Pedro | [#2](https://github.com/renan-utida/expensehub-api/pull/2)|
| I03 | Cadastro HTTP e gerenciamento de roles | 8% | Implementada, aguardando PR | Pedro | [#3](https://github.com/renan-utida/expensehub-api/pull/3) |
| I04 | Criar e editar rascunho | 7% | Implementada, aguardando PR | Pedro | [#4](https://github.com/renan-utida/expensehub-api/pull/4) |
| I05 | Enviar, listar e consultar | 7% | Implementada, aguardando PR | Pedro | - |
| I06 | Ownership e matriz de acesso | 10% | A implementar | Pedro | - |
| I07 | Aprovar e reprovar com justificativa | 12% | A implementar | Renan | - |
| I08 | Pagamento e histórico | 8% | A implementar | Renan | - |
| I09 | Testes unitários | 10% | A implementar | Renan | - |
| I10 | Qualidade de Código | 25% | A implementar | Renan | - |

## Endpoints

| Método | Rota | Descrição | Issue |
|---|---|---|---|
| GET | `/health` | Confirma que a aplicação iniciou; não acessa o banco | Esqueleto inicial |
| POST | `/register` | Público. Recebe `email` e `password` e cria o usuário **sem nenhuma role**; qualquer role enviada no corpo é ignorada. `201` com `id`, `email` e `roles` vazia; entrada inválida ou senha fora da política: `400`; e-mail já cadastrado: `409` | I03 |
| POST | `/login` | Público. Recebe `email` e `password` e devolve o token bearer. Credencial inválida: `401`; corpo inválido: `400` | I02 |
| GET | `/api/admin/users` | Somente `Admin`. Lista `id`, `email` e `roles` dos usuários, ordenados por e-mail. Sem token: `401`; sem a role: `403` | I02 e I03 |
| PUT | `/api/admin/users/{id}/roles` | Somente `Admin`. Recebe `{"roles": [...]}` e **substitui** as roles do usuário: as listadas são atribuídas e as omitidas são removidas. Role desconhecida ou lista ausente: `400`; usuário inexistente: `404`; Admin removendo a própria role Admin: `403`. O usuário afetado precisa fazer login de novo | I03 |
| POST | `/api/expenses` | Somente `Employee`. Recebe `description`, `amount` e `expenseDate` e cria um rascunho (`Draft`) cujo dono é o usuário do token. `201` com a despesa e o cabeçalho `Location` (`/api/expenses/{id}`); dados inválidos: `400`; sem token: `401`; sem a role: `403` | I04 e I05 |
| PUT | `/api/expenses/{id}` | Somente `Employee`. **Substitui** `description`, `amount` e `expenseDate` de um rascunho do próprio usuário. `200` com a despesa; dados inválidos: `400`; inexistente ou de outro usuário: `404`; fora de `Draft`: `409`; sem token: `401`; sem a role: `403` | I04 |
| POST | `/api/expenses/{id}/submit` | Somente `Employee`. Envia um rascunho do próprio usuário: `Draft` para `Submitted`, sem corpo (o estado nunca vem do cliente). `200` com a despesa; inexistente ou fora do escopo de leitura: `404`; visível por outra role mas de outro dono: `403`; fora de `Draft`, inclusive reenvio e envio simultâneo: `409`; sem token: `401`; sem a role: `403` | I05 |
| GET | `/api/expenses` | `Employee`, `Approver`, `Finance` ou `Auditor`. Lista as despesas do escopo do perfil, da mais nova para a mais antiga: Employee as próprias, Approver as `Submitted`, Finance as `Approved` e `Paid`, Auditor todas; roles acumuladas somam os filtros. O Admin sozinho e quem não tem role recebem `403`; sem token: `401` | I05 |
| GET | `/api/expenses/{id}` | Mesmas roles da listagem. `200` com a despesa; inexistente ou fora do escopo: `404`, com a mesma resposta nos dois casos, para não revelar que o recurso existe; `403` para quem não lê despesas; `401` sem token | I05 |

Esta tabela ganha uma linha a cada endpoint implementado.

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

Apenas a estrutura de pastas atual:

```text
dotnet-tools.json                 ferramentas locais (dotnet-ef)
docs/                             especificação da disciplina
scripts/                          pipeline de qualidade local
sources/
  ExpenseHub.slnx
  ExpenseHub.Api/
    Program.cs                    inicialização, pipeline e GET /health
    appsettings.json
    Controllers/                  AuthController (POST /register e /login), AdminUsersController, ExpensesController
    Domain/
      Entities/                   Expense, ExpenseCategory, ExpenseHistory, PaymentRecord
      Enums/                      ExpenseStatus, ExpenseHistoryAction
    Expenses/                     ExpenseService (criar, editar, enviar e consultar), ExpenseRules (contrato de validação),
                                  ExpenseVisibility e ExpenseScope (o que cada perfil lê),
                                  BrazilTime, IExpenseRepository, DTOs (ExpenseRequest e ExpenseResponse) e MoneyAmount
    Identity/                     registro do Identity e do bearer, DTOs, AppRoles, o seed, UserAccountService
                                  (regras de cadastro e de roles) e a validação do SecurityStamp
    Persistence/
      ExpenseHubDbContext.cs
      ExpenseHubDbContextFactory.cs             usada só pelo dotnet ef
      PersistenceServiceCollectionExtensions.cs registro no DI e connection string
      Configurations/             mapeamento de cada entidade
      Converters/                 conversões de valor e de instante
      Migrations/                 InitialCreate, AddIdentity e snapshot (gerados)
      Repositories/               ExpenseRepository (EF Core)
  ExpenseHub.UnitTests/
    Expenses/                     testes das regras de despesa, do serviço e dos DTOs, com fake do repositório
    Identity/                     testes do seed, do cadastro, das roles e das validações, com fakes das stores
    Persistence/                  testes das conversões
```

As entidades são classes simples, sem regras de negócio por enquanto. A regra do seed fica em `IdentitySeeder`, que depende da interface `IIdentitySeedStore`; a implementação com `UserManager` e `RoleManager` é `IdentitySeedStore`. As regras de cadastro e de roles ficam em `UserAccountService`, que depende de `IUserAccountStore`; a implementação com EF Core e Identity é `UserAccountStore`. As regras de despesa ficam em `ExpenseService` e `ExpenseRules`, que dependem de `IExpenseRepository` e de um `TimeProvider`; a implementação com EF Core é `ExpenseRepository`. O serviço nunca recebe `DbContext`.

## Testes

```shell
dotnet test ./sources/ExpenseHub.slnx
```

- Somente testes unitários (MSTest 4), sem banco, rede ou serviço externo. Hoje são 250 testes: 13 da I01 (9 de `MoneyConversion` e 4 de `UtcTicks`), 25 da I02 (14 de `IdentitySeeder` e 11 de `LoginRequest`), 48 da I03 (25 de `UserAccountService`, 11 de `RegisterRequest`, 4 de `UpdateUserRolesRequest` e 8 de `SecurityStampCheck`), 110 da I04 (42 de `ExpenseRules`, 4 de `BrazilTime`, 26 de `ExpenseService` na criação, 20 na edição e 18 de `ExpenseRequest`) e 54 da I05 (19 de `ExpenseVisibility`, 18 de envio e 16 de consulta, mais 1 de conflito na edição, que ficou junto dos testes de edição), contando cada caso de `DataRow`.
- Os testes da I01 chamam funções estáticas puras. Os do seed, de `UserAccountService` e de `ExpenseService` usam fakes escritos à mão das stores e do repositório, e um relógio fixo escrito à mão (`TimeProvider`). Nenhum usa tipos do EF Core.
- O login, o cadastro, o `401`, o `403`, a invalidação do token e a gravação das despesas dependem do host e do banco, então foram validados à mão (seções da I02, da I03 e da I04), e não por teste unitário.
- Não usamos EF Core InMemory nem SQLite em memória nos testes unitários.

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
- I02: `dotnet build --no-incremental` com 0 erros e 0 avisos e `dotnet test` com 38 testes aprovados. O score do pipeline da I02 é o da execução na PR.
- I03: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 86 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`.
- I04: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 196 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`.
- I05: `dotnet build --no-incremental` com 0 erros e 0 avisos, `dotnet test` com 250 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes), com `-SkipGitleaks`. Depois dos commits da I04, o Smart App Control do Windows chegou a bloquear a DLL de testes nesta máquina e o script mostrou 96 por um falso "teste falhou"; o score oficial é o do workflow na PR.
- Nenhum aviso é suprimido (sem `#pragma warning disable`, `[SuppressMessage]` nem `NoWarn`).

## Decisões de projeto

- **SQLite:** arquivo local, sem servidor, adequado a um trabalho acadêmico. Trocar o provider exigiria gerar uma nova migration e revisar as conversões e o `CHECK`.
- **Valor em centavos:** `Expense.Amount` é `decimal` no domínio, mas a coluna `AmountCents` guarda um inteiro (`MoneyConversion`). A razão é ter um `CHECK` numérico na faixa do valor (de 1 a 214748364700 centavos, ou seja, de R$ 0,01 a R$ 2.147.483.647,00) e ordenação nativa, independente da collation do EF. `ToCents` arredonda meio centavo para longe do zero; o conversor não valida entrada de usuário.
- **Ticks UTC:** instantes (`CreatedAtUtc`, `OccurredAtUtc`, `PaidAtUtc`) são `DateTimeOffset` no domínio e `INTEGER` de ticks UTC no banco (`UtcTicks`). A leitura sempre volta com offset zero.
- **`Guid` em `Expense.Id`:** identificador não sequencial gerado pelo servidor. `ExpenseHistory` e `PaymentRecord` usam inteiro autoincrementado.
- **Usuário sem chave estrangeira:** `OwnerId` e `ActorId` são texto (até 450 caracteres) e ainda não têm FK, porque o Identity só entra na I02.
- **Um pagamento por despesa:** índice único em `PaymentRecords.ExpenseId`, como segunda barreira além da regra de negócio.
- **Categoria mínima:** `ExpenseCategory` tem só `Id` e `Name`, sem endpoint, seed nem vínculo com `Expense`.
- **Repositórios na I04:** as interfaces de repositório nascem na I04, junto com o primeiro serviço.
- **Bearer nativo do Identity:** o token vem do esquema `IdentityConstants.BearerScheme`, sem JWT. Não há chave de assinatura para guardar, e o pacote extra de JWT não é necessário. O `MapIdentityApi` não é usado, para que cadastro e login fiquem sob controle da equipe.
- **Login sem vazar contas:** e-mail inexistente, senha errada e conta bloqueada respondem o mesmo `401`, e há bloqueio por tentativas repetidas (`lockoutOnFailure`).
- **`401` e `403`:** sem token ou com token inválido a resposta é `401`; autenticado sem a role exigida é `403`. Ambos saem como `ProblemDetails` (`AddProblemDetails` e `UseStatusCodePages`). A regra por status code completa, com ownership e estado, é da I06.
- **Seed que falha cedo:** sem a senha do Admin a aplicação não inicia. Um erro de configuração fica visível na hora, em vez de gerar uma API no ar sem Admin. A regra do seed é uma classe simples atrás de `IIdentitySeedStore`, testada sem banco.
- **Um único Admin:** o seed só cria o Admin quando nenhum usuário está na role `Admin`. O Admin não recebe nenhuma role funcional de despesa.
- **Cadastro sem roles:** `RegisterRequest` não tem nenhum membro de role, então o que o cliente enviar a mais é ignorado, e `IUserAccountStore.CreateUserAsync` nem recebe role. O usuário nasce sem roles e só um Admin concede alguma depois. E-mail já cadastrado responde `409`, o que revela que a conta existe; escolhemos a clareza da resposta.
- **PUT substitui o conjunto de roles:** atribuir é listar a role e remover é omiti-la. A lista é obrigatória: um corpo sem `roles` é `400` e nunca zera as roles por acidente; lista vazia é válida. Os nomes são comparados sem diferenciar maiúsculas e gravados com o nome canônico. Uma role desconhecida rejeita o pedido inteiro e nenhuma role é criada implicitamente. A troca roda em um único `SaveChanges`, então é atômica.
- **Ordem das respostas em `PUT /api/admin/users/{id}/roles`:** `401` (sem token), `403` (sem a role Admin), `400` (corpo ou role inválidos), `404` (usuário inexistente) e `403` (Admin removendo a própria role Admin). A regra da própria role é de autorização, por isso `403`, como nas regras de dono das despesas.
- **Nunca sem Admin:** como o Admin que faz a chamada não pode tirar a própria role Admin, sempre resta pelo menos um Admin. Ele pode remover a role de outro Admin e pode acumular outras roles.
- **Novo login após alterar roles, de verdade:** o token bearer nativo carrega as roles do momento do login e não consulta o banco por conta própria, então sem tratamento a role removida continuaria valendo até o token expirar (1 hora). Cada troca de roles renova o `SecurityStamp` do usuário, e o `SecurityStampValidationMiddleware` compara o stamp do token com o do banco a cada requisição autenticada. Token antigo vira `401`, e o usuário precisa fazer login de novo. Custo: uma leitura do usuário por requisição autenticada. Trocar para as mesmas roles que o usuário já tem não escreve nada e não invalida o token.
- **Repositório de despesas e filtro na consulta:** `IExpenseRepository.FindOwnedAsync(id, ownerId)` aplica o filtro de dono na própria consulta, antes de materializar. A despesa de outro usuário nunca é carregada e responde `404`, igual à inexistente, como nas decisões do projeto. A criação grava a despesa e a linha de histórico em um único `SaveChanges`, e a edição também (a linha `Edited` entra no histórico da despesa rastreada antes do salvamento).
- **Ordem das respostas em `PUT /api/expenses/{id}`:** `401` (sem token), `403` (sem a role Employee), `400` (dados inválidos), `404` (inexistente ou de outro usuário) e `409` (fora de `Draft`). Por isso a despesa de outro usuário que já foi enviada responde `404`, e não `409`.
- **O `PUT` é uma substituição completa:** `description`, `amount` e `expenseDate` são obrigatórios; a ausência de qualquer um é `400` e nunca "mantém o valor antigo". O servidor define o dono, o estado, o ator e os horários, e os DTOs não têm esses membros, então o que o cliente enviar a mais é ignorado (mass assignment impedido).
- **Valor:** de R$ 0,01 até `Int32.MaxValue`, com no máximo duas casas decimais. O DTO e o serviço **rejeitam** mais de duas casas, e o conversor para centavos nunca arredonda entrada de usuário. O valor é gravado em centavos (`MoneyConversion`).
- **Descrição:** de 10 a 500 caracteres depois de aparar os espaços das pontas, e é gravada já aparada. Uma descrição de 495 caracteres com muitos espaços nas pontas pode passar de 500 no corpo bruto e ser rejeitada pelo DTO; é um limite conservador.
- **Data não futura, em Brasília:** a data da despesa não pode ser posterior à data de hoje no fuso de Brasília (`America/Sao_Paulo`, ou o nome equivalente do Windows). Assim, às 23h30 em Brasília, quando a data em UTC já virou, a data de hoje continua valendo e a de amanhã é rejeitada.
- **Histórico de edição:** toda edição grava uma linha `Edited` (de `Draft` para `Draft`) com o resumo do que mudou, de cada campo com o valor antigo e o novo. Se o `PUT` mandar os mesmos valores já gravados, a linha também é gravada, com `No field changed.`. O resumo é truncado para caber em 2000 caracteres.
- **Resposta da criação:** `POST /api/expenses` responde `201` com o corpo e, desde a I05 (quando o `GET` por id passou a existir), com o cabeçalho `Location` (`/api/expenses/{id}`).
- **Limitação conhecida, para a I06:** um usuário com `Employee` e outra role (por exemplo `Auditor`) que tente editar (`PUT`) a despesa de outra pessoa recebe `404`, porque a edição só enxerga as despesas do dono. O envio já aplica a regra completa (`404`, depois `403`, depois `409`); a I06 deve levar a mesma regra ao `PUT`.
- **Visibilidade por perfil (I05), aplicada na consulta:** `ExpenseVisibility` calcula o escopo do usuário a partir das roles do token (Employee: as próprias; Approver: as `Submitted`; Finance: as `Approved` e `Paid`; Auditor: todas; o Admin sozinho não lê nada), e roles acumuladas **somam** os escopos (união). O escopo é um predicado que o repositório aplica dentro da própria consulta (`WHERE`), antes de materializar, e nunca carrega tudo para filtrar na memória. Os valores do escopo viajam como parâmetros da consulta, então o EF a traduz uma vez para todos os usuários.
- **Detalhe sem vazamento:** o `GET` por id usa o mesmo escopo. Uma despesa inexistente e uma fora do escopo recebem exatamente o mesmo `404` (mesmo corpo), então nada revela que o recurso existe. A resposta traz só `id`, `ownerId`, `description`, `amount`, `expenseDate`, `status` e `createdAtUtc`: sem e-mail do dono, sem histórico e sem pagamento. O `ownerId` é o identificador interno do usuário, mantido porque as próximas issues precisam dele (por exemplo, para o Approver não decidir a própria despesa).
- **Ordem das respostas em `POST /api/expenses/{id}/submit`:** `401` (sem token), `403` (sem a role Employee), `404` (inexistente ou fora do escopo de leitura), `403` (visível por outra role, mas de outro dono) e `409` (fora de `Draft`). Por isso um Employee que também é Auditor recebe `403` ao tentar enviar o rascunho de outra pessoa, e um Employee comum recebe `404`. A regra do dono vem antes da do estado: a despesa `Submitted` de outro usuário dá `403`, e não `409`.
- **Envio simultâneo e `Status` como token de concorrência:** ler o estado e gravar depois deixaria dois envios simultâneos passarem e duplicaria o histórico, o que a especificação proíbe. O `Status` é um token de concorrência do EF: o `UPDATE` leva `WHERE Id = ... AND Status = <valor lido>`, o segundo envio não altera linha nenhuma, o repositório converte a exceção do EF em `ExpenseConflictException` e o serviço responde `409`. A gravação do estado e do histórico é uma única transação, então o envio perdedor não deixa linha de histórico. O mesmo vale para o `PUT` contra um envio simultâneo. A migration `ExpenseStatusConcurrencyToken` é vazia (o esquema do banco não muda; só o snapshot do modelo ganha o token).
- **Listagem:** da mais nova para a mais antiga (`CreatedAtUtc`, depois `Id`), sem paginação (fora de escopo na issue). Com muitos dados a lista cresce; a paginação é uma melhoria futura.
- **Admin e Auditor:** o Admin sozinho recebe `403` nos `GET` e no envio, porque a role Admin não dá acesso funcional. O Auditor lê tudo e nunca escreve: tentar enviar a despesa de outra pessoa dá `403` (e a rota de envio ainda exige a role Employee).

## Solução de problemas

- `dotnet ef` não encontrado: rode `dotnet tool restore` na raiz do repositório.
- `no such table`: o banco ainda não foi criado ou está desatualizado; rode `dotnet ef database update`.
- `Format of the initialization string does not conform to specification`: o valor de `ConnectionStrings__ExpenseHub` está sem o prefixo `Data Source=`.
- O arquivo `.db` não está onde se esperava: confira se a variável `ConnectionStrings__ExpenseHub` está definida no terminal; sem ela o arquivo fica em `sources/ExpenseHub.Api/expensehub.db`.
- `Generating idempotent scripts for migrations is not currently supported for SQLite`: use `dotnet ef migrations script` sem `--idempotent`.
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
- `POST /api/expenses/{id}/submit` devolve `403` com um usuário que tem `Employee` e outra role: a despesa é de outra pessoa e está visível pela outra role, mas só o dono envia.

## Fora de escopo

- deploy;
- Docker;
- anexos;
- leitura automática de comprovantes;
- integração bancária;
- múltiplas moedas;
- aprovação multinível;
- notificações;
- reabertura ou cancelamento;
- frontend obrigatório;
- testes de integração obrigatórios.

## Uso de IA

O projeto usa o Claude Code como assistente de desenvolvimento, conforme [docs/USO-DE-IA.md](docs/USO-DE-IA.md). Todos os diffs são revisados por uma pessoa da equipe, e os commits são feitos pelos autores. Nenhum segredo é enviado em prompts ou versionado.

## Fluxo de trabalho da equipe

Para cada issue:

1. Uma branch por issue, com o identificador no nome, por exemplo `i06-ownership`.
2. Commits coesos, no padrão convencional, por exemplo `feat(expenses): enforce draft ownership on updates`, `test(expenses): cover invalid approval transitions` ou `docs: document SQLite setup`.
3. Pull request no repositório da equipe, com título como `I06: Ownership e matriz de acesso`.
4. A descrição da PR cita a issue como `Racass/checkpoint-csharpracass-expensehub#6`, sem `Closes`, `Fixes` ou `Resolves`, porque a issue do backlog central permanece aberta.
5. O pipeline `code-quality` é analisado antes do merge, e os problemas são corrigidos.
6. Auto-revisão da PR e merge.

## Detalhe da I01: Fundação da solução e Entity Framework Core

Critérios de aceite:

| Critério | Situação | Evidência |
|---|---|---|
| Solução com API e projeto de testes organizados e compiláveis | Atendido | `dotnet build` com 0 avisos e 0 erros; `dotnet test` com 13 testes aprovados |
| Provider relacional compatível com EF Core | Atendido | SQLite, com `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 |
| Contexto e mapeamento das entidades mínimas | Atendido | `ExpenseHubDbContext` com `Expense`, `ExpenseCategory`, `ExpenseHistory` e `PaymentRecord` |
| Mecanismo reproduzível para criar ou atualizar o banco | Atendido | migration `InitialCreate` e `dotnet ef database update` |
| Provider, configuração e comandos documentados, sem segredos | Atendido | seções Como rodar e Banco de dados; a connection string é só um caminho de arquivo |

Casos negativos:

- O build não depende de servidor de banco: SQLite é um arquivo e nada acessa o banco durante o build.
- Os testes unitários não dependem do provider: só chamam funções estáticas puras.
- Diretórios gerados, banco local e credenciais não são versionados: `bin/`, `obj/`, `artifacts/`, `*.db`, `*.db-shm`, `*.db-wal` e `*.sqlite*` estão no `.gitignore`.

Tabelas criadas pela migration:

| Tabela | Colunas principais | Índices e restrições |
|---|---|---|
| `Expenses` | `Id` (Guid), `OwnerId`, `Description` (até 500), `AmountCents`, `ExpenseDate`, `Status` (texto), `CreatedAtUtc` | `CHECK` de faixa em `AmountCents`; índices em `OwnerId` e `Status` |
| `ExpenseCategories` | `Id`, `Name` (até 100) | chave primária |
| `ExpenseHistories` | `Id`, `ExpenseId`, `Action`, `ActorId`, `OccurredAtUtc`, `PreviousStatus`, `NewStatus`, `Reason` (até 500), `Changes` (até 2000) | FK para `Expenses` com `RESTRICT`; índice composto (`ExpenseId`, `OccurredAtUtc`) |
| `PaymentRecords` | `Id`, `ExpenseId`, `ActorId`, `PaidAtUtc` | FK para `Expenses` com `RESTRICT`; índice único em `ExpenseId` |

Os limites de tamanho (`MaxLength`) estão no modelo do EF; o SQLite não os aplica no banco.

Evidências do experimento de consulta (banco real, migration aplicada):

- `OrderBy(Amount)` gerou `ORDER BY "AmountCents"` e retornou `0,01 < 9,99 < 10 < 1234,56 < 2147483647`.
- `Where(Amount > 10.00)` gerou `WHERE "AmountCents" > 1000`.
- `OrderBy` e `Where` em instantes gravados com offsets +0, +3 e -5 respeitaram a ordem cronológica real, e a leitura voltou em UTC.
- Valor 0,00 e valor acima de R$ 2.147.483.647,00 foram rejeitados pelo `CHECK`; o segundo pagamento da mesma despesa foi rejeitado pelo índice único; excluir uma despesa com histórico foi rejeitado pela chave estrangeira.

Decisões:

- Campos de `Expense`, `ExpenseHistory` e `PaymentRecord` conferidos com `docs/REQUISITOS.md`. `CreatedAtUtc` foi mantido (ordenação estável da listagem) e `UpdatedAtUtc` foi removido, porque o histórico já registra cada mudança com horário.
- Os campos de `PaymentRecord` (`ExpenseId`, `ActorId`, `PaidAtUtc`) são inferidos do comportamento de pagamento descrito nos requisitos, que não os enumeram.
- Entidades e `DbContext` são `public` com documentação XML, para manter zero avisos sem supressão.
- A migration não é aplicada na inicialização: o passo de criar o banco é explícito.

## Detalhe da I02: Identity, Admin e autenticação

Critérios de aceite:

| Critério | Situação | Evidência |
|---|---|---|
| Identity utiliza persistência relacional | Atendido | `ExpenseHubDbContext` herda de `IdentityDbContext<IdentityUser>`; migration `AddIdentity` cria as tabelas `AspNet*` no SQLite |
| `POST /login` emite credencial bearer para credenciais válidas | Atendido | login do Admin devolve `tokenType: Bearer` e `accessToken`; o token abre `GET /api/admin/users` com `200` |
| As roles `Admin`, `Employee`, `Approver`, `Finance` e `Auditor` existem | Atendido | o seed cria as cinco; banco com 5 linhas em `AspNetRoles` após a inicialização |
| O seed é idempotente e cria somente uma conta Admin | Atendido | depois de reiniciar duas vezes: 5 roles, 1 usuário, 1 vínculo de role; testes `SeedAsync_RunTwice_*` e `SeedAsync_AdminAlreadyExists_*` |
| A senha inicial vem de configuração segura | Atendido | só user-secrets ou `Seed__Admin__Password`; nenhum arquivo versionado a contém |
| Rotas protegidas diferenciam `401` e `403` | Atendido | sem token: `401`; token de usuário sem a role `Admin`: `403`; com Admin: `200` |

Casos negativos:

- **Credencial inválida não gera token:** e-mail inexistente e senha errada devolvem `401` `ProblemDetails`, sem `accessToken`. Corpo vazio, e-mail malformado, senha vazia ou acima de 128 caracteres devolvem `400`.
- **Reexecutar o seed não duplica roles nem Admin:** conferido no banco depois de reiniciar a aplicação duas vezes.
- **Usuário autenticado sem role não executa operação privilegiada:** o token de um usuário sem role recebe `403` em `GET /api/admin/users`.
- **Segredos fora de commits, logs e arquivos versionados:** a senha não está em nenhum arquivo versionado, e a busca pela senha e pelo token no log da aplicação não encontrou ocorrência. Os erros do seed mostram só códigos de regra.
- **Sem senha configurada:** a aplicação não inicia e nada é gravado; a mensagem não contém nenhum valor secreto.

Tabelas criadas pela migration `AddIdentity`: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins` e `AspNetUserTokens`. A migration não altera as tabelas da I01.

Decisões:

- `IdentityUser` padrão, com o e-mail também como nome de usuário e e-mail único.
- `OwnerId` e `ActorId` continuam sem chave estrangeira; ela será avaliada quando a I04 precisar dela.
- `LockoutEnd` (`DateTimeOffset?`) ficou sem conversor, porque nenhuma consulta ordena ou filtra por ele.
- `GET /api/admin/users` existe só para provar `401` e `403` e devolve apenas `id` e `email`, sem hash de senha. A listagem completa e a alteração de roles são da I03.
- O cadastro (`/register`) e o gerenciamento de roles ficam para a I03.

Como validar (com um banco fora do repositório):

```powershell
$env:ConnectionStrings__ExpenseHub = "Data Source=$env:TEMP\expensehub-teste.db"
$env:Seed__Admin__Password = "<senha do Admin>"
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Depois, chame `POST /login` com o Admin e `GET /api/admin/users` com e sem token. Ao terminar, limpe as variáveis com `Remove-Item Env:ConnectionStrings__ExpenseHub, Env:Seed__Admin__Password`.

## Detalhe da I03: Cadastro HTTP e gerenciamento de roles

Critérios de aceite:

| Critério | Situação | Evidência |
|---|---|---|
| `POST /register` cria usuário sem aceitar role do cliente | Atendido | `201` com `roles` vazia, mesmo com `roles`, `role` e `isAdmin` no corpo; `RegisterRequest` não tem membro de role (teste `RegisterRequest_HasNoRoleMember`) |
| `GET /api/admin/users` é acessível somente por Admin | Atendido | Sem token: `401`; usuário cadastrado sem role: `403`; Admin: `200` com `roles` de cada usuário |
| `PUT /api/admin/users/{id}/roles` altera apenas roles conhecidas | Atendido | `Superuser` e uma role inválida entre válidas: `400`, sem nada aplicado e com 5 roles no banco depois; `employee` e `approver` viram `Employee` e `Approver` |
| O Admin não remove a própria role Admin | Atendido | Remover listando outra role e enviando lista vazia: `403`; o Admin continua `Admin` |
| Usuário inexistente, role inválida e entrada inválida têm respostas coerentes | Atendido | usuário inexistente `404`; role inválida, corpo sem `roles` e `roles: null`: `400`; e-mail inválido, senha fraca e corpo vazio no cadastro: `400`; todos em `ProblemDetails` |
| A documentação informa que um novo login é necessário após alterar roles | Atendido | seção Cadastro e roles, tabela de endpoints e Decisões de projeto; além disso o sistema força: o token antigo devolve `401` |

Casos negativos:

- **Cadastro não promove usuário:** o usuário cadastrado, mesmo enviando roles no corpo, recebe `403` na rota de Admin e `403` ao tentar se promover pelo `PUT`.
- **Employee, Approver, Finance e Auditor não administram roles:** a rota exige a role Admin; sem ela, `403` (e, sem token, `401`).
- **Role arbitrária não é criada implicitamente:** `Superuser` devolve `400` e o banco continua com as 5 roles; o serviço só repassa à store nomes de `AppRoles`.
- **Admin não bloqueia a própria administração:** a remoção da própria role Admin é `403`; um Admin pode tirar a role de outro Admin, e o que faz a chamada continua Admin, então sempre resta um.

Decisões:

- O `PUT` substitui o conjunto de roles e a lista é obrigatória (um corpo sem `roles` é `400`, e nunca "remover tudo").
- A troca de roles é um único `SaveChanges` e renova o `SecurityStamp` do usuário; o middleware `SecurityStampValidationMiddleware` rejeita tokens emitidos antes (`401`). Ver Decisões de projeto.
- `UserSummaryResponse` ganhou `roles` e continua sem hash de senha nem dados de segurança.
- E-mail duplicado responde `409` (a conta existe), e a comparação do e-mail ignora maiúsculas.

Como validar (com um banco fora do repositório):

```powershell
$env:ConnectionStrings__ExpenseHub = "Data Source=$env:TEMP\expensehub-teste.db"
$env:Seed__Admin__Password = "<senha do Admin>"
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Depois: cadastre um usuário, faça login com ele e com o Admin, liste os usuários, atribua e remova roles, tente os casos negativos acima e confira que o token antigo do usuário alterado devolve `401`. Ao terminar, limpe as variáveis com `Remove-Item Env:ConnectionStrings__ExpenseHub, Env:Seed__Admin__Password`.

## Detalhe da I04: Criar e editar rascunho

Critérios de aceite:

| Critério | Situação | Evidência |
|---|---|---|
| `POST /api/expenses` exige Employee e cria estado `Draft` | Atendido | sem token `401`; sem a role (inclusive o Admin) `403`; Employee `201` com `status` `Draft` |
| O proprietário é obtido da identidade autenticada | Atendido | `ownerId` da resposta e da coluna `OwnerId` é o id do usuário do token, mesmo com outro `ownerId` no corpo |
| Descrição, valor e data respeitam o contrato de validação | Atendido | descrição de 10 a 500 caracteres (aparada), valor de 0,01 a 2.147.483.647 com no máximo duas casas e data não posterior a hoje em Brasília; cada violação é `400` e os limites exatos são aceitos |
| `PUT /api/expenses/{id}` edita somente Draft próprio | Atendido | dono em `Draft`: `200`; outro usuário: `404`; `Submitted`, `Approved`, `Rejected` e `Paid`: `409` |
| DTOs impedem mass assignment de proprietário, estado, ator e horários | Atendido | `ExpenseRequest` só tem `description`, `amount` e `expenseDate` (teste `ExpenseRequest_HasOnlyDescriptionAmountAndDate`); `ownerId`, `status`, `createdAtUtc`, `actorId`, `id` e `payment` no corpo são ignorados |
| A persistência e as respostas são consistentes | Atendido | valor gravado em centavos (87,50 vira 8750); um histórico `Created` por despesa criada e um `Edited` por edição, na mesma operação; `ProblemDetails` em todas as falhas |

Casos negativos:

- **Valor menor que R$ 0,01 ou maior que `Int32.MaxValue`:** `0`, `0.009`, `-1` e `2147483647.01` devolvem `400`; `0.01` e `2147483647` são aceitos. Mais de duas casas decimais (`12.345`) também é `400`.
- **Data futura e descrição fora dos limites:** a data de amanhã em Brasília, 9 caracteres, 501 caracteres, só espaços e data inválida (`2026-02-30`) devolvem `400`.
- **Outro usuário não edita o rascunho:** outro Employee recebe `404` e a despesa continua igual; nenhum histórico é gravado.
- **Expense fora de Draft não é editada:** `409`, sem alterar a despesa e sem gravar histórico.

Respostas por endpoint:

| Situação | `POST /api/expenses` | `PUT /api/expenses/{id}` |
|---|---|---|
| Sem token | `401` | `401` |
| Sem a role Employee | `403` | `403` |
| Dados inválidos ou campo ausente | `400` | `400` |
| Inexistente, `id` que não é Guid ou de outro usuário | - | `404` |
| Fora de Draft | - | `409` |
| Sucesso | `201` | `200` |

Exemplo de corpo (o mesmo nos dois endpoints):

```json
{
  "description": "Almoco com cliente em Campinas",
  "amount": 87.50,
  "expenseDate": "2026-10-01"
}
```

Tabelas e dados: nenhuma migration nova. A I01 já criou `Expenses` (com o `CHECK` de faixa em `AmountCents`) e `ExpenseHistories`. A criação grava uma linha em `Expenses` e uma em `ExpenseHistories` (`Created`, ator igual ao dono, estado anterior nulo e novo `Draft`); cada edição grava uma linha `Edited` com o campo `Changes`.

Decisões (o raciocínio completo está em Decisões de projeto):

- O `PUT` substitui os três campos e todos são obrigatórios; o servidor define dono, estado, ator e horários.
- Edição com os mesmos valores grava histórico com `No field changed.`.
- `201` da criação sem `Location` na I04; desde a I05, que criou o `GET` por id, a criação devolve o cabeçalho `Location`.
- A data não futura usa a data de hoje em Brasília.
- Despesa de outro usuário é `404` (o filtro de dono está na consulta); a visibilidade completa por role fica na I06.

Como validar (com um banco fora do repositório):

```powershell
$env:ConnectionStrings__ExpenseHub = "Data Source=$env:TEMP\expensehub-teste.db"
$env:Seed__Admin__Password = "<senha do Admin>"
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Depois: cadastre um usuário, conceda `Employee` pelo Admin e faça login de novo; crie uma despesa; edite-a; tente os casos negativos acima. Ainda não há `GET` de despesas (I05), então confira a gravação nas tabelas `Expenses` e `ExpenseHistories` no banco. Para ver o `409`, mude o `Status` da despesa direto no banco temporário. Ao terminar, limpe as variáveis com `Remove-Item Env:ConnectionStrings__ExpenseHub, Env:Seed__Admin__Password`.

## Detalhe da I05: Enviar, listar e consultar

Critérios de aceite:

| Critério | Situação | Evidência |
|---|---|---|
| `POST /api/expenses/{id}/submit` executa `Draft` para `Submitted` | Atendido | dono em `Draft`: `200` com `status` `Submitted`; histórico `Created` e `Submitted` (anterior `Draft`, novo `Submitted`, ator = dono) na mesma gravação |
| Somente o proprietário envia o Draft | Atendido | outro Employee `404`; Employee que também é Auditor `403`; Approver, Finance, Auditor e Admin sozinhos `403` (falta a role Employee) |
| `GET /api/expenses` aplica o filtro do perfil antes de materializar dados | Atendido | o escopo é um predicado aplicado dentro da consulta (`WHERE`); Employee, Approver, Finance e Auditor veem exatamente o conjunto do perfil, e roles acumuladas somam |
| `GET /api/expenses/{id}` não vaza recursos fora do escopo | Atendido | fora do escopo: `404` com o mesmo corpo de uma despesa inexistente; a resposta não traz e-mail, histórico nem pagamento |
| Recurso inexistente ou invisível retorna `404` | Atendido | `id` inexistente, `id` que não é Guid e despesa de outro perfil dão `404` |
| Transição incompatível ou repetida retorna `409` | Atendido | reenvio, `Submitted`, `Approved`, `Rejected` e `Paid` dão `409`, sem histórico novo; 12 pares de envios simultâneos deram sempre um `200`, um `409` e um único histórico `Submitted` |

Casos negativos:

- **Outro Employee não envia o Draft:** `404` (a despesa está fora do escopo dele), e a despesa continua `Draft` com um único histórico.
- **Submitted não é enviado novamente:** `409`, sem linha de histórico nova.
- **Auditor não ganha escrita:** o Auditor sozinho recebe `403` na rota de envio, e mesmo um Employee que também é Auditor recebe `403` ao tentar enviar a despesa de outra pessoa; nada é alterado.
- **Admin não ganha acesso funcional por ser Admin:** `403` na listagem, no detalhe e no envio.

Visibilidade por perfil (o que cada role lê):

| Role | `GET /api/expenses` e `GET /api/expenses/{id}` |
|---|---|
| Employee | as próprias, em qualquer estado |
| Approver | as `Submitted` de qualquer dono |
| Finance | as `Approved` e `Paid` de qualquer dono |
| Auditor | todas |
| Admin sozinho, ou sem role | `403` |

Roles acumuladas somam os escopos: um Employee que também é Approver lê as próprias despesas mais as `Submitted` de todos.

Respostas por endpoint:

| Situação | `POST .../submit` | `GET /api/expenses` | `GET /api/expenses/{id}` |
|---|---|---|---|
| Sem token | `401` | `401` | `401` |
| Sem role que sirva (inclusive Admin sozinho) | `403` | `403` | `403` |
| Inexistente ou fora do escopo | `404` | - | `404` |
| Visível por outra role, mas de outro dono | `403` | - | - |
| Fora de `Draft` (inclusive reenvio e envio simultâneo) | `409` | - | - |
| Sucesso | `200` | `200` | `200` |

Tabelas e dados: a migration `ExpenseStatusConcurrencyToken` é vazia, porque o esquema do banco não muda. Ela existe só para o snapshot do modelo registrar que `Expenses.Status` é um token de concorrência (sem ela, o `dotnet ef database update` acusaria mudanças pendentes). Cada envio grava uma linha `Submitted` em `ExpenseHistories`.

Decisões (o raciocínio completo está em Decisões de projeto):

- O `Status` é um token de concorrência, para dois envios simultâneos não passarem nem duplicarem o histórico.
- A regra do envio segue o `CLAUDE.md`: `404` fora do escopo de leitura, `403` quando é visível mas de outro dono, `409` quando o estado não aceita.
- A criação passa a devolver `Location`, agora que o `GET` por id existe.
- O `ownerId` fica na resposta (identificador interno, sem e-mail).
- A listagem não tem paginação (fora de escopo), e fica da mais nova para a mais antiga.

Como validar (com um banco fora do repositório):

```powershell
$env:ConnectionStrings__ExpenseHub = "Data Source=$env:TEMP\expensehub-teste.db"
$env:Seed__Admin__Password = "<senha do Admin>"
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Depois: cadastre usuários, conceda as roles pelo Admin (`Employee`, `Approver`, `Finance`, `Auditor` e combinações) e faça login de novo com cada um; crie despesas, envie algumas, e compare o que cada perfil lista e consulta. Aprovar e pagar ainda não existem (I07 e I08), então, para ver `Approved`, `Paid` e `Rejected`, mude o `Status` direto no banco temporário. Ao terminar, limpe as variáveis com `Remove-Item Env:ConnectionStrings__ExpenseHub, Env:Seed__Admin__Password`.

## Próximas issues

Os critérios de cada issue estão no backlog central, e não são copiados aqui.

- I02: [Racass/checkpoint-csharpracass-expensehub#2](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/2)
- I03: [Racass/checkpoint-csharpracass-expensehub#3](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/3)
- I04: [Racass/checkpoint-csharpracass-expensehub#4](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/4)
- I05: [Racass/checkpoint-csharpracass-expensehub#5](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/5)
- I06: [Racass/checkpoint-csharpracass-expensehub#6](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/6)
- I07: [Racass/checkpoint-csharpracass-expensehub#7](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/7)
- I08: [Racass/checkpoint-csharpracass-expensehub#8](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/8)
- I09: [Racass/checkpoint-csharpracass-expensehub#9](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/9)
- I10: [Racass/checkpoint-csharpracass-expensehub#10](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/10)
