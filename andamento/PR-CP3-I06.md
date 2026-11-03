# CP3-I06: Experimentos e diagnóstico

## Issue implementada

Racass/checkpoint-csharpracass-expensehub#16 (CP3-I06, 10 pontos). A PR registra os cinco experimentos obrigatórios (E01 a E05) executados no Docker do Renan, com as imagens publicadas do SHA completo `f18cadbc69fdd38a3cc55b817e4fcc91ee139e1f` (merge da CP3-I04), completa o arquivo `.http` e escreve o diagnóstico da aceitação, da publicação e da janela sem Outbox. **Nenhum código de produção mudou**: só o `.http` e a documentação. Não há pacote novo, migration nem imagem nova. O commit final de entrega gera imagens próprias (outra tag); os experimentos não foram repetidos com elas, e a CP3-I07 provará com `git diff --stat` que a diferença do SHA dos experimentos até o final é só documentação.

## Resumo técnico

- **Como foi feito.** A issue é guiada: o assistente preparou o roteiro (blocos numerados para o Git Bash) e o ensaiou antes, de ponta a ponta, num projeto Compose separado (`expensehub-i06dry`, credenciais descartáveis, portas diferentes, as mesmas imagens publicadas); o Renan executou o roteiro no projeto real `expensehub` e colou as saídas; o assistente registrou no dossiê só o que foi colado. O ensaio serviu para corrigir o texto dos blocos e não é evidência dos experimentos.
- **E01 (fluxo normal).** `POST` `Mine` de setembro de 2026 da ana: `202` com `Location`, relatório `2e1a02c1-aac4-40ca-b2c9-2e4043209fa3` acompanhado até `Completed` com 3 itens e 175 (Approved 2 e 150, Paid 1 e 25, os outros três estados em zero), logs do web e do worker pelo id e Excel real (`200`, `Content-Type` de planilha, `Content-Disposition: attachment` com `report-{id}.xlsx`, `Microsoft Excel 2007+`) com as mesmas células do JSON, em células numéricas.
- **E02 (worker parado).** Só o worker foi parado; o `POST` deu `202` e o relatório `56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f` ficou `Pending` com a fila `expensehub.reports` em `1 0` (1 Ready, 0 Unacked); ao religar o worker, o **mesmo id** chegou a `Completed` e a fila voltou a `0 0`. O E01-b (Excel) foi feito antes do E02.
- **E03 (duplicata).** Com o relatório do E01 concluído, o worker foi reiniciado e o mesmo `reportId` foi republicado pelo painel do RabbitMQ (Delivery mode `2 - Persistent`, `content_type` `application/json`, payload `{"reportId":"..."}`): o log do worker traz `is already Completed: duplicate message ignored, nothing was changed.`; o hash do JSON do detalhe, a quantidade de relatórios da listagem e o hash das células do Excel ficaram idênticos; fila `0 0`; uma só linha de conclusão para o id.
- **E04 (isolamento).** Oito casos, todos `OK`: `403` do Employee pedindo `Global`; `404` do Employee consultando e baixando o Excel do relatório do bruno; `202` do Auditor em `Global` (4 itens e 375); `404` do Auditor consultando e baixando o relatório da ana; `404` do Employee para o relatório do Auditor; listagem do Auditor só com o relatório dele.
- **E05 (persistência e versão).** `docker compose up -d --force-recreate web worker` sem remover volume: web e worker com contêineres novos, RabbitMQ intacto; a versão é a mesma (rótulo `org.opencontainers.image.revision` e linha de log `Image commit` com o SHA `f18cadbc...`); o relatório do E01, consultado com o **mesmo token** de antes da recriação, respondeu `200` e os hashes do JSON e do Excel ficaram iguais.
- **Diagnóstico (quatro partes).** (a) aceitação e conclusão, com a linha do tempo do E01 (o `202` é "aceito e publicado", não "concluído"; a confirmação vem depois da gravação); (b) worker parado, broker indisponível e janela sem Outbox como falhas diferentes, com o que cada uma deixa no banco e na fila e a fonte de cada linha (o único caso reproduzido aqui é o do worker parado; os outros são do código, dos testes e da CP3-I03, ditos como tal); (c) duplicata e novo `POST` (um novo `POST` cria outro relatório com outro id e não prova idempotência); (d) `Ready` e `Unacked` do RabbitMQ não são `Pending` e `Processing` da aplicação.
- **Arquivo `.http`.** Reescrito com 50 requisições nomeadas (login do Admin, cadastro de quatro contas e papéis, despesas de teste, `Mine` e `Global`, listar, consultar, baixar o Excel e os casos `400`, `401`, `403` e `404`), sem e-mail, senha nem token: as credenciais vêm de `http-client.env.json.user` (ignorado pelo git) e os tokens vêm das respostas dos logins. Foi executado de verdade, requisição por requisição, por um executor em Python (fora do repositório) contra uma pilha de ensaio: 50 de 50 com o código HTTP esperado. Não foi aberto no Visual Studio.
- **Documentação.** `CHECKPOINT-3.md` (seção da CP3-I06 e o dossiê com ambiente, sanitização, dados de teste, comandos exatos, saídas reais, tentativas que falharam, diagnóstico e observações), README (estado, tabela e `.http`) e `andamento/PROGRESSO.md`.

## Decisões e concessões

