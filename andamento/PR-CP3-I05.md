# CP3-I05: Imagens, workflow de publicação e Compose

## Issue implementada

Racass/checkpoint-csharpracass-expensehub#15 (CP3-I05, 15 pontos). A PR entrega as duas imagens (web e worker), o workflow que as publica no GitHub Container Registry só depois dos testes, identificadas pelo SHA completo do commit, e o Docker Compose que as executa com o RabbitMQ, o banco SQLite compartilhado e as chaves de autenticação em volumes nomeados. Nenhum comportamento do CP2 mudou e não há pacote NuGet novo nem migration nova.

## Resumo técnico

- **Dockerfiles** (`sources/ExpenseHub.Api/Dockerfile` e `sources/ExpenseHub.Worker/Dockerfile`, contexto na raiz do repositório): compilam no SDK `10.0.403` e executam em `aspnet:10.0.14`, as mesmas bases nos dois (o worker usa `aspnet` porque herda `Microsoft.AspNetCore.App` da Api). Rodam como o usuário `app` (UID 1654); `/data` (e `/keys` no web) são criados e entregues a esse usuário, para os volumes nascerem com a posse certa. O `.dockerignore` mantém `bin`, `obj`, bancos e `.env` fora do contexto.
- **Evidência da versão em execução**: o commit entra por `GIT_COMMIT` na variável `Build__Commit`, na label `org.opencontainers.image.revision` e numa linha de log na partida dos dois processos (`Image commit: <sha>`). O valor só é aceito se for hexadecimal de 7 a 64 caracteres, senão aparece `unknown`. O `/health` não mudou.
- **Workflow novo** `.github/workflows/publish-images.yml` (o `build.yml` do professor não foi tocado): job `test` (restore, build, test) e job `images` com `needs: test`. Em push na `main`, sem filtro de caminho, constrói as duas imagens, confere a label do commit, entra no GHCR com o `GITHUB_TOKEN` e envia `ghcr.io/renan-utida/expensehub-web:<SHA completo>` e `ghcr.io/renan-utida/expensehub-worker:<SHA completo>`. Nunca `latest`. Nas PRs para a `main` os mesmos builds rodam sem login e sem push. Só a CLI do Docker e as ações de checkout e do .NET nas mesmas versões do `build.yml`.
- **`compose.yaml`**: `rabbitmq`, `web` e `worker`. As imagens vêm de `${IMAGE_TAG:?...}`, sem o SHA dentro do arquivo. O RabbitMQ está fixado em `4.4.0-management` por tag e digest (a imagem que a CP3-I03 validou), com hostname fixo (o nome do nó nomeia a pasta de dados), volume nomeado e healthcheck. Web e worker montam o mesmo volume `expensehub-data` em `/data` e recebem a mesma connection string absoluta de um único bloco com âncora YAML. O web também recebe `Database__MigrateOnStartup` e o volume `expensehub-keys`. O worker espera o broker e o web saudáveis. Portas só em localhost e o AMQP não é publicado. Todas as variáveis obrigatórias usam `${VAR:?mensagem}`.
- **`.env.example`**: lista as variáveis com os segredos em branco; o `.env` local é ignorado pelo Git.
- **Inicialização do banco**: a chave `Database:MigrateOnStartup`, desligada por padrão e ligada só no Compose, faz o web aplicar as migrations pendentes antes do seed do Admin (`DatabaseMigrationHostedService`). Escolhida em vez de um bundle do EF por não criar serviço novo. O worker nunca migra. O comportamento do CP2 (`dotnet ef database update`) não muda.
- **Chaves de autenticação**: `DataProtection:KeysPath` (vazia por padrão, sem efeito) leva as chaves do Data Protection para o volume, e o token emitido antes de recriar o contêiner continua válido.
- **Correção no worker**: `dotnet publish` do worker falhava com NETSDK1152 porque o `appsettings.json` da Api chegava à saída pela referência de projeto e colidia com o do worker. Um alvo no `.csproj` do worker remove só esse item; a checagem de duplicatas do SDK continua ligada.
- **Documentação**: README ("Rodar com Docker Compose", variáveis, volumes, WAL, como conferir a versão, como construir à mão, o que não fazer, solução de problemas e a versão do RabbitMQ), seção da CP3-I05 e decisões no `CHECKPOINT-3.md`, ambiente `compose` no `.http`, e a CP3-I03 marcada como concluída (PR #14, merge `78bca7d`, workflow `code-quality` 100/100 na execução #75).

## Decisões e concessões

- **Flag em vez de bundle do EF** para a inicialização do banco: sem serviço extra, sem `dotnet-ef` no build e sem executável autocontido por arquitetura dentro da imagem. Custo: com a flag ligada o processo web tem o poder de alterar o esquema.
- **Gatilho de PR no workflow** é um acréscimo: a publicação só roda na `main`, e sem ele os Dockerfiles só seriam exercitados no GitHub depois do merge. Pode ser removido apagando o bloco `pull_request`.
- **`RABBITMQ_PASSWORD` no Compose**: a verificação `:?` fica na linha `RabbitMq__Password`; a do broker usa `${RABBITMQ_PASSWORD}` simples, porque o script de qualidade leria `PASSWORD:?` como valor literal em uma linha cuja chave não tem a palavra senha. A variável é obrigatória do mesmo jeito (o Compose interpola o arquivo inteiro).
- As imagens são `linux/amd64`. O WAL exige web e worker no mesmo host e disco local (por isso volume nomeado). As chaves do Data Protection ficam sem criptografia em repouso no volume.
- O `RABBITMQ_DEFAULT_*` só vale quando o volume do broker nasce.

## Como validar

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

Depois do merge na `main` e do workflow `publish-images` terminado, com os dois pacotes públicos:

```shell
cp .env.example .env     # preencher IMAGE_TAG (SHA completo), RABBITMQ_USER, RABBITMQ_PASSWORD, ADMIN_EMAIL e ADMIN_PASSWORD
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail 80 worker
docker compose stop worker
docker compose up -d worker
docker compose up -d --force-recreate web worker
```

Antes do merge, as imagens podem ser construídas à mão com os comandos do README e usadas com `IMAGE_TAG=local`.

## Evidências relevantes

- Build com 0 avisos e 0 erros nos três projetos. `dotnet test`: **1149 aprovados** (1086 da base e 63 novos), 0 falhas.
- Testes novos, sem banco, rede, RabbitMQ nem Docker: linha de versão 8, normalização do commit 16, chave de migração 10, local das chaves 7 e regras dos arquivos de entrega 22 (lidos como texto).
- Mutações feitas e desfeitas: ignorar a chave de migração, trocar a tag do Compose por `latest`, tirar `needs: test` do workflow e tirar a validação do commit quebraram 11 testes.
- Validação real no Docker (projeto Compose `expensehub-i05`, volumes novos, credenciais geradas na hora, imagens construídas com os mesmos nomes): **71 conferências aprovadas, nenhuma falha**.
  - Primeira subida: 4 migrations aplicadas, seed do Admin, web saudável antes do worker, linha de versão nos dois logs, fila durável, RabbitMQ 4.4.0.
  - Fluxo: `202` e `Completed` com o worker em outro contêiner (`Mine` 3 e 175,00; `Global` 4 e 375,00), Excel válido, `-wal` e `-shm` vistos pelo worker.
  - Worker parado: `202`, `Pending`, mensagem pronta na fila; ao voltar, o mesmo id conclui.
  - `up -d --force-recreate web worker`: o token emitido antes continuou válido, o resultado anterior ficou idêntico, o commit da label, da variável e do log é o mesmo, e as migrations não foram reaplicadas.
  - Broker recriado com uma mensagem na fila: fila durável e mensagem sobreviveram; o mesmo id concluiu.
  - Logs do web, do worker e do RabbitMQ sem a senha do broker, a do Admin, o token e a connection string.
- Script de qualidade (`-SkipGitleaks`): **100/100**, 20 em cada categoria, sem bloqueantes. Achado informativo `FIAP0002` (Gitleaks ignorado; roda no CI).

## Pendências depois do merge

- Conferir que o workflow `publish-images` terminou na `main` e que as duas tags (SHA completo) existem no GHCR. Isso só pode ser provado depois do merge.
- Tornar os dois pacotes públicos na página deles no GitHub (passo manual), para a correção puxar sem credenciais, e conferir `docker compose pull` com o SHA completo.
- Preencher o número da PR e o score oficial desta issue no README, no CHECKPOINT-3 e no PROGRESSO.

## Impactos em segurança e autorização

- Nenhum arquivo versionado tem usuário, senha ou token: o `.env.example` deixa os segredos em branco, o Compose só os referencia por variável e as imagens não trazem nenhum. O `docker inspect` dos contêineres só foi usado com campos específicos, porque o `Env` completo traz as credenciais.
- Os logs dos três serviços foram conferidos e não trazem as credenciais geradas, o token nem a connection string. A linha de versão só aceita hexadecimal.
- As imagens rodam como usuário não root, e as portas são publicadas só em localhost.
- Nenhum comportamento do CP2 (autenticação, autorização, pagamento) nem os contratos HTTP foram alterados; as chaves de configuração novas vêm desligadas ou vazias por padrão.

## Checklist

- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
