CP3-I01 — Contrato e resultado do relatório — 20 pontos #11

# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**20 pontos dos 100 pontos da nota-base coletiva do CP3 (Checkpoint 3).**

## Objetivo

permitir solicitar, acompanhar e baixar um resumo de reembolsos por período, preservando o resultado gerado.

## Dependências

sem issue anterior obrigatória para iniciar. O cálculo pode ser validado isoladamente; o fluxo integrado depende de CP3-I03.

## O que fazer



- [ ] Criar as rotas de solicitação, listagem, detalhe e download Excel do relatório.
- [ ] Validar período e escopo; registrar identidade e estado no servidor.
- [ ] Implementar o cálculo isolável por estado e os totais.
- [ ] Persistir solicitação e resultado, sem alterar estados do reembolso.

## Critérios de aceite e pontuação



| Subcritério | Pontos |
| --- | --- |
| Período, escopo e solicitação persistida conforme contrato. | 4 |
| Listagem, detalhe e estados observáveis. | 4 |
| Resumo por estado e totais corretos, inclusive conjunto vazio. | 8 |
| Resultado e instante de geração salvos; consulta sem recálculo. | 2 |
| Download Excel válido, fiel ao resultado salvo e com contrato publicado. | 2 |

## Casos negativos

datas inválidas ou invertidas não criam trabalho; conjunto vazio retorna zeros; reembolsos fora do período não entram; download de solicitação própria não concluída retorna 409; a planilha não pode ser texto renomeado nem recalcular dados atuais.

## Fora de escopo

interface nova, relatórios com outros agrupamentos, envio por email e alteração do fluxo de pagamento.

## Evidências e validação



Conferir cálculo com dados conhecidos, limites do período, estados com zero, consulta e Excel após alterar um reembolso. O arquivo deve abrir como uma planilha válida e manter os valores salvos. I01 permite validar cálculo isoladamente; o fluxo real com RabbitMQ depende de [CP3-I03](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/13).

## Contrato complementar — 3. Contrato de solicitação e acompanhamento

A API (Application Programming Interface, interface de programação de aplicações) usa as rotas e os campos abaixo. Os exemplos em JSON (JavaScript Object Notation, notação de objetos JavaScript) são ilustrativos nos valores. O tipo do identificador pode seguir a base do grupo; ele deve ser consistente nas rotas, mensagens e respostas.

| Operação | Comportamento |
| --- | --- |
| POST /api/reports | Solicitar relatório com fromDate, toDate e scope. |
| GET /api/reports | Listar somente solicitações criadas pelo usuário autenticado. |
| GET /api/reports/{id} | Consultar andamento e resultado de uma solicitação própria. |
| GET /api/reports/{id}/excel | Baixar o resultado salvo de uma solicitação própria concluída em formato .xlsx. |

```json
{
  "fromDate": "2026-09-01",
  "toDate": "2026-09-30",
  "scope": "Mine"
}
```

Datas no formato ano-mês-dia. Escopos válidos: Mine e Global; não há um ownerId informado pelo cliente.

- [ ] Datas obrigatórias e válidas; fromDate deve ser menor ou igual a toDate.
- [ ] Filtrar ExpenseDate entre as duas datas, inclusive; não usar a data de criação como substituta.
- [ ] scope deve ser Mine ou Global, respeitando a matriz de acesso.
- [ ] O servidor gera o identificador e obtém o solicitante da identidade autenticada.
- [ ] Um novo POST válido cria outra solicitação; deduplicar requisições do cliente não é obrigatório.

Em HTTP (Hypertext Transfer Protocol, protocolo de transferência de hipertexto), a aceitação normal retorna 202, cabeçalho Location apontando para o detalhe e o envelope abaixo. Isso ocorre após persistir a solicitação e obter confirmação da publicação pelo broker. O worker pode avançar o estado antes da próxima consulta.

```json
{
  "id": "rel-001",
  "scope": "Mine",
  "fromDate": "2026-09-01",
  "toDate": "2026-09-30",
  "status": "Pending",
  "createdAt": "2026-10-01T12:00:00Z",
  "startedAt": null,
  "completedAt": null,
  "result": null,
  "error": null
}
```

O detalhe retorna 200 com esse envelope atualizado. A listagem retorna um array de envelopes do próprio usuário; paginação não é obrigatória. Datas de instante usam UTC (Coordinated Universal Time, tempo universal coordenado).

| Estado da solicitação | Campos relevantes |
| --- | --- |
| Pending | createdAt preenchido; ainda sem resultado. |
| Processing | startedAt preenchido; ainda sem resultado. |
| Completed | completedAt e result preenchidos; error nulo. |
| Failed | completedAt e error preenchidos; result nulo. |

