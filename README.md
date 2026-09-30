# ExpenseHub

[![code-quality](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml/badge.svg)](https://github.com/renan-utida/expensehub-api/actions/workflows/build.yml)

API REST corporativa de reembolso de despesas, desenvolvida para o Checkpoint 2 de C# da FIAP (turma 3ESPW). A aplicação usa ASP.NET Core (.NET 10) e Entity Framework Core em banco relacional. O trabalho é entregue por issue, uma por vez, e este README cresce junto com o código: cada seção descreve apenas o que já existe.

Estado atual: a fundação está pronta (solução, persistência com SQLite, entidades mínimas e migration inicial). Ainda não existem Identity, autenticação nem endpoints de negócio; o único endpoint é `GET /health`.

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
- [Próximas issues](#próximas-issues)

## Equipe

| Nome | RM | GitHub |
|---|---|---|
| Renan Dias Utida | 558540 | [renan-utida](https://github.com/renan-utida) |
| Pedro Almeida e Camacho | 556831 | [Pedro-Camacho](https://github.com/Pedro-Camacho) |

## Issues

| Issue | Título | Peso | Status | Responsável | PR |
|---|---|---:|---|---|---|
| I01 | Fundação da solução e Entity Framework Core | 4% | Concluída | Renan | a preencher |
| I02 | Identity, Admin e autenticação | 9% | A implementar | Pedro | - |
| I03 | Cadastro HTTP e gerenciamento de roles | 8% | A implementar | Pedro | - |
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

Esta tabela ganha uma linha a cada endpoint implementado.

## Como rodar

Pré-requisito: SDK do .NET 10. Não é preciso instalar servidor de banco.

Rode os comandos a partir da raiz do repositório:

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet tool restore
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

- `dotnet tool restore` instala o `dotnet-ef` na versão fixada em `dotnet-tools.json`.
- `dotnet ef database update` cria o arquivo do banco e aplica as migrations. Rode a partir da raiz do repositório, porque o caminho de `--project` é relativo à pasta atual. O arquivo do banco cai sempre em `sources/ExpenseHub.Api/expensehub.db`.
- A aplicação escuta em `http://localhost:5245`. Para conferir que ela iniciou:

```shell
curl http://localhost:5245/health
```

Resposta esperada: `{"status":"ok"}`.

## Banco de dados

**Provider:** SQLite, escolhido por ser um arquivo local que dispensa servidor.

| Pacote | Versão | Onde |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.12 | `ExpenseHub.Api` |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.12 | `ExpenseHub.Api` (somente tempo de design, `PrivateAssets=all`) |
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
    Program.cs                    inicialização e GET /health
    appsettings.json
    Domain/
      Entities/                   Expense, ExpenseCategory, ExpenseHistory, PaymentRecord
      Enums/                      ExpenseStatus, ExpenseHistoryAction
    Persistence/
      ExpenseHubDbContext.cs
      ExpenseHubDbContextFactory.cs             usada só pelo dotnet ef
      PersistenceServiceCollectionExtensions.cs registro no DI e connection string
      Configurations/             mapeamento de cada entidade
      Converters/                 conversões de valor e de instante
      Migrations/                 InitialCreate e snapshot (gerados)
  ExpenseHub.UnitTests/
    Persistence/                  testes das conversões
```

As entidades são classes simples, sem regras de negócio por enquanto.

## Testes

```shell
dotnet test ./sources/ExpenseHub.slnx
```

- Somente testes unitários (MSTest 4), sem banco, rede ou serviço externo. Na I01, 13 testes: 9 de `MoneyConversion` e 4 de `UtcTicks`.
- Os testes chamam funções estáticas puras e não usam nenhum tipo do EF Core.
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

- Nesta rodada: `dotnet build` com 0 erros e 0 avisos, `dotnet test` com 13 testes aprovados e score local **100/100** (20 em cada categoria, sem bloqueantes). Essa execução usou `-SkipGitleaks`, então a varredura de segredos do Gitleaks só roda no CI.
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

## Solução de problemas

- `dotnet ef` não encontrado: rode `dotnet tool restore` na raiz do repositório.
- `no such table`: o banco ainda não foi criado ou está desatualizado; rode `dotnet ef database update`.
- `Format of the initialization string does not conform to specification`: o valor de `ConnectionStrings__ExpenseHub` está sem o prefixo `Data Source=`.
- O arquivo `.db` não está onde se esperava: confira se a variável `ConnectionStrings__ExpenseHub` está definida no terminal; sem ela o arquivo fica em `sources/ExpenseHub.Api/expensehub.db`.
- `Generating idempotent scripts for migrations is not currently supported for SQLite`: use `dotnet ef migrations script` sem `--idempotent`.

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
