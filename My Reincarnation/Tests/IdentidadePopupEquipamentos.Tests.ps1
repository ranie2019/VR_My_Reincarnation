$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$code=@'
using System;using System.Collections.Generic;
public enum ClasseManopla { Comum, Divino }
public enum AfinidadeDivinaManopla { Nenhuma,Luz,Trevas }
public class TMP_Text {public string text;}
public class GameObject {public string name;}
public class ItemPersistente {public GameObject PrefabReferencia;}
public class ValorAtributo {public int valor;}
public class EstadoGerado {
 public string nome="Nome salvo antigo",descricao="Descricao salva antiga";
 public ClasseManopla classe;public AfinidadeDivinaManopla afinidade;
 public List<ValorAtributo> atributos=new List<ValorAtributo>{new ValorAtributo{valor=7}};
 public string Dump()=>nome+"|"+descricao+"|"+classe+"|"+afinidade+"|"+atributos[0].valor;
}
public static class JsonUtility {public static string ToJson(object estado)=>((EstadoGerado)estado).Dump();}
'@
$index=0
foreach($script in @('AtributosManoplas','AtributosPeitoral','AtributosElmo')) {
 $source=Get-Content -Raw (Join-Path $project "Assets/Script/Equipamento/$script.cs")
 $a=$source.IndexOf('    private string ObterNomePrefab()')
 $b=$source.IndexOf('    public int ObterValor(', $a)
 $nameMethod=$source.Substring($a,$b-$a)
 $a=$source.IndexOf('    public void AtualizarPopup()')
 $b=$source.IndexOf('    private void AtualizarLinhasAtributos(', $a)
 $popupMethods=$source.Substring($a,$b-$a)
 $a=$source.IndexOf('    public string ExportarEstadoJson()')
 $b=$source.IndexOf('    public void RestaurarEstadoJson(', $a)
 $export=$source.Substring($a,$b-$a)
 $hostName="IdentityHost$index"
 $identityFixture=@'
public class HOST {
 private string nomeExibicao,descricao="New description";
 private GameObject prefabObjeto=new GameObject{name="New Helmet"},gameObject=new GameObject{name="Instance(Clone)"};
 private ItemPersistente persistente;
 private TMP_Text textoNome=new TMP_Text(),textoDescricao=new TMP_Text(),textoClasse=new TMP_Text();
 private EstadoGerado resultado=new EstadoGerado();private bool Gerado=>resultado!=null;
 private T GetComponent<T>() where T:class=>persistente as T;
 private void BuscarPopup(){}private void AtualizarDurabilidadeVisual(){}
 private void AtualizarLinhasAtributos(IReadOnlyList<ValorAtributo> a){}
'@
 $tests=@'
 public static int Run(){int checks=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);checks++;};
  var h=new HOST();string saved=h.ExportarEstadoJson();var original=h.resultado;
  h.AtualizarPopup();c(h.textoNome.text=="New Helmet"&&h.textoDescricao.text=="New description","Popup must ignore cached name/description");
  h.prefabObjeto.name="Renamed Helmet";h.descricao="English description";h.AtualizarIdentidadePopup();
  c(h.textoNome.text=="Renamed Helmet"&&h.textoDescricao.text=="English description","Live metadata changes must refresh while open");
  h.nomeExibicao="  Localized display name  ";h.AtualizarIdentidadePopup();c(h.textoNome.text=="Localized display name","Explicit display name override");
  h.descricao="";h.AtualizarIdentidadePopup();c(h.textoDescricao.text=="","Empty description must not restore old save text");
  c(h.ExportarEstadoJson()==saved&&object.ReferenceEquals(original,h.resultado)&&h.resultado.atributos[0].valor==7,"UI refresh must not mutate roll or saved payload");
  h.nomeExibicao=" ";h.prefabObjeto=null;h.persistente=new ItemPersistente{PrefabReferencia=new GameObject{name="Current prefab"}};
  h.AtualizarIdentidadePopup();c(h.textoNome.text=="Current prefab","Fallback uses current persistent prefab reference");
  h.persistente=null;h.AtualizarIdentidadePopup();c(h.textoNome.text=="Instance","Fallback removes clone suffix");
  return checks;
 }
}
'@
 $code+=($identityFixture+$nameMethod+$popupMethods+$export+$tests).Replace('HOST',$hostName)
 $index++
}
Add-Type -TypeDefinition $code
$checks=[IdentityHost0]::Run()+[IdentityHost1]::Run()+[IdentityHost2]::Run()
Write-Output "PASS: $checks verificacoes dos metodos reais de identidade dos tres equipamentos, host simulado."