Em Failed, error contém code estável e message sanitizada; a redação humana não será comparada literalmente. Falha de publicação sem confirmação não prova falha definitiva do cálculo: ver seção “6. Mensageria, confirmação e idempotência” da apostila.

## Contrato complementar — 4. Resultado e exemplo verificável

Considere somente reembolsos do período e do escopo registrado. Use valores decimais; não introduza erro de ponto flutuante. O estado e o valor são os lidos durante o processamento, não os existentes quando a solicitação foi criada.

| Reembolso de exemplo | Estado | Valor |
| --- | --- | --- |
| Do solicitante, dentro do período. | Approved | R$ 100,00 |
| Do solicitante, dentro do período. | Approved | R$ 50,00 |
| Do solicitante, dentro do período. | Paid | R$ 25,00 |
| De outro funcionário, dentro do período. | Approved | R$ 200,00 |
| Do solicitante, fora do período. | Paid | R$ 500,00 |

Para Mine, o resultado tem três reembolsos e R$ 175,00: dois Approved somando R$ 150,00 e um Paid de R$ 25,00. Para Global, seriam quatro e R$ 375,00, mantendo a exclusão do registro fora do período.

```json
{
  "generatedAt": "2026-10-01T12:00:02Z",
  "totalCount": 3,
  "totalAmount": 175.00,
  "byStatus": [
    {"status": "Draft", "count": 0, "amount": 0.00},
    {"status": "Submitted", "count": 0, "amount": 0.00},
    {"status": "Approved", "count": 2, "amount": 150.00},
    {"status": "Rejected", "count": 0, "amount": 0.00},
    {"status": "Paid", "count": 1, "amount": 25.00}
  ]
}
```

Conteúdo do campo result quando Completed. Os cinco estados são obrigatórios; a ordem dos itens e casas decimais na serialização não alteram o valor.

- [ ] Sem ocorrências: totalCount e totalAmount zero e os cinco estados com zero.
- [ ] Salvar o resultado e generatedAt; consultar não executa novamente o cálculo.
- [ ] Alterar um reembolso depois da conclusão não altera o relatório já salvo.
- [ ] Não é exigido congelamento dos dados na solicitação nem snapshot transacional entre múltiplas consultas.

## Download Excel obrigatório

GET /api/reports/{id}/excel retorna uma planilha .xlsx válida, com os dados do resultado persistido, não uma nova consulta aos reembolsos. O arquivo pode ser montado no momento do download a partir desse resultado, sem exigir outra mensagem na fila.

| Contrato do download | Exigência |
| --- | --- |
| Sucesso. | 200, com arquivo .xlsx real; não CSV (Comma-Separated Values, valores separados por vírgula) ou texto renomeado. |
| Content-Type. | application/vnd.openxmlformats-officedocument.spreadsheetml.sheet |
| Content-Disposition. | attachment, com nome de arquivo .xlsx, por exemplo report-{id}.xlsx. |
| Conteúdo. | Identificador, escopo, período, instante de geração e tabela com os cinco estados, quantidades, valores e total geral. |
| Tipos e fidelidade. | Contagens e valores em células numéricas; dados iguais ao resultado persistido, inclusive zeros e conjunto vazio. |
| Autorização e estado. | Somente o solicitante autenticado; 404 para inexistente/alheio, 409 para próprio não Completed. |

O arquivo deve abrir como planilha em Excel ou aplicativo compatível. A biblioteca é livre, respeitada sua licença; não é necessário serviço externo ou formatação visual elaborada.

## Regras gerais de avaliação

Nota-base = pontos do grupo ÷ 10. Aplicar o menor teto: não compila, 2; autorização sistematicamente ausente, 4; sem testes próprios significativos, 8. O bônus individual de LinkedIn não ultrapassa nota 10 nem esses tetos. Os critérios completos constam da apostila. A nota do CP2 (Checkpoint 2) não é recalculada.

## Entrega no repositório do grupo

Branch sugerida: `cp3-i01`.

Na descrição da pull request, cite `Racass/checkpoint-csharpracass-expensehub#11`. Não use Closes, Fixes ou Resolves e não feche a issue central: ela é a especificação comum dos grupos.

Prazo: 04/10 23:59. Canal: Teams.






---





CP3-I02 — Autorização e isolamento — 15 pontos #12

# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**15 pontos dos 100 pontos da nota-base coletiva do CP3 (Checkpoint 3).**

## Objetivo

impedir que criação, processamento, consulta ou exportação ultrapassem o escopo autorizado do solicitante.

## Dependências

CP3-I01. O filtro do worker deve ser integrado com CP3-I03.

## O que fazer



