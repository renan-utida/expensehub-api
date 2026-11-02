# Checkpoint 3: ExpenseHub em operação

Registro do Checkpoint 3 (CP3) do ExpenseHub, feito por Renan Dias Utida (RM 558540). Prazo: 04/11/2026, às 23:59, no Teams. A nota-base é coletiva. O estado vivo do sistema (como rodar, endpoints, banco, testes e qualidade) fica no [README](README.md); este arquivo guarda o detalhe de cada issue, as decisões e as evidências, do mesmo modo que o [CHECKPOINT-2.md](CHECKPOINT-2.md) faz para o Checkpoint 2. Cada seção é preenchida quando a issue termina.

## Sumário

- [Ponto de partida](#ponto-de-partida)
- [Decisões do CP3](#decisões-do-cp3)
- [Fora de escopo do CP3](#fora-de-escopo-do-cp3)
- [Fluxo de trabalho do CP3](#fluxo-de-trabalho-do-cp3)
- [Detalhe da CP3-I01: Contrato e resultado do relatório](#detalhe-da-cp3-i01-contrato-e-resultado-do-relatório)
- [Detalhe da CP3-I02: Autorização e isolamento](#detalhe-da-cp3-i02-autorização-e-isolamento)
- [Detalhe da CP3-I03: RabbitMQ, worker e idempotência](#detalhe-da-cp3-i03-rabbitmq-worker-e-idempotência)
- [Detalhe da CP3-I04: Testes próprios significativos](#detalhe-da-cp3-i04-testes-próprios-significativos)
- [Detalhe da CP3-I05: Publicação e execução reproduzível](#detalhe-da-cp3-i05-publicação-e-execução-reproduzível)
- [Detalhe da CP3-I06: Experimentos e diagnóstico](#detalhe-da-cp3-i06-experimentos-e-diagnóstico)
- [Detalhe da CP3-I07: Qualidade objetiva das alterações](#detalhe-da-cp3-i07-qualidade-objetiva-das-alterações)
- [Detalhe da CP3-EXTRA-LINKEDIN: Publicação individual no LinkedIn](#detalhe-da-cp3-extra-linkedin-publicação-individual-no-linkedin)
- [Dossiê dos experimentos (E01 a E05)](#dossiê-dos-experimentos-e01-a-e05)
- [Entrega](#entrega)

## Ponto de partida

| Item | Valor |
|---|---|
| Commit do CP2 | `2e74619a311b38dbcc959888c8f1358b6877e9a1` (merge da PR 10 na `main`) |
| Testes unitários no fim do CP2 | 611 aprovados |
| Build conferido em 02/11/2026 | 0 avisos e 0 erros (SDK .NET 10.0.403) |
| Score do workflow `code-quality` no CP2 | 100/100 (registrado na PR 10) |
| Branch da preparação | `cp3-00-preparacao` |

## Decisões do CP3

| Data | Tema | Decisão | Issue |
|---|---|---|---|
| 02/11/2026 | Estrutura de projetos | O worker referencia `ExpenseHub.Api`. A lógica de relatório fica na Api e o worker é fino (host e adaptador do RabbitMQ). Ele não registra o seed do Admin, os controllers nem a autenticação | CP3-I03 |
| 02/11/2026 | Biblioteca de Excel | `DocumentFormat.OpenXml` (MIT). Versão, licença e dependências a conferir no NuGet antes de adicionar o pacote | CP3-I01 |
| 02/11/2026 | POST sem publicador real | Responde `503` até existir o publicador do RabbitMQ; nunca `202` sem confirmação. Os testes usam um fake do publicador | CP3-I01 |
| 02/11/2026 | Identificador | `Guid`, como `Expense.Id`, igual nas rotas, na mensagem e nas respostas | CP3-I01 |
| 02/11/2026 | Versão das imagens | O workflow publica a cada push na `main`, com tag igual ao SHA completo; o commit final gera as suas próprias imagens | CP3-I05 |
| 02/11/2026 | Arquivo `.http` | Nasce na CP3-I01 e é completado na CP3-I06; as credenciais vêm de um arquivo de ambiente ignorado pelo git | CP3-I01 e CP3-I06 |
| 02/11/2026 | Migrações no Compose | A decidir no plano da CP3-I05, com só duas imagens publicadas e sem apagar dados | CP3-I05 |

## Fora de escopo do CP3

- novo frontend;
- assistente;
- nova autenticação;
- pagamento assíncrono;
- nuvem e Kubernetes;
- escala obrigatória;
- migração geral de banco;
- Transactional Outbox, retentativas automáticas completas e fila de mensagens não processáveis (opcionais, sem bônus).

## Fluxo de trabalho do CP3

1. Uma branch por issue de implementação: `cp3-i01` a `cp3-i07`. A preparação usa `cp3-00-preparacao`. A publicação no LinkedIn não tem branch nem PR.
2. Commits coesos, no padrão convencional, em inglês, com o identificador da issue entre parênteses no fim da primeira linha, por exemplo `feat(reports): add report request endpoint (CP3-I01)`. Sem `#N` no texto do commit.
3. Pull request no repositório da equipe, com título como `CP3-I01: Contrato e resultado do relatório`.
4. A descrição da PR cita a issue central como `Racass/checkpoint-csharpracass-expensehub#11`, sem `Closes`, `Fixes` ou `Resolves`, porque a issue central permanece aberta.
5. O pipeline `code-quality` é analisado antes do merge, e os problemas são corrigidos.
6. Merge com "Create a merge commit"; as branches são mantidas.

## Detalhe da CP3-I01: Contrato e resultado do relatório

Issue central: [11](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/11). Branch: `cp3-i01`. Pontos: 20. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Entrada, período e solicitação persistida conforme o contrato | 4 | Pendente | A preencher ao concluir a issue |
| Listagem, detalhe e estados observáveis | 4 | Pendente | A preencher ao concluir a issue |
| Resumo por estado e totais corretos, inclusive conjunto vazio | 8 | Pendente | A preencher ao concluir a issue |
| Resultado e instante de geração salvos; consulta sem recálculo | 2 | Pendente | A preencher ao concluir a issue |
| Excel válido, fiel ao resultado salvo e com o contrato de download | 2 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue.

## Detalhe da CP3-I02: Autorização e isolamento

Issue central: [12](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/12). Branch: `cp3-i02`. Pontos: 15. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Autenticação e permissão por perfil e escopo | 5 | Pendente | A preencher ao concluir a issue |
| Listagem, detalhe e Excel isolados por solicitante, inclusive Auditor | 5 | Pendente | A preencher ao concluir a issue |
| Solicitante e escopo controlados pelo servidor até o processamento | 5 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue.

## Detalhe da CP3-I03: RabbitMQ, worker e idempotência

Issue central: [13](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/13). Branch: `cp3-i03`. Pontos: 20. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Publicação confirmada, fila durável e mensagens persistentes | 5 | Pendente | A preencher ao concluir a issue |
| Worker processa; consumo confirmado só depois de salvar | 5 | Pendente | A preencher ao concluir a issue |
| Conclusão e resultado consistentes; falha definitiva registrada | 5 | Pendente | A preencher ao concluir a issue |
| Duplicata reconhecida sem novo efeito, com proteção persistente | 5 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. Inclui a janela sem Transactional Outbox entre salvar e publicar.

## Detalhe da CP3-I04: Testes próprios significativos

Issue central: [14](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/14). Branch: `cp3-i04`. Pontos: 15. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Cálculo, conjunto vazio e validação de período | 5 | Pendente | A preencher ao concluir a issue |
| Negação por perfil e escopo; isolamento por solicitante | 5 | Pendente | A preencher ao concluir a issue |
| Duplicata, resultado persistido e Excel, com asserções de comportamento | 5 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. Inclui a tabela cenário por teste e o comando `dotnet test`.

## Detalhe da CP3-I05: Publicação e execução reproduzível

Issue central: [15](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/15). Branch: `cp3-i05`. Pontos: 15. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Duas imagens publicadas e identificadas pelo commit, com testes antes da publicação | 5 | Pendente | A preencher ao concluir a issue |
| Compose executa as imagens publicadas e o RabbitMQ com configuração documentada | 5 | Pendente | A preencher ao concluir a issue |
| Banco e armazenamento compatíveis, inicialização reproduzível e segredos externos | 5 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue.

## Detalhe da CP3-I06: Experimentos e diagnóstico

Issue central: [16](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/16). Branch: `cp3-i06`. Pontos: 10. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| E01 com Excel e E02 correlacionados na mesma solicitação | 3 | Pendente | A preencher ao concluir a issue |
| E03 duplicata depois de reiniciar o worker, sem novo efeito | 2 | Pendente | A preencher ao concluir a issue |
| E04 cenários de autorização reproduzíveis | 2 | Pendente | A preencher ao concluir a issue |
| E05 persistência depois de recriar web e worker | 2 | Pendente | A preencher ao concluir a issue |
| Diagnóstico correto de aceitação, publicação e janela sem Outbox | 1 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. As evidências ficam no [Dossiê dos experimentos](#dossiê-dos-experimentos-e01-a-e05).

## Detalhe da CP3-I07: Qualidade objetiva das alterações

Issue central: [17](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/17). Branch: `cp3-i07`. Pontos: 5. Estado: Pendente.

Critérios de aceite:

| Critério | Pontos | Situação | Evidência |
|---|---:|---|---|
| Sem novos warnings ou diagnósticos atribuíveis ao trabalho | 2 | Pendente | A preencher ao concluir a issue |
| Naming e formatação seguem as regras existentes | 1 | Pendente | A preencher ao concluir a issue |
| Analisadores e verificações não foram enfraquecidos | 1 | Pendente | A preencher ao concluir a issue |
| Higiene da entrega: só fontes, sem binários nem arquivos locais | 1 | Pendente | A preencher ao concluir a issue |

Decisões e como validar: a preencher ao concluir a issue. Inclui a comparação com o commit do CP2.

## Detalhe da CP3-EXTRA-LINKEDIN: Publicação individual no LinkedIn

Issue central: [18](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/18). Sem branch nem PR. Pontos: +1 individual. Estado: Pendente.

Atividade opcional e manual: publicação própria no LinkedIn, com texto próprio, sobre a aplicação, e envio do link no Teams, identificando o autor.

## Dossiê dos experimentos (E01 a E05)

| Experimento | Situação | Evidência |
|---|---|---|
| E01 Fluxo normal | Pendente | A preencher na CP3-I06 |
| E02 Worker parado | Pendente | A preencher na CP3-I06 |
| E03 Duplicata | Pendente | A preencher na CP3-I06 |
| E04 Isolamento | Pendente | A preencher na CP3-I06 |
| E05 Persistência e versão | Pendente | A preencher na CP3-I06 |

### Diagnóstico

A preencher na CP3-I06, explicando:

- aceitação e conclusão;
- worker parado e broker indisponível;
- duplicata e novo POST;
- a janela sem Transactional Outbox.

## Entrega

A preencher no fim do checkpoint:

- SHA do commit do CP2 e SHA do commit final do CP3;
- referências completas das duas imagens e a forma de acesso para a correção;
- comando dos testes unitários e comandos para subir o ambiente.
