# ExpenseHub

[![code-quality](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml/badge.svg)](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml)

API REST corporativa de reembolso de despesas, desenvolvida para o Checkpoint 2 de C# da FIAP (turma 3ESPW). A aplicação usa ASP.NET Core (.NET 10) e Entity Framework Core em banco relacional. O trabalho é entregue por issue, uma por vez, e este README cresce junto com o código: cada seção descreve apenas o que já existe.

Estado atual: a fundação (I01), a autenticação (I02) e o cadastro com a administração de roles (I03) estão prontos: persistência com SQLite, ASP.NET Core Identity, cadastro público sem roles, login com token bearer, as cinco roles, a conta Admin inicial e a atribuição de roles pelo Admin. Ainda não existem endpoints de despesa; os endpoints atuais são `GET /health`, `POST /register`, `POST /login`, `GET /api/admin/users` e `PUT /api/admin/users/{id}/roles`.

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
| I04 | Criar e editar rascunho | 7% | A implementar | Pedro | - |
| I05 | Enviar, listar e consultar | 7% | A implementar | Pedro | - |
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
    Controllers/                  AuthController (POST /register e /login), AdminUsersController
    Domain/
      Entities/                   Expense, ExpenseCategory, ExpenseHistory, PaymentRecord
      Enums/                      ExpenseStatus, ExpenseHistoryAction
    Identity/                     registro do Identity e do bearer, DTOs, AppRoles, o seed, UserAccountService
                                  (regras de cadastro e de roles) e a validação do SecurityStamp
    Persistence/
      ExpenseHubDbContext.cs
      ExpenseHubDbContextFactory.cs             usada só pelo dotnet ef
      PersistenceServiceCollectionExtensions.cs registro no DI e connection string
      Configurations/             mapeamento de cada entidade
      Converters/                 conversões de valor e de instante
      Migrations/                 InitialCreate, AddIdentity e snapshot (gerados)
  ExpenseHub.UnitTests/
    Identity/                     testes do seed, do cadastro, das roles e das validações, com fakes das stores
    Persistence/                  testes das conversões
```

As entidades são classes simples, sem regras de negócio por enquanto. A regra do seed fica em `IdentitySeeder`, que depende da interface `IIdentitySeedStore`; a implementação com `UserManager` e `RoleManager` é `IdentitySeedStore`. As regras de cadastro e de roles ficam em `UserAccountService`, que depende de `IUserAccountStore`; a implementação com EF Core e Identity é `UserAccountStore`.

## Testes

```shell
dotnet test ./sources/ExpenseHub.slnx
```

- Somente testes unitários (MSTest 4), sem banco, rede ou serviço externo. Hoje são 86 testes: 13 da I01 (9 de `MoneyConversion` e 4 de `UtcTicks`), 25 da I02 (14 de `IdentitySeeder` e 11 de `LoginRequest`) e 48 da I03 (25 de `UserAccountService`, 11 de `RegisterRequest`, 4 de `UpdateUserRolesRequest` e 8 de `SecurityStampCheck`, contando cada caso de `DataRow`).
- Os testes da I01 chamam funções estáticas puras. Os do seed e os de `UserAccountService` usam fakes escritos à mão das stores. Nenhum usa tipos do EF Core.
- O login, o cadastro, o `401`, o `403` e a invalidação do token dependem do host e do banco, então foram validados à mão (seções da I02 e da I03), e não por teste unitário.
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
