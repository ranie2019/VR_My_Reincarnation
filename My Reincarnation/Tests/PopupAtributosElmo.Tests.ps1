$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/EquiparElmo.cs')
$a = $source.IndexOf('    private const int LimiteLinhasPopup')
$b = $source.IndexOf('    [SerializeField, HideInInspector] private EstadoGerado', $a)
$setup = $source.Substring($a, $b - $a)
$a = $source.IndexOf('    private void AtualizarLinhasAtributos(')
$b = $source.IndexOf('    private EquiparElmo ObterElmoComparacao(', $a)
$render = $source.Substring($a, $b - $a)
$a = $source.IndexOf('    private static void AtualizarComparacaoLinha(')
$b = $source.IndexOf('    public string ExportarEstadoJson()', $a)
$render += $source.Substring($a, $b - $a)
$code = @'
using System;
using System.Collections.Generic;
using System.Globalization;
public enum TipoAtributoManopla { Forca, Constituicao, Agilidade, Resistencia, Sorte, Mana, HP }
public class TMP_Text { public string text="antigo"; }
public static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); public static float Clamp(float v,float a,float b)=>Math.Min(b,Math.Max(a,v)); }
public class GameObject { public bool activeSelf=true; public void SetActive(bool value){activeSelf=value;} }
public class EquiparElmo {
 public class Estado { public List<PopupElmoTests.ValorAtributo> atributos=new List<PopupElmoTests.ValorAtributo>(); }
 public Estado resultado;
 public Dictionary<TipoAtributoManopla,int> valores=new Dictionary<TipoAtributoManopla,int>();
 public int ObterValor(TipoAtributoManopla tipo)=>valores.ContainsKey(tipo)?valores[tipo]:0;
}
public class PopupElmoTests {
 public class TextoAtributo {
 public TMP_Text textoNome, textoValor, textoDiferenca;
 public GameObject setaUp, setaDown, sinalPositivo, sinalNegativo;
}
 public class ValorAtributo { public TipoAtributoManopla atributo; public int valor; }
 private float vidaMaxima=100,vidaAtual=100,desgasteNaturalPorMinuto=.5f;
 private List<TextoAtributo> textosValores;
 private EquiparElmo equipada;
 private bool disponivel=true;
 private EquiparElmo ObterElmoComparacao(out bool ok){ok=disponivel;return equipada;}
'@ + $setup + $render + @'
 public static int Run() {
  int n=0;Action<bool,string> check=(ok,msg)=>{if(!ok)throw new Exception(msg);n++;};
  var host=new PopupElmoTests();
  host.NormalizarLinhasPopup();check(host.textosValores.Count==5,"Cinco linhas por padrao");
  foreach(var row in host.textosValores){row.textoNome=new TMP_Text();row.textoValor=new TMP_Text();}
  var first=host.textosValores[0];
  host.AtualizarLinhasAtributos(new List<ValorAtributo>{new ValorAtributo{atributo=TipoAtributoManopla.Sorte,valor=7}});
  check(first.textoNome.text=="Sorte"&&first.textoValor.text=="+7","Um atributo deve ocupar o topo independentemente do tipo");
  for(int i=1;i<5;i++)check(host.textosValores[i].textoNome.text==""&&host.textosValores[i].textoValor.text=="","Linha nao usada deve ser limpa");
  var values=new List<ValorAtributo>();
  for(int i=0;i<6;i++)values.Add(new ValorAtributo{atributo=(TipoAtributoManopla)i,valor=i+1});
  host.AtualizarLinhasAtributos(values);
  for(int i=0;i<5;i++)check(host.textosValores[i].textoNome.text==((TipoAtributoManopla)i).ToString()&&host.textosValores[i].textoValor.text=="+"+(i+1),"Preenchimento deve seguir a ordem do sorteio");
  check(values.Count==6,"Limite visual nao deve alterar valores salvos ou bonus");
  host.AtualizarLinhasAtributos(new List<ValorAtributo>());
  foreach(var row in host.textosValores)check(row.textoNome.text==""&&row.textoValor.text=="","Nenhum atributo deve limpar textos anteriores");
  host.textosValores.Add(new TextoAtributo());host.ValidarAtributosEDurabilidade();
  check(host.textosValores.Count==5&&host.textosValores[0]==first,"Normalizar deve preservar as primeiras referencias");
  host.textosValores[0]=null;host.AtualizarLinhasAtributos(values);
  check(host.textosValores[0]!=null,"Campos ausentes nao devem causar excecao");
  host=new PopupElmoTests();host.NormalizarLinhasPopup();
  foreach(var row in host.textosValores){
   row.textoDiferenca=new TMP_Text();row.setaUp=new GameObject();row.setaDown=new GameObject();
   row.sinalPositivo=new GameObject();row.sinalNegativo=new GameObject();
  }
  first=host.textosValores[0];
  host.equipada=new EquiparElmo();host.equipada.valores[TipoAtributoManopla.Forca]=5;
  values=new List<ValorAtributo>{new ValorAtributo{atributo=TipoAtributoManopla.Forca,valor=7}};
  host.AtualizarLinhasAtributos(values);
  check(first.textoDiferenca.text=="2"&&first.setaUp.activeSelf&&first.sinalPositivo.activeSelf&&!first.setaDown.activeSelf&&!first.sinalNegativo.activeSelf,"7 contra 5 deve mostrar +2 e somente up");
  values[0].valor=3;host.AtualizarLinhasAtributos(values);
  check(first.textoDiferenca.text=="2"&&!first.setaUp.activeSelf&&!first.sinalPositivo.activeSelf&&first.setaDown.activeSelf&&first.sinalNegativo.activeSelf,"3 contra 5 deve mostrar -2 e somente down");
  values[0].valor=5;host.AtualizarLinhasAtributos(values);
  check(first.textoDiferenca.text=="0"&&!first.setaUp.activeSelf&&!first.setaDown.activeSelf&&!first.sinalPositivo.activeSelf&&!first.sinalNegativo.activeSelf,"Igual deve ocultar sinais e setas");
  values[0].atributo=TipoAtributoManopla.Sorte;values[0].valor=4;host.AtualizarLinhasAtributos(values);
  check(first.textoDiferenca.text=="4"&&first.setaUp.activeSelf,"Atributo ausente no equipado vale zero");
  host.equipada=null;host.AtualizarLinhasAtributos(values);
  check(first.textoDiferenca.text=="4"&&first.sinalPositivo.activeSelf,"Mao vazia compara com zero");
  host.disponivel=false;host.AtualizarLinhasAtributos(values);
  check(first.textoDiferenca.text==""&&!first.setaUp.activeSelf&&!first.setaDown.activeSelf,"Comparacao indisponivel nao inventa diferenca");
  host.disponivel=true;host.AtualizarLinhasAtributos(new List<ValorAtributo>());
  foreach(var row in host.textosValores)check(row.textoDiferenca.text==""&&!row.setaUp.activeSelf&&!row.setaDown.activeSelf&&!row.sinalPositivo.activeSelf&&!row.sinalNegativo.activeSelf,"Linha vazia deve limpar todos os indicadores");
  host.equipada=new EquiparElmo();host.equipada.valores[TipoAtributoManopla.Forca]=int.MaxValue;
  values[0].atributo=TipoAtributoManopla.Forca;values[0].valor=0;host.AtualizarLinhasAtributos(values);
  check(first.textoDiferenca.text==int.MaxValue.ToString()&&first.setaDown.activeSelf,"Limite inteiro deve preservar diferenca negativa");
  host.equipada.resultado=new EquiparElmo.Estado();
  host.equipada.resultado.atributos.Add(new ValorAtributo{atributo=TipoAtributoManopla.Resistencia,valor=4});
  host.equipada.valores[TipoAtributoManopla.Resistencia]=4;
  values[0].valor=7;host.AtualizarLinhasAtributos(values);
  var loss=host.textosValores[1];
  loss.textoNome=new TMP_Text();loss.textoValor=new TMP_Text();host.AtualizarLinhasAtributos(values);
  check(loss.textoNome.text=="Resistencia"&&loss.textoValor.text=="+0"&&loss.textoDiferenca.text=="4"&&loss.setaDown.activeSelf&&loss.sinalNegativo.activeSelf,"Atributo perdido deve mostrar -4 sem modificar a candidata");
  check(values.Count==1,"Uniao de comparacao nao deve modificar atributos reais");
  values[0].atributo=TipoAtributoManopla.Mana;values[0].valor=4;host.AtualizarLinhasAtributos(values);
  first.textoNome=new TMP_Text();first.textoValor=new TMP_Text();host.AtualizarLinhasAtributos(values);
  check(first.textoNome.text=="MP"&&first.textoValor.text=="+4","Mana deve aparecer como MP");
  values[0].atributo=TipoAtributoManopla.HP;values[0].valor=20;host.AtualizarLinhasAtributos(values);
  check(first.textoNome.text=="HP"&&first.textoValor.text=="+20","HP deve aparecer no popup");
  return n;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: " + [PopupElmoTests]::Run() + " verificacoes das linhas reais em host simulado.")