- **Versão dos experimentos.** As imagens publicadas do SHA `f18cadbc...` (merge da CP3-I04), não as do commit final. O dossiê diz isso e diz que o commit final gera imagens próprias.
- **Segredos.** A senha do Admin é gerada com `openssl` no bloco 1 e gravada no `.env` sem imprimir (só letras, dígitos e `!`, para a interpolação do Compose não a alterar); a das contas de teste é digitada com `read -rs` e validada; tokens e senhas só em variáveis do shell, nunca em arquivo. O arquivo de saídas do Renan foi varrido antes de gravar (nada encontrado), não foi copiado para o repositório, e os trechos entre colchetes que ele escreveu não foram citados como saída.
- **Tentativas que falharam, registradas como aconteceram.** Os blocos 1 e 5 quebraram na primeira vez porque a colagem trouxe linhas em branco depois de uma barra de continuação (`sed -i \` e `curl ... \`); o bloco 2 falhou em consequência do 1 (`required variable ... is missing a value`), sem criar contêiner, rede nem volume; a primeira leitura da senha no bloco 3a recebeu uma linha vazia e foi recusada. O roteiro passou a ter a regra "nenhum comando com barra de continuação", o bloco 1 ganhou uma trava (`cmp -s`, que só apaga a cópia em branco do exemplo) e o `docker compose config --quiet`. O texto de uma linha do bloco 1 no dossiê é uma reconstrução das partes que o arquivo de saídas abreviou com `...`, testada em arquivo descartável.
- **Saída do `docker compose pull`.** Registrada como veio (`Pulling` e `Pulled`); ela não diz se as camadas foram baixadas naquele momento, e o dossiê não afirma isso. O pull sem login no GHCR feito no ensaio é observação do assistente.
- **Duas observações conferidas no código, sem defeito.** (1) O intervalo de 1 a 2 s entre `message received` e `processing started` ocorreu na primeira mensagem de cada processo do worker (1,724 s, 1,314 s e 0,892 s na duplicata; 28 ms no E04 com o worker aquecido). Não há `Task.Delay` nem polling nesse caminho; a explicação provável é o custo de primeiro uso do acesso a dados, que **não foi medida**. (2) O token continuou válido depois de recriar o web porque as chaves do Data Protection ficam no volume `expensehub-keys` (`DataProtection__KeysPath: /keys`); o experimento mostra a validade e a configuração aponta a causa, mas o contrafactual (sem o volume) não foi testado, de propósito.
- **Limites.** O painel do RabbitMQ não tem captura de tela no dossiê (só a descrição do passo e o log do worker que confirma a hora da publicação); o `409` do Excel de um relatório próprio não concluído não faz parte dos cinco experimentos (está nos testes unitários); o `Processing` não foi capturado ao vivo (a janela dura décimos de segundo), e a passagem por ele aparece pelo `startedAt` e pelo log; broker indisponível e janela sem Outbox não foram reproduzidos nesta issue (a apostila não exige um sexto experimento).

## Como validar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx --no-incremental
dotnet test ./sources/ExpenseHub.slnx
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

Para repetir os experimentos, siga os blocos do dossiê (`CHECKPOINT-3.md`, seção "Dossiê dos experimentos (E01 a E05)") no Git Bash, a partir da raiz do repositório e no mesmo terminal. O arquivo `.http` é usado depois dos experimentos, porque o `Global` soma as despesas de todos os usuários.

## Evidências relevantes

- Build com 0 avisos e 0 erros nos três projetos. `dotnet test`: **1170 aprovados**, 0 falhas (a issue não mudou código nem testes).
- Script de qualidade (`-SkipGitleaks`): **100/100**, 20 em cada categoria, sem bloqueantes. Achado informativo `FIAP0002` (Gitleaks ignorado; roda no CI).
- Experimentos: ids de relatório `2e1a02c1-aac4-40ca-b2c9-2e4043209fa3` (E01, E03 e E05), `56e9f508-b9eb-4e9f-b61d-6b15fc5d2f9f` (E02), `8061bff1-4218-4bad-82ba-e8a00991a926` (bruno, E04) e `3d7f3b45-d770-4a64-a0bc-3a56a1de61ad` (caio, E04); hash das células do Excel `ae3d22e2d17ba4e1` (E01-b, E03 e E05) e hash do JSON do detalhe `225393dcc6004d6b` (E03 e E05); fila `0 0` no fim de cada experimento; horários em UTC nos logs.
- Nenhum token, senha, cabeçalho de autorização nem connection string aparece no dossiê.

## Pendências

- Preencher o número da PR e o score oficial desta issue no README, no CHECKPOINT-3 e no PROGRESSO depois do merge.
- CP3-I07: `git diff --stat` do SHA dos experimentos até o SHA final mostrando só documentação, e o `docker compose pull` com o `IMAGE_TAG` final.
- Limpeza dos contêineres parados e dos volumes dos projetos `expensehub-i05` e `expensehub-i06dry` que ficaram no Docker do Renan (a decisão é dele); o projeto real `expensehub` continua no ar.

## Impactos em segurança e autorização

- Nenhum código de produção mudou, então nenhum comportamento de autenticação, autorização, pagamento ou de relatório foi alterado. O E04 confirma na prática o isolamento já coberto pelos testes (`403` e `404`, inclusive para o Auditor).
- O `.http` não tem e-mail, senha nem token; o `.env` real do Renan nunca foi lido nem impresso pelo assistente e continua ignorado pelo git. As senhas geradas e digitadas ficaram só em variáveis do shell, que o bloco final apaga.

## Checklist

- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