- [ ] Permitir Mine para Employee e Global para Auditor.
- [ ] Obter o solicitante da identidade autenticada.
- [ ] Filtrar listagem, detalhe e download Excel pelo criador da solicitação.
- [ ] Preservar o escopo registrado quando o worker calcula o relatório.

## Critérios de aceite e pontuação



| Subcritério | Pontos |
| --- | --- |
| Autenticação e permissão por perfil/escopo. | 5 |
| Listagem, detalhe e Excel isolados por solicitante, inclusive Auditor. | 5 |
| Solicitante e escopo controlados pelo servidor até o processamento. | 5 |

## Casos negativos

acesso anônimo retorna 401; Employee não solicita Global; Admin não ganha privilégio implícito; solicitante não é aceito do cliente; detalhe e Excel alheios retornam 404, mesmo para Auditor; Mine nunca soma despesas de outro funcionário.

## Fora de escopo

nova autenticação, novos perfis, nova matriz geral do ExpenseHub ou privilégio de consultar solicitações alheias para Auditor.

## Evidências e validação



Usar duas contas Employee e uma Auditor. Conferir anonimato, Global negado para Employee, Mine restrito ao próprio usuário e 404 para detalhe e Excel de solicitação alheia. Não publicar os tokens nas evidências.

## Contrato complementar — 5. Autorização e códigos de resposta

| Perfil | Mine | Global | Listagem e detalhe |
| --- | --- | --- | --- |
| Employee | Sim | Não | Somente solicitações próprias. |
| Auditor | Não, salvo se também Employee | Sim | Somente solicitações próprias. |
| Employee e Auditor | Sim | Sim | Somente solicitações próprias. |
| Admin, Approver ou Finance sem os perfis acima | Não | Não | Não ganham acesso a solicitações alheias. |

As consultas e o download Excel exigem autenticação e propriedade da solicitação. Mesmo Auditor consulta apenas as que criou; seu privilégio global é sobre os dados usados em seu relatório. O proprietário não pode ser definido pelo corpo da requisição, parâmetros arbitrários de cliente ou conteúdo não confiável da mensagem.

| Cenário | Resposta |
| --- | --- |
| Requisição sem autenticação. | 401 |
| Perfil sem permissão de solicitar o escopo. | 403 |
| Detalhe ou Excel de solicitação inexistente ou alheia. | 404 |
| Excel de solicitação própria ainda não concluída ou Failed. | 409 |
| Datas ou escopo inválidos. | 400 |
| Solicitação persistida e publicação confirmada. | 202 e Location |
| Banco indisponível antes de registrar. | Erro explícito; sem 202 e sem identificador fictício. |
| Publicação sem confirmação após registrar. | 503 com id persistido e erro sanitizado. |

Ao processar Mine, o worker filtra pelo solicitante registrado no servidor. Registrar corretamente o proprietário, mas calcular sobre todos os usuários, também viola o isolamento.

## Regras gerais de avaliação

Nota-base = pontos do grupo ÷ 10. Aplicar o menor teto: não compila, 2; autorização sistematicamente ausente, 4; sem testes próprios significativos, 8. O bônus individual de LinkedIn não ultrapassa nota 10 nem esses tetos. Os critérios completos constam da apostila. A nota do CP2 (Checkpoint 2) não é recalculada.

## Entrega no repositório do grupo

Branch sugerida: `cp3-i02`.

Na descrição da pull request, cite `Racass/checkpoint-csharpracass-expensehub#12`. Não use Closes, Fixes ou Resolves e não feche a issue central: ela é a especificação comum dos grupos.

Prazo: 04/10 23:59. Canal: Teams.




---



CP3-I03 — RabbitMQ, worker e idempotência — 20 pontos #13


# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**20 pontos dos 100 pontos da nota-base coletiva do CP3 (Checkpoint 3).**

## Objetivo

deslocar o cálculo para um worker separado, preservando o resultado diante de espera, reentrega e reinício.

## Dependências

CP3-I01. Preservar as regras de acesso de CP3-I02 ao carregar o registro da solicitação.

## O que fazer



- [ ] Configurar fila durável, mensagens persistentes e confirmação de publicação.
- [ ] Consumir por um worker C# separado e confirmar consumo após persistência.
- [ ] Registrar conclusão e resultado consistentemente.
- [ ] Evitar novo efeito ao receber uma solicitação já concluída.
- [ ] Registrar falha definitiva sem reencaminhamento infinito.
- [ ] Tratar falha de publicação explicitamente e explicar a janela sem outbox.

## Critérios de aceite e pontuação



| Subcritério | Pontos |
| --- | --- |
| Publicação confirmada, fila durável e mensagens persistentes. | 5 |
| Worker processa; consumo é confirmado após salvar. | 5 |
| Conclusão/resultado consistentes e falha definitiva registrada. | 5 |
| Duplicata reconhecida sem novo efeito, com proteção persistente. | 5 |

## Casos negativos

sem confirmação de publicação não retornar 202; falha ao persistir não pode ser confirmada como sucesso; duplicata não cria resultado adicional; reinício não elimina a proteção; falha definitiva não pode entrar em reencaminhamento infinito; Processing não pode ser ignorado para sempre.

## Fora de escopo

escala obrigatória, entrega exatamente uma vez, Transactional Outbox, retentativas completas e fila de mensagens não processáveis obrigatória.

## Evidências e validação



Demonstrar fluxo real, espera com worker parado e retomada. Reentregar mensagem concluída após reiniciar o worker para excluir uma proteção apenas em memória. Consulte seção “6. Mensageria, confirmação e idempotência” da apostila.

## Contrato complementar — 6. Mensageria, confirmação e idempotência

O contrato mínimo da mensagem é reportId, com o mesmo tipo de identificador adotado pela aplicação. O worker carrega período, escopo e solicitante do registro persistido. Documente nome da fila e configuração usada para publicar e consumir.

```json
{"reportId": "rel-001"}
```

Exemplo de mensagem. Reentregar a mesma mensagem não equivale a fazer outro POST.

| Regra obrigatória | O que demonstrar |
| --- | --- |
| Fila durável e mensagens persistentes. | Configuração e armazenamento do RabbitMQ documentados. |
| Confirmação de publicação. | 202 somente após confirmação; falha sem sucesso falso. |
| Confirmação de consumo após salvar. | Nenhum reconhecimento antecipado à conclusão persistida. |
| Idempotência persistente. | Mesmo reportId concluído não gera outro resultado, inclusive após reiniciar o worker. |
| Conclusão consistente. | Completed e resultado são persistidos de forma consistente. |
| Falha definitiva. | Failed com erro sanitizado; sem reencaminhamento infinito. |

Uma confirmação de consumo é um acknowledgement. Se o worker cair antes dela, pode haver reentrega. O cálculo pode ser repetido; o efeito persistido não. Pending ou Processing não podem ser descartados para sempre apenas porque uma tentativa anterior começou.

Uma falha ao salvar o resultado ou o próprio estado de erro não permite confirmar como se a persistência tivesse ocorrido. Diferencie falha de negócio definitiva de indisponibilidade transitória e documente como uma solicitação Processing pode ser retomada.

## Limite sem Transactional Outbox

salvar no banco e publicar são operações distintas. Uma queda entre elas pode deixar Pending sem mensagem. Uma confirmação perdida também pode deixar a publicação incerta. Retorne 503 com id quando não houver confirmação, preserve o registro e documente a limitação; não afirme que reiniciar o worker recupera uma mensagem inexistente.

Se o banco falhar antes de registrar, retorne erro explícito sem inventar um identificador persistido. Se a publicação falhar depois de registrar, o usuário pode consultar o id retornado; novo POST cria outro id. Não exigir republicação automática, entrega exatamente uma vez ou recuperação universal de falhas.

Os experimentos obrigatórios usam um worker. Escala, retentativas automáticas completas, Transactional Outbox e DLQ (Dead Letter Queue, fila de mensagens não processáveis) são opcionais, sem bônus e sem necessidade para nota máxima. Não reencaminhar uma falha definitiva para sempre continua obrigatório.

## Regras gerais de avaliação

Nota-base = pontos do grupo ÷ 10. Aplicar o menor teto: não compila, 2; autorização sistematicamente ausente, 4; sem testes próprios significativos, 8. O bônus individual de LinkedIn não ultrapassa nota 10 nem esses tetos. Os critérios completos constam da apostila. A nota do CP2 (Checkpoint 2) não é recalculada.

## Entrega no repositório do grupo

Branch sugerida: `cp3-i03`.

Na descrição da pull request, cite `Racass/checkpoint-csharpracass-expensehub#13`. Não use Closes, Fixes ou Resolves e não feche a issue central: ela é a especificação comum dos grupos.

Prazo: 04/10 23:59. Canal: Teams.




---



CP3-I04 — Testes próprios significativos — 15 pontos #14




# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**15 pontos dos 100 pontos da nota-base coletiva do CP3 (Checkpoint 3).**

## Objetivo

detectar regressões das novas regras com testes de comportamento independentes de serviços externos.

## Dependências

CP3-I02 e CP3-I03 para concluir a suíte; escrever os testes durante o desenvolvimento, não apenas no fechamento.

## O que fazer



- [ ] Testar cálculo, conjunto vazio e período inválido.
- [ ] Testar escopo negado e solicitação alheia.
- [ ] Testar duplicata, resultado persistido e contrato do download Excel.
- [ ] Manter testes unitários independentes de banco, rede e RabbitMQ reais.

## Critérios de aceite e pontuação



| Subcritério | Pontos |
| --- | --- |
| Cálculo, conjunto vazio e validação de período. | 5 |
| Negação por perfil/escopo e isolamento por solicitante. | 5 |
| Duplicata, resultado persistido e Excel, com asserções de comportamento. | 5 |

## Casos negativos

suíte que depende de banco ou RabbitMQ reais não substitui testes unitários isolados; testes antigos ou da ferramenta de qualidade não comprovam as novas regras; executar sem verificar resultado não demonstra valor; quantidade e cobertura percentual não concedem pontos.

## Fora de escopo

percentual mínimo de cobertura, biblioteca de testes específica ou obrigação de automatizar todos os experimentos operacionais.

## Evidências e validação



Documentar um comando `dotnet test` reproduzível sem dependências externas para os testes unitários. Testes operacionais ficam em [CP3-I06](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/16); quantidade de métodos e cobertura percentual não concedem pontos.

## Contrato complementar — 8. Testes próprios e verificação do contrato

Os testes unitários das novas regras executam sem banco, rede ou RabbitMQ reais. Use as abstrações, fakes ou doubles necessários, sem impor um framework específico. Teste resultados e efeitos, não apenas a chamada de um método.

| Cenário mínimo | Asserção relevante |
| --- | --- |
| Dados conhecidos e limites inclusivos do período. | Quantidade e soma corretas por estado e no total. |
| Nenhum reembolso elegível. | Cinco estados e totais com zero. |
| Data inicial posterior à final. | Solicitação rejeitada sem publicação. |
| Employee solicita Global. | Negação sem criar processamento autorizado. |
| Solicitação de outro usuário. | Não expõe detalhe nem resultado. |
| Mesma mensagem novamente. | Nenhum novo resultado ou efeito de conclusão. |
| Consulta depois de alterar um reembolso. | Retorna o resultado persistido, sem recálculo. |
| Download do relatório concluído. | Planilha válida, cabeçalhos corretos e números iguais ao resultado salvo. |
| Download pendente, Failed ou alheio. | 409 para próprio não concluído; 404 para alheio, sem arquivo ou dados expostos. |

Documente o comando de execução da suíte unitária. Testes de integração podem existir separadamente; as evidências operacionais abaixo são obrigatórias, mas não exigem um framework de automação. Não há pontuação por cobertura percentual ou quantidade bruta de métodos.

Testes dos scripts de qualidade ou testes antigos sem relação com relatórios não substituem os testes das novas regras. A geração destes materiais não valida nenhuma implementação dos grupos.

## Regras gerais de avaliação

Nota-base = pontos do grupo ÷ 10. Aplicar o menor teto: não compila, 2; autorização sistematicamente ausente, 4; sem testes próprios significativos, 8. O bônus individual de LinkedIn não ultrapassa nota 10 nem esses tetos. Os critérios completos constam da apostila. A nota do CP2 (Checkpoint 2) não é recalculada.

## Entrega no repositório do grupo

Branch sugerida: `cp3-i04`.

Na descrição da pull request, cite `Racass/checkpoint-csharpracass-expensehub#14`. Não use Closes, Fixes ou Resolves e não feche a issue central: ela é a especificação comum dos grupos.

Prazo: 04/10 23:59. Canal: Teams.




---





CP3-I05 — Publicação e execução reproduzível — 15 pontos #15








# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**15 pontos dos 100 pontos da nota-base coletiva do CP3 (Checkpoint 3).**

## Objetivo

permitir executar a versão entregue a partir das duas imagens publicadas, com RabbitMQ e banco compatível.

## Dependências

preparação independente. A comprovação final usa a web de CP3-I01 e o worker de CP3-I03.

## O que fazer



- [ ] Criar Dockerfiles e publicação das duas imagens após os testes.
- [ ] Identificar imagens pelo commit e documentar como a correção as acessa.
- [ ] Configurar Compose para usar imagens publicadas e RabbitMQ.
- [ ] Validar banco acessível pelos dois processos e armazenamento persistente.
- [ ] Fornecer instruções de inicialização e exemplo de configuração sem segredos.

## Critérios de aceite e pontuação



| Subcritério | Pontos |
| --- | --- |
| Duas imagens publicadas e identificadas, com testes anteriores à publicação. | 5 |
| Compose executa imagens publicadas e RabbitMQ com configuração documentada. | 5 |
| Banco/armazenamento compatíveis, inicialização reproduzível e segredos externos. | 5 |

## Casos negativos

somente build local ou latest não identifica a versão entregue; imagem de outro projeto não comprova implementação; web e worker não podem usar bases divergentes; credenciais não podem ser versionadas; remover volumes não comprova persistência.

## Fora de escopo

nuvem, Kubernetes, registry específico ou migração geral obrigatória do provedor relacional.

## Evidências e validação



Executar `docker compose pull`, iniciar o ambiente e conferir referências das imagens. Recriar somente web e worker e consultar um resultado anterior, sem remover volumes. Os comandos devem corresponder aos nomes reais dos serviços do grupo.

## Contrato complementar — 7. Imagens, banco e execução reproduzível

- [ ] Publicar duas imagens reais: aplicação web e worker, após aprovação dos testes.
- [ ] Identificar ambas pelo commit final e informar as referências completas e a forma de acesso para correção, sem enviar credenciais.
- [ ] Usar essas imagens no Compose; apenas construir localmente ou usar latest não comprova a entrega.
- [ ] Incluir RabbitMQ e sua configuração persistente no Compose.
- [ ] Fornecer exemplo de configuração, inicialização do banco e instruções sem segredos.

Use o provedor relacional do grupo quando compatível. Aplicação e worker precisam acessar o mesmo estado. O banco pode ser externo ao Compose, desde que a execução esteja documentada e seja reproduzível.

| Situação | Cuidado |
| --- | --- |
| SQLite | Arquivo persistente compartilhado e configuração coerente para os dois processos. |
| LocalDB | Não acompanha automaticamente contêineres Linux; validar um caminho compatível antes da integração. |
| Servidor relacional externo | Documentar acesso, inicialização e configuração; não versionar segredos. |
| Identidade e proteção de dados | Preservar configurações e chaves necessárias ao funcionamento da autenticação entre execuções. |

Não há migração geral obrigatória de banco. Se a base não for compatível com a execução exigida, discuta o impedimento com o professor antes de assumir que a configuração funcionará. Não apague volumes ou dados para esconder problemas.

```powershell
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail 80 worker
docker compose stop worker
docker compose up -d worker
```

Referência operacional: ajuste worker ao nome real do serviço. Antes de executar, configure o ambiente e confirme que está no projeto do grupo.

Mantenha registros correlacionados por reportId, com eventos de publicação, início, conclusão, duplicata e falha. Não registre tokens, senhas, cabeçalhos de autenticação ou strings de conexão sensíveis. Publicar uma imagem não comprova que o ambiente a executou: guarde também evidência da versão em execução.

## Regras gerais de avaliação

Nota-base = pontos do grupo ÷ 10. Aplicar o menor teto: não compila, 2; autorização sistematicamente ausente, 4; sem testes próprios significativos, 8. O bônus individual de LinkedIn não ultrapassa nota 10 nem esses tetos. Os critérios completos constam da apostila. A nota do CP2 (Checkpoint 2) não é recalculada.

## Entrega no repositório do grupo

Branch sugerida: `cp3-i05`.

Na descrição da pull request, cite `Racass/checkpoint-csharpracass-expensehub#15`. Não use Closes, Fixes ou Resolves e não feche a issue central: ela é a especificação comum dos grupos.

Prazo: 04/10 23:59. Canal: Teams.




---


CP3-I06 — Experimentos e diagnóstico — 10 pontos #16

# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**10 pontos dos 100 pontos da nota-base coletiva do CP3 (Checkpoint 3).**

## Objetivo

demonstrar o funcionamento integrado e explicar o comportamento observado usando registros correlacionados.

## Dependências

CP3-I02, CP3-I03 e CP3-I05.

## O que fazer



- [ ] Executar os cinco experimentos de seção “9. Cinco experimentos obrigatórios” da apostila.
- [ ] Registrar identificadores, comandos, resultados e logs sanitizados.
- [ ] Explicar aceitação, conclusão, reentrega e efeito idempotente.
- [ ] Distinguir worker parado, broker indisponível e janela sem outbox.

## Critérios de aceite e pontuação



| Subcritério | Pontos |
| --- | --- |
| Fluxo normal com Excel e worker parado/retomado, correlacionando a mesma solicitação. | 3 |
| Duplicata após reinício, sem novo efeito. | 2 |
| Cenários de autorização reproduzíveis. | 2 |
| Persistência após recriar web e worker. | 2 |
| Diagnóstico correto de aceitação, publicação e janela sem outbox. | 1 |

## Casos negativos

captura de testes unitários não substitui fluxo real; outro identificador não prova recuperação da mesma solicitação; novo POST não prova duplicata; reiniciar o worker não recupera mensagem que nunca foi publicada; registros com segredos não podem ser entregues.

## Fora de escopo

gravação obrigatória, experimento adicional de interrupção do broker, escala e recuperação universal de falhas.

## Evidências e validação



Entregar um dossiê curto com evidências por cenário. Gravação é complementar, não obrigatória; não substitui código ou resultados reproduzíveis.

## Contrato complementar — 9. Cinco experimentos obrigatórios

| Experimento | Procedimento e evidência |
| --- | --- |
| E01 — Fluxo normal | Autenticar, solicitar, guardar id, acompanhar até Completed, baixar o Excel e conferir resultado e planilha contra dados conhecidos. |
| E02 — Worker parado | Parar só o worker; solicitar; observar mensagem disponível e relatório não concluído; retomar e acompanhar o mesmo id até Completed. |
| E03 — Duplicata | Após concluir, reiniciar o worker e republicar a mensagem com o mesmo reportId; comparar resultado e registros, sem novo efeito. |
| E04 — Isolamento | Employee não solicita Global nem consulta ou baixa Excel alheio; Auditor solicita Global mas não consulta nem baixa relatório criado por outro usuário. |
| E05 — Persistência e versão | Recriar apenas web e worker usando imagens publicadas; consultar um resultado anterior e comprovar versão e dados preservados. |

Para E03, documente como republicar o payload da mensagem na fila do ambiente de estudo, por exemplo pelo painel de gerenciamento do RabbitMQ. Não é necessário criar um endpoint de negócio para essa operação nem alterar o banco para simular sucesso.

No RabbitMQ, Ready indica mensagem disponível; Unacked indica mensagem entregue sem confirmação de consumo. Essas métricas não são os estados Pending e Processing da aplicação. Correlacione a observação com o identificador e os registros do worker.

E05 pode usar `docker compose up -d --force-recreate web worker`, ajustado aos nomes reais. Não recrie o banco como se fosse um teste de perda de dados e não remova volumes. Tokens ficam fora do dossiê; se uma nova autenticação for necessária, isso não invalida a prova de persistência do resultado.

Worker parado e broker indisponível não são a mesma falha. O tratamento de publicação sem confirmação deve existir no código e ser verificável por teste; um sexto experimento operacional de interrupção do broker não é obrigatório.

## Regras gerais de avaliação

Nota-base = pontos do grupo ÷ 10. Aplicar o menor teto: não compila, 2; autorização sistematicamente ausente, 4; sem testes próprios significativos, 8. O bônus individual de LinkedIn não ultrapassa nota 10 nem esses tetos. Os critérios completos constam da apostila. A nota do CP2 (Checkpoint 2) não é recalculada.

## Entrega no repositório do grupo

Branch sugerida: `cp3-i06`.

Na descrição da pull request, cite `Racass/checkpoint-csharpracass-expensehub#16`. Não use Closes, Fixes ou Resolves e não feche a issue central: ela é a especificação comum dos grupos.

Prazo: 04/10 23:59. Canal: Teams.




---



CP3-I07 — Qualidade objetiva das alterações — 5 pontos #17

# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**5 pontos dos 100 pontos da nota-base coletiva do CP3 (Checkpoint 3).**

## Objetivo

manter as verificações e convenções do projeto sem introduzir diagnósticos atribuíveis às alterações.

## Dependências

transversal, sem issue anterior obrigatória; comparar o commit do CP2 com a entrega final.

## O que fazer



- [ ] Preservar analisadores e configurações de qualidade.
- [ ] Resolver diagnósticos atribuíveis às alterações.
- [ ] Seguir as convenções objetivas já adotadas no projeto.
- [ ] Entregar fontes sem binários, segredos ou arquivos locais desnecessários.

## Critérios de aceite e pontuação



| Subcritério | Pontos |
| --- | --- |
| Sem novos warnings ou diagnósticos objetivos atribuíveis ao trabalho. | 2 |
| Alterações seguem naming e formatação das regras existentes. | 1 |
| Analisadores e verificações não foram enfraquecidos. | 1 |
| Higiene da entrega: fontes, sem binários ou arquivos locais indevidos. | 1 |

## Casos negativos

remover analisadores, reduzir severidades ou excluir arquivos para esconder achados não é correção; arquivos binários e configurações locais indevidas não pertencem aos fontes; score geral não equivale automaticamente ao score das alterações.

## Fora de escopo

recalcular o CP2, cobrar preferências estéticas ou descontar problemas antigos sem relação com a evolução.

## Evidências e validação



Usar build, verificações existentes e comparação entre commits. Não confundir o score geral do pipeline com uma nota diferencial automática. Este checkpoint não fornece um corretor pronto.

