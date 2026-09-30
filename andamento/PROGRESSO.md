# PROGRESSO

Registro temporário do andamento por issue. Esta pasta será removida antes do SHA final. Leia este arquivo antes de começar uma issue e atualize-o ao terminar (junto com a linha "Estado atual", a tabela de status, a tabela de endpoints e a seção da issue no README).

## Prazo e decisões do professor

- **Prazo:** quarta-feira, 14/10/2026, às 23:59, no Teams (confirmado pelo professor; o ENUNCIADO e o README dele ainda dizem 13/10). Vale planejar para fechar antes e deixar folga para o score, o README e o SHA final.
- **ExpenseCategory:** nenhuma issue cobra categoria. Fica só a entidade mínima (`Id` e `Name`), sem endpoint, seed nem vínculo com `Expense`.
- **Testes unitários:** sem EF Core InMemory e sem SQLite em memória. O acesso a dados é simulado por interface de repositório injetada, com fakes ou mocks. O banco em si seria coberto por testes funcionais, que são opcionais e não pontuam.
- **Códigos de status:** o professor pediu que a equipe decida pelo significado de cada código (401 é autenticação, 403 é autorização, 404 é recurso inexistente ou fora do escopo, 409 é conflito de estado). As escolhas atuais estão na seção Decisions do `CLAUDE.md`. Documentar o raciocínio no README quando a I06 for concluída.

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

**Status:** concluída; PR #1 (pipeline oficial 100/100, com Gitleaks).

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
- 10 commits com `(I01)` no fim da primeira linha, reunidos na PR #1 do repositório da equipe.

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

- **Merge da PR #1:** depois de marcar "Pipeline analisado" na descrição e conferir o check verde, fazer o merge com "Create a merge commit". Depois, `git switch main` e `git pull`, e conferir que o workflow também passou na `main`.
- **Remover `andamento/`** antes do SHA final. O README não cita nem linka esta pasta; o `CLAUDE.md` cita, e a regra correspondente deve ser removida junto.
- **Feitos:** número da PR preenchido no README; Gitleaks conferido no CI.

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

### Modelo de PR (usado na PR #1)

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
- [ ] Pipeline analisado
- [x] Documentação atualizada
```

## Próximo passo: I02 (Pedro)

Depois do merge da PR #1:

```shell
git switch main
git pull
dotnet tool restore
dotnet ef database update --project ./sources/ExpenseHub.Api --startup-project ./sources/ExpenseHub.Api
git switch -c i02-identity-auth
```

Antes de codar: ler os avisos (a) a (h) acima, a seção Decisions do `CLAUDE.md` e os critérios da I02 no backlog central (Racass/checkpoint-csharpracass-expensehub#2). A senha inicial do Admin vem de configuração segura (user-secrets ou variável de ambiente) e nunca de arquivo versionado.