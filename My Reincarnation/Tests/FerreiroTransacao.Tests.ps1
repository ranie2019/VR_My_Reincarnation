$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/NPC/Ferreiro/CriarItem.cs')
$a=$source.IndexOf('    private bool PossuiRecursosSuficientes(ConfiguracaoBotao')
$b=$source.IndexOf('    private static void AtualizarTextoQuantidade', $a)
$validation=$source.Substring($a,$b-$a)
$a=$source.IndexOf('    private bool consumindoRecursos;')
$b=$source.IndexOf('    /// <summary>', $a)
$transaction=$source.Substring($a,$b-$a)
$stub=@'
using System;using System.Collections.Generic;using System.Linq;
public class GameObject {
 public List<object> components=new List<object>();public bool active=true,destroyed;
 public void SetActive(bool a){active=a;}public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();
}
public class XRGrabInteractable {public int id;public GameObject gameObject=new GameObject();public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();}
public class AtributosElmo {public bool valido=true;public bool GarantirGeracao()=>valido;}
public class AtributosPeitoral {public bool valido=true;public bool GarantirGeracao()=>valido;}
public class AtributosManoplas {public bool valido=true;public bool GarantirGeracao()=>valido;}
public class ItemPersistente {public int consumed;public void RegistrarConsumoPermanente(){consumed++;}}
public class RecuperacaoItemCarga {GameObject go;bool active;public RecuperacaoItemCarga(XRGrabInteractable item){go=item.gameObject;active=go.active;}public void Restaurar(){go.active=active;}}
public class SlotInventario {
 public string name="Wood";public List<XRGrabInteractable> items=new List<XRGrabInteractable>();public int calls,failAt=-1;
 public int ObterQuantidadeAtual()=>items.Count;public List<XRGrabInteractable> ObterItensParaSave()=>items.ToList();
 public Action CapturarRecuperacaoCarga(){var before=items.ToList();return ()=>items=before.ToList();}
 public bool ReservarUnidadeParaConsumo(out XRGrabInteractable item){item=null;if(calls++==failAt||items.Count==0)return false;item=items.Last();items.RemoveAt(items.Count-1);item.gameObject.SetActive(false);return true;}
}
public class SaveManager {public static SaveManager Instancia=new SaveManager();public int locks;public bool blocked;public void IniciarOperacaoCritica(){locks++;}public void FinalizarOperacaoCritica(){locks--;}public void NotificarFalhaRestauracao(string m){blocked=true;}}
public static class Debug {public static void LogError(object a,object b){}}
public static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);}
public class Button {}
public class CraftHost {
 public class CustoRecurso {public string nomeRecurso;public int quantidade;}
 public class ConfiguracaoBotao {public List<CustoRecurso> custos=new List<CustoRecurso>();}
 public ConfiguracaoBotao config=new ConfiguracaoBotao();public SlotInventario[] slots;
 private ConfiguracaoBotao EncontrarConfiguracao(Button b)=>config;
 private SlotInventario[] ObterSlotsDoJogador()=>slots;
 private int ContarRecurso(string name)=>slots.Where(s=>string.Equals(s.name,name.Trim(),StringComparison.OrdinalIgnoreCase)).Sum(s=>s.items.Count);
 private static void AtualizarTextoQuantidade(CustoRecurso c,int value){}
 private static bool CorrespondeAoRecurso(SlotInventario s,string name)=>string.Equals(s.name,name.Trim(),StringComparison.OrdinalIgnoreCase);
 private void Destroy(GameObject go){go.destroyed=true;}
'@
$tests=@'
}
public static class CraftTests {
 static int checks;static void Check(bool ok,string m){checks++;if(!ok)throw new Exception(m);}
 static CraftHost New(int count){var h=new CraftHost{slots=new[]{new SlotInventario()}};for(int i=0;i<count;i++){var x=new XRGrabInteractable{id=i};x.gameObject.components.Add(new ItemPersistente());h.slots[0].items.Add(x);}h.config.custos.Add(new CraftHost.CustoRecurso{nomeRecurso="Wood",quantidade=2});SaveManager.Instancia=new SaveManager();return h;}
 public static int Run(){
  var h=New(3);h.config.custos.Add(new CraftHost.CustoRecurso{nomeRecurso=" wood ",quantidade=2});bool spawned=false;
  Check(!h.TentarCriarComRecursos(new Button(),()=>{spawned=true;return new GameObject();}),"Custos repetidos devem ser somados");Check(!spawned&&h.slots[0].items.Count==3,"Insuficiencia nao pode consumir");
  h=New(4);var originals=h.slots[0].items.ToArray();h.slots[0].failAt=1;
  Check(!h.TentarCriarComRecursos(new Button(),()=>new GameObject()),"Falha parcial deve cancelar");Check(h.slots[0].items.SequenceEqual(originals)&&originals.All(x=>x.gameObject.active&&!x.gameObject.destroyed),"Rollback deve manter mesmas instancias e LIFO");
  h=New(4);originals=h.slots[0].items.ToArray();Check(!h.TentarCriarComRecursos(new Button(),()=>{throw new Exception("spawn");}),"Falha de spawn deve cancelar");Check(h.slots[0].items.SequenceEqual(originals)&&originals.All(x=>!x.gameObject.destroyed),"Falha de spawn deve devolver materiais");
  Check(!h.TentarCriarComRecursos(new Button(),()=>null),"Spawn nulo nao confirma");Check(h.slots[0].items.SequenceEqual(originals),"Nulo deve devolver unidades");
  var bad=new GameObject();bad.components.Add(new AtributosManoplas{valido=false});Check(!h.TentarCriarComRecursos(new Button(),()=>bad),"Item sem atributos validos nao confirma");Check(bad.destroyed&&h.slots[0].items.SequenceEqual(originals),"Item invalido descartado e materiais preservados");
  var host=h;Check(h.TentarCriarComRecursos(new Button(),()=>{Check(SaveManager.Instancia.locks==1,"Save deve estar bloqueado durante criacao");Check(!host.TentarCriarComRecursos(new Button(),()=>new GameObject()),"Reentrada deve ser rejeitada");return new GameObject();}),"Sucesso deve confirmar");
  Check(h.slots[0].items.Count==2&&originals.Count(x=>x.gameObject.destroyed)==2,"Destruir exatamente duas unidades");Check(originals[2].GetComponent<ItemPersistente>().consumed==1&&originals[3].GetComponent<ItemPersistente>().consumed==1,"Registrar consumo permanente");Check(SaveManager.Instancia.locks==0&&!SaveManager.Instancia.blocked,"Liberar bloqueio depois de sucesso");
  h=New(2);h.config.custos[0].quantidade=0;Check(!h.ConsumirRecursos(new Button()),"Custo invalido nao pode criar item gratis");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($stub+$validation+$transaction+$tests)
Write-Output ("PASS: "+[CraftTests]::Run()+" verificacoes do consumo real em host simulado.")