## Regras gerais de avaliação

Nota-base = pontos do grupo ÷ 10. Aplicar o menor teto: não compila, 2; autorização sistematicamente ausente, 4; sem testes próprios significativos, 8. O bônus individual de LinkedIn não ultrapassa nota 10 nem esses tetos. Os critérios completos constam da apostila. A nota do CP2 (Checkpoint 2) não é recalculada.

## Entrega no repositório do grupo

Branch sugerida: `cp3-i07`.

Na descrição da pull request, cite `Racass/checkpoint-csharpracass-expensehub#17`. Não use Closes, Fixes ou Resolves e não feche a issue central: ela é a especificação comum dos grupos.

Prazo: 04/10 23:59. Canal: Teams.








---



CP3-EXTRA-LINKEDIN — Publicação individual no LinkedIn — 1 ponto extra #18



# ExpenseHub em operação — CP3 (Checkpoint 3)

## Peso

**Atividade opcional: 1 ponto individual, limitado à nota 10 e aos tetos. Não integra os 100 pontos coletivos.**

## Objetivo

comunicar o valor empresarial da aplicação e refletir sobre a própria aprendizagem.

## Dependências

opcional, sem bloquear as issues obrigatórias; usar a experiência real do projeto.

## O que fazer



- [ ] Publicar no próprio LinkedIn, com suas próprias palavras, explicando a ferramenta construída.
- [ ] Descrever como a aplicação se integraria ao dia a dia de uma empresa e ajudaria a controlar gastos.
- [ ] Relatar o que aprendeu e as ferramentas utilizadas para construir a aplicação.
- [ ] Enviar individualmente o link da publicação no Teams, identificando o autor.

## Critérios de aceite e pontuação



A publicação deve atender a todos os pontos, ter texto próprio e evidência acessível à correção. A atividade concede 1 ponto na nota individual, limitado à nota 10 e sem ultrapassar os tetos. Não exige engajamento, quantidade de seguidores ou curtidas.

## Casos negativos

publicação de outro integrante não concede ponto; texto copiado, simples compartilhamento ou rascunho não publicado não atendem; o bônus não ultrapassa nota 10 nem os tetos.

## Fora de escopo

métricas de engajamento, obrigação de publicar dados pessoais ou despesas reais e distribuição do bônus ao grupo inteiro.

## Evidências e validação



Conferir autor, conteúdo e link. Se houver restrição de acesso, anexar também uma captura legível da publicação real; um rascunho não publicado não atende. Não expor credenciais, dados pessoais ou dados reais de despesas.

## Contrato complementar — 15. Ponto extra individual — publicação no LinkedIn

Cada integrante pode receber 1 ponto extra individual ao publicar no próprio LinkedIn, com suas próprias palavras, sobre a aplicação construída. A atividade é opcional: a publicação de um integrante não concede pontos aos colegas.

- [ ] Explique a ferramenta construída e suas funcionalidades, incluindo relatórios, processamento em segundo plano e exportação Excel.
- [ ] Descreva como ela se integraria ao dia a dia de uma empresa e ajudaria a empresa a controlar gastos.
- [ ] Conte o que aprendeu durante a construção da aplicação.
- [ ] Apresente as ferramentas e tecnologias utilizadas e suas funções no projeto.

Cada aluno deve utilizar suas próprias palavras e seu próprio relato de aprendizagem. Copiar o mesmo texto entre integrantes ou apenas compartilhar a publicação de um colega não atende à atividade.

Envie individualmente o link da publicação no Teams, identificando o autor, junto à entrega. Se o acesso ao link for restrito, inclua também uma captura legível da publicação real; um rascunho não publicado não atende. Não são exigidos curtidas, seguidores ou engajamento.

A publicação que atende a todos os critérios concede 1 ponto na nota individual. A nota final permanece limitada a 10 e aos tetos de compilação, autorização e testes; o bônus não remove essas restrições.

| Situação | Nota individual |
| --- | --- |
| Nota-base 8,2; publicação elegível; sem teto aplicável. | 9,2 |
| Nota-base 9,7; publicação elegível; sem teto aplicável. | 10,0 |
| Nota-base 8,2; publicação elegível; teto de autorização 4,0. | 4,0 |
| Nota-base 8,2; somente outro integrante publicou. | 8,2, se não houver teto aplicável. |

Utilize apenas dados fictícios ou informações que possam ser divulgadas. Não publique tokens, senhas, dados pessoais ou dados reais de despesas. O ticket [CP3-EXTRA-LINKEDIN](https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/18) resume esta atividade opcional.

Prazo: 04/10 23:59. Canal: Teams.
