# CP3-00: Reorganização da documentação (README, CHECKPOINT-2 e CHECKPOINT-3)

## Issue implementada

Preparação do Checkpoint 3 (CP3-00). Não há issue central correspondente: é a etapa que organiza a documentação antes das issues CP3-I01 a CP3-I07. A mudança é somente de documentação: nenhum código de produção, pacote, workflow, `docs/`, `scripts/` ou `.github/` foi alterado.

## Resumo técnico

O README tinha 1044 linhas e misturava o estado vivo do sistema com o registro histórico do CP2. Agora:

- **`CHECKPOINT-2.md` (novo, 780 linhas):** registro congelado do CP2, com o nome do Pedro. Recebe, movidos do README sem reescrever, a apresentação e o "Estado atual" do CP2, as tabelas Equipe e Issues, Decisões de projeto, Fora de escopo, Fluxo de trabalho da equipe e as dez seções "Detalhe da I01 a I10". Ganha só um título, uma nota e um Sumário (12 linhas novas).
- **`CHECKPOINT-3.md` (novo, 199 linhas):** esqueleto do CP3, com só o nome do Renan: ponto de partida, decisões de 02/11, fora de escopo, fluxo de trabalho, uma seção por issue com os critérios da rubrica (todos pendentes), dossiê E01 a E05 e entrega.
- **`README.md` (316 linhas):** página de entrada. Texto novo (57 linhas): apresentação, Sumário, Equipe com uma coluna por checkpoint, uma seção curta por checkpoint e as tabelas de requisitos (CP2: I01 a I10; CP3: CP3-I01 a CP3-I07 e o extra do LinkedIn). Mantém inteiras, como estavam, as seções vivas: Endpoints, Como rodar, Banco de dados, Arquitetura, Testes, Qualidade, Solução de problemas e Uso de IA.

Mapa das 1044 linhas do README original:

| Linhas originais | Seção | Destino |
|---|---|---|
| 1-4 | Título e badge | README |
| 5-8 | Apresentação e Estado atual do CP2 | CHECKPOINT-2.md |
| 9-23 e 34 | Sumário antigo | descartado (cada arquivo tem o seu) |
| 24-33 | Links do Sumário para os Detalhes | CHECKPOINT-2.md |
| 35-56 | Equipe e Issues do CP2 | CHECKPOINT-2.md |
| 57-275 | Endpoints, Como rodar, Banco de dados, Arquitetura, Testes e Qualidade | README |
| 276-328 | Decisões de projeto | CHECKPOINT-2.md |
| 329-361 | Solução de problemas | README |
| 362-375 | Fora de escopo | CHECKPOINT-2.md |
| 376-378 | Uso de IA | README |
| 379 | Linha em branco final | descartada |
| 380-390 | Fluxo de trabalho da equipe | CHECKPOINT-2.md |
| 391-1044 | Detalhe da I01 a I10 | CHECKPOINT-2.md |

Conta: 259 linhas antigas no README + 768 no CHECKPOINT-2.md + 17 descartadas = 1044.

## Decisões e concessões

- A movimentação foi feita com `sed -n 'A,Bp'`, sem reescrever o texto, como decidido no PROGRESSO.
- **Decisões de projeto** foi para o CHECKPOINT-2.md por ser o racional histórico do CP2. **Fora de escopo** também, porque lista "Docker" e "deploy", o oposto do CP3. O **Fluxo de trabalho** do CP2 foi junto; o do CP3 nasce no CHECKPOINT-3.md.
- **Testes** e **Qualidade** ficam inteiros no README, com a contagem e os scores por issue do CP2, porque o CP3 vai editar essas seções. Se for melhor, mover esses trechos para o CHECKPOINT-2.md é um passo posterior.
- Foram corrigidas 7 linhas do texto movido (links e referências que apontavam para seções que mudaram de arquivo): 2 no início do CHECKPOINT-2.md (linhas 5 e 7 do original), 4 nos Detalhes (linhas 401, 487, 1021 e 1027 do original) e 1 no README (linha 245 do original). O plano previa 6; a sétima (linha 5, "este README descreve") apareceu na verificação.
- Pendência fora desta PR: o título da seção "Decisions" do `CLAUDE.md` (linha 133) ainda manda documentar o raciocínio no README. Não foi alterado por não fazer parte do pedido.

## Como validar

```shell
git show main:README.md | wc -l          # 1044
wc -l README.md CHECKPOINT-2.md CHECKPOINT-3.md
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
pwsh ./scripts/Invoke-CodeQuality.ps1 -SkipGitleaks
```

Depois do push, abrir no GitHub e conferir que os links funcionam: README, tabela do CP2, "Detalhe" da I08; README, seção Testes, "O que a suíte não cobre"; CHECKPOINT-2.md, Sumário, "Decisões de projeto"; CHECKPOINT-3.md, Sumário, "Detalhe da CP3-I03".

## Evidências relevantes

- Etapa A (só os blocos movidos, sem texto novo): 259 + 768 = 1027 linhas, e o hash de `sort | md5sum` dessas linhas é igual ao do README original sem as 17 descartadas. Nenhuma linha perdida ou duplicada.
- Script de verificação (somente leitura, 83 conferências, todas aprovadas): cada bloco do original aparece contíguo e na ordem no arquivo de destino; as únicas diferenças em relação ao original são as 7 linhas com link corrigido; cada título do original existe uma vez no arquivo de destino; todos os links locais dos três arquivos resolvem (inclusive as 25 âncoras do README original); sem travessões, sem CR, sem espaço no fim da linha, newline final; tabelas com o número certo de colunas; células de Peso, Estado e PR da tabela do CP2 iguais às do original; `CHECKPOINT-3.md` sem o nome do Pedro.
- `diff` dos blocos com correção: só as linhas esperadas diferem.
- `dotnet build`: 0 avisos e 0 erros. `dotnet test`: 611 aprovados, 0 falhas.
- Script de qualidade (`-SkipGitleaks`): **100/100**, 20 em cada categoria, sem bloqueantes. Único achado: `FIAP0002` (informativo, Gitleaks ignorado por causa da opção `-SkipGitleaks`; o Gitleaks roda no CI).

## Impactos em segurança e autorização

Nenhum. Só documentação. Nenhum segredo foi adicionado: o texto movido é o mesmo que já passou no Gitleaks do CI, e os exemplos continuam com marcadores como `<senha do Admin>`.

## Checklist

- [ ] Critérios de aceite atendidos
- [ ] Casos negativos validados
- [ ] Autorização revisada
- [ ] Testes unitários adicionados ou atualizados
- [ ] Build sem erros
- [ ] Pipeline analisado
- [ ] Documentação atualizada
