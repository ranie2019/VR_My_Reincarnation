# Ataque principal do cajado — 2026-11-06

O lançamento copia o ataque base sorteado e persistido de CajadoPopup para a BolaDeFogo. O dano não soma Força automaticamente. Soltar, trocar ou destruir o cajado depois do lançamento não altera a magia que já foi disparada.

## Fórmula atual

`dano = ataque principal do cajado + bônus secundário da magia`

O bônus secundário começa em zero e pode ser configurado no prefab da BolaDeFogo. O elemento também fica identificado (Neutro, Fogo ou Planta). Ainda não existem sorteio de crítico nem multiplicadores de vantagem elemental. Valores negativos são limitados a zero e a soma é protegida contra overflow.

A área de fogo recebe o mesmo contexto antes de inicializar: esse dano é aplicado por pulso. Intervalo, duração e opções existentes de explosão instantânea continuam configuráveis. Portanto, vários pulsos podem causar um dano acumulado maior que o ataque exibido no cajado.

Magias sem contexto de cajado mantêm seus valores anteriores. A restrição de atributos do cajado não foi alterada nesta etapa.

## Arquivos

- CajadoMagico.cs: captura DanoBase após validar a direção do lançamento.
- BolaDeFogo.cs: contexto imutável por lançamento e configuração inicial do bônus/elemento.
- EfeitoFogoArea.cs: recebe o contexto antes do primeiro pulso.
- Tests/CajadoAtaqueMagico.Tests.ps1: 13 verificações com métodos reais em ambiente Unity simulado, além de verificações da conexão entre os componentes.

## Validação

Compilação de Assembly-CSharp com as referências do projeto: sem erros. Testes de ataque mágico (13), comandos de voz (18) e alvos de combate (9) aprovados. Isso não substitui testes em Play e no Quest.

No Unity, testar um cajado com ataque fixo 7 e bônus da magia zero; conferir 7 por pulso no alvo. Depois configurar bônus 3 e conferir 10. Lançar e soltar o cajado antes do impacto deve conservar o valor. Conferir também o lançamento por voz, o consumo de mana e o funcionamento de magias antigas sem CajadoPopup.

Este relatório atualiza a limitação registrada nos relatórios anteriores: o ataque do cajado agora está conectado à magia.
