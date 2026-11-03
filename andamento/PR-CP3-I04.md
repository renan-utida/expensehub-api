# CP3-I04: Auditoria da suíte e verificação de mutação

## Issue implementada

Racass/checkpoint-csharpracass-expensehub#14 (CP3-I04, 15 pontos). A PR audita a suíte unitária contra a tabela de 13 cenários mínimos da apostila, cria só o que faltava, prova por mutação que os testes pegam defeitos nas regras novas do CP3 e documenta o comando `dotnet test`. **Nenhum código de produção mudou**: só testes e documentação. Não há pacote novo nem migration.

## Resumo técnico

- **Auditoria.** Li os testes (não só os nomes) contra cada cenário, olhando se a asserção é de efeito (o que o cliente recebe e o que ficou gravado) e não só de chamada. Nenhum cenário estava sem teste. Faltava profundidade em três pontos: o percurso encadeado (controller, serviço e processador reais juntos), o corpo das respostas de erro (os testes do controller só conferiam o código) e a medição de que os testes pegam defeito nas regras do CP3. A tabela cenário por teste (13 linhas) está no `CHECKPOINT-3.md`, com o que a PR acrescentou em cada uma.
- **`ReportScenarioTests` (novo, 15 casos).** Usam o `ReportsController`, o `ReportService` e o `ReportMessageProcessor` reais sobre os fakes escritos à mão que já existiam. Cobrem: 400 sem gravar nem publicar (período invertido, datas ausentes, escopo inexistente), 403 do Employee em `Global` sem nada criado, 404 de relatório alheio (detalhe e Excel) com o corpo de um inexistente e sem dado do relatório, 409 sem arquivo para o próprio não concluído (`Pending`, `Processing`, `Failed`), planilha entregue com o tipo e o nome do contrato e células numéricas iguais ao resultado salvo, resultado que o worker gravou e que sobrevive a uma alteração de reembolso e a uma mensagem repetida depois de reiniciar o worker, 503 com o `id` e relatório `Pending` (broker sem confirmar e broker fora do ar) e corpo com `ownerId` e `requesterId` ignorado.
- **6 testes novos que o primeiro passe de mutação mostrou faltar:** `GetExcelAsync_OwnReportNotCompleted_GivesNoFileEvenWithAResultSaved` e `From_ReportNotCompletedWithAResultSaved_HasNoResult`, três estados cada. O `CHECK` da tabela só exige resultado quando o estado é `Completed`; ele não proíbe resultado em outro estado, e nenhum teste dizia que o estado decide o Excel e o envelope.
- **7 testes antigos reforçados:** três de `ReportServiceExcelTests` (sem nome de arquivo nem conteúdo no 404; o arquivo concluído passa a ser aberto e conferido) e quatro de `ReportResultSerializerTests` (a exceção passa a ter a causa certa e não só o tipo).
- **Verificação de mutação**, no método da I09 do CP2, mas numa cópia do repositório no scratchpad (nunca na árvore real) e sem `git restore`. 45 defeitos simples nas regras novas, em três grupos: cálculo, vazio e período (14); negação, escopo e isolamento (15); duplicata, resultado salvo e Excel (16). Detalhes, tabela por grupo e a lista das 45 no `CHECKPOINT-3.md`.
- **Documentação.** README (seção "Testes" com o comando, as variações com `--filter`, o resultado esperado e o bullet da CP3-I04) e `CHECKPOINT-3.md` (seção da CP3-I04, duas decisões novas). A CP3-I05 foi marcada como concluída (PR #15, merge `fb89a4f`, workflow `code-quality` 100/100 na execução #78, imagens publicadas com o SHA completo do merge, pacotes públicos e pull anônimo por SHA conferido pelo Renan). O `docker compose pull` com o `IMAGE_TAG` final continua pendente até a conferência final (CP3-I06 e CP3-I07).

## Decisões e concessões

- **Mutação fora da árvore real.** As mutações só existem em cópias no scratchpad, acessadas por uma letra de unidade (`subst`), porque o caminho do scratchpad faz alguns arquivos passarem dos 260 caracteres do Windows. A árvore real nunca contém um mutante.
- **Sobreviventes relatados como estão.** A rodada 1 deixou dois: A10 (o estado decide o Excel) e D13 (a causa da exceção do serializador). Eram lacunas reais dos testes e foram corrigidas. A mutação D16 (o mesmo padrão no envelope) foi acrescentada depois da rodada 1, por isso são 45 mutações e não 44.
- **Os testes de cenário não foram escritos para matar mutante.** Os 15 sozinhos matam 29 das 45 mutações; o que é regra de um componente só (por exemplo, período de um dia válido, `mine` em minúsculas, valores sem centavos) continua nos testes por componente. O valor deles é mostrar que as peças, juntas, dão as respostas do contrato, e dar um segundo detector a seis mutações que na rodada 1 dependiam de um teste só.
- **Limite.** O filtro por solicitante do repositório em EF, as transições do `ReportProcessingStore`, os adaptadores do RabbitMQ e o host do worker não têm teste unitário (os testes não podem tocar banco nem broker). Uma mutação ali sobreviveria por desenho; a prova foi manual, nas CP3-I01 a CP3-I03. A verificação de mutação é manual e não faz parte do CI.

## Como validar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx --no-incremental
dotnet test ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx --filter "FullyQualifiedName~ExpenseHub.UnitTests.Reports.ReportScenarioTests"
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

O segundo comando roda a suíte unitária inteira (1170 aprovados, 0 falhas), sem banco, rede, RabbitMQ nem Docker; o terceiro, só os 15 cenários.

## Evidências relevantes

- Build com 0 avisos e 0 erros nos três projetos. `dotnet test`: **1170 aprovados** (1149 da base e 21 novos), 0 falhas.
- Mutação, rodada 1 (suíte antes da PR, 1149 testes): **42 de 44 mortas**; sobreviventes A10 e D13. Rodada 2 (suíte final, 1170 testes): **45 de 45 mortas**. Em cada cópia a suíte passou inteira antes e depois das mutações, e todo arquivo mutado voltou idêntico. As mutações que dependiam de um único teste passaram de 9 para 3 (C13, D2 e D3).
- Só os 15 testes de cenário: 29 das 45 mortas.
- Script de qualidade (`-SkipGitleaks`): **100/100**, 20 em cada categoria, sem bloqueantes. Achado informativo `FIAP0002` (Gitleaks ignorado; roda no CI).

## Pendências

- O `docker compose pull` com o `IMAGE_TAG` final só será conferido na CP3-I06/I07, com o SHA do commit de entrega.
- Completar o `.http` e o dossiê dos experimentos (E01 a E05) na CP3-I06.
- Preencher o número da PR e o score oficial desta issue no README, no CHECKPOINT-3 e no PROGRESSO depois do merge.

## Impactos em segurança e autorização

- Nenhum código de produção mudou, então nenhum comportamento de autenticação, autorização, pagamento ou de relatório foi alterado.
- Os testes novos montam os valores em tempo de execução e não atribuem literal a nomes com password, senha, secret ou token. O nome "intruder" do teste do corpo com `ownerId` é só um identificador de teste.
- A verificação de mutação não deixou nada no repositório: as cópias, os resultados e o script ficam no scratchpad, fora do Git.

## Checklist

- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
