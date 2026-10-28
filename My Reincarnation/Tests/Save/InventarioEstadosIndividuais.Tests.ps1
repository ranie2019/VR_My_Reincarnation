param([string]$LegacySavePath)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Get-Content -Raw (Join-Path $project 'Assets/Script/Save/InventorySaveData.cs')
$item = Get-Content -Raw (Join-Path $project 'Assets/Script/Save/ItemPersistente.cs')
$safeFile = Get-Content -Raw (Join-Path $project 'Assets/Script/Save/ArquivoSaveSeguro.cs')
$adapter = Get-Content -Raw (Join-Path $project 'Assets/Script/Save/InventarioSaveAdapter.cs')
$start = $adapter.IndexOf('    private void AplicarInstanciaIdsDaPilha(')
$end = $adapter.IndexOf('    private void MarcarOrigemRuntimeNoSave(', $start)
$capture = $adapter.Substring($start, $end-$start).Replace('private void AplicarInstanciaIdsDaPilha','public void Capture')
# Actual production DTO, ItemPersistente and capture method; fake Unity host.
# Does not execute Unity lifecycle, physics, sockets or Unity JsonUtility.
$stub = @'
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute {}
 [AttributeUsage(AttributeTargets.Field)] public class Header : Attribute { public Header(string x) {} }
 [AttributeUsage(AttributeTargets.Field)] public class Tooltip : Attribute { public Tooltip(string x) {} }
 [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute {}
 [AttributeUsage(AttributeTargets.Field)] public class HideInInspector : Attribute {}
 [AttributeUsage(AttributeTargets.Method)] public class ContextMenu : Attribute { public ContextMenu(string x) {} }
 public class Component { public GameObject gameObject; public T GetComponent<T>() where T:Component { return gameObject.GetComponent<T>(); } }
 public class MonoBehaviour:Component {}
 public class GameObject {
  public string name="Test"; public List<Component> components=new List<Component>();
  public T Add<T>() where T:Component,new() { var x=new T(); x.gameObject=this; components.Add(x); return x; }
  public T GetComponent<T>() where T:Component { return components.OfType<T>().FirstOrDefault(); }
  public Component GetComponent(string name) { return components.FirstOrDefault(x=>x.GetType().Name==name); }
 }
 public static class Application { public static bool isPlaying=true; }
 public static class Mathf { public static int Max(int a,int b)=>Math.Max(a,b); public static int RoundToInt(float x)=>(int)Math.Round(x); }
 public struct Hash128 { public static Hash128 Compute(string x)=>new Hash128(); public override string ToString()=>"test-type"; }
 public static class JsonUtility { public static string ToJson<T>(T x)=>System.Text.Json.JsonSerializer.Serialize(x, Audit05.Options); public static T FromJson<T>(string x)=>System.Text.Json.JsonSerializer.Deserialize<T>(x,Audit05.Options); }
}
public class EstadoItemInventario:UnityEngine.Component { public bool estaNoInventario; }
public class Respawnitem:UnityEngine.Component { public bool EstaDesarmadoComoItemColetado()=>false; public void DesarmarComoItemColetado() {} }
public class XRGrabInteractable:UnityEngine.Component {}
public class Espada:UnityEngine.Component { public int vidaAtual=100, vidaMaxima=100; public bool quebrada; public int VidaAtual=>vidaAtual; public int VidaMaxima=>vidaMaxima; public void AtualizarDurabilidadeVisual() {} }
public class Equipamento:Espada {}
public class Machado:Espada {}
public class Picareta:Espada {}
public class Escudo:Espada {}
public class Arco:UnityEngine.Component { public float durabilidadeAtual=100, durabilidadeMaxima=100; public bool arcoQuebrado; public float DurabilidadeAtual=>durabilidadeAtual; public float DurabilidadeMaxima=>durabilidadeMaxima; public void AtualizarDurabilidadeVisual() {} }
public class CaptureAdapter {
 private ItemPersistente GarantirItemPersistenteParaSave(XRGrabInteractable x)=>x.GetComponent<ItemPersistente>();
 private void AdicionarInstanciaIdUnico(List<string> ids,string id) { if(!ids.Contains(id))ids.Add(id); }
'@
$tests = @'
public static class Audit05 {
 public static readonly System.Text.Json.JsonSerializerOptions Options=new System.Text.Json.JsonSerializerOptions { IncludeFields=true };
 static int assertions;
 static void Check(bool x,string message) { assertions++; if(!x)throw new Exception(message); }
 static void Fail(Action a) { bool failed=false; try{a();}catch(Exception){failed=true;} Check(failed,"Expected rejection"); }
 static T Copy<T>(T x)=>System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(x,Options),Options);
 static void Field(object x,string name,object value)=>x.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(x,value);
 static XRGrabInteractable New(string id,int value,bool durable=true) {
  var go=new UnityEngine.GameObject(); var grab=go.Add<XRGrabInteractable>(); var item=go.Add<ItemPersistente>();
  Field(item,"tipoItemId","type-1"); Field(item,"dadosExtrasJson","custom:"+id); item.DefinirInstanciaId(id);
  if(durable){var sword=go.Add<Espada>();sword.vidaAtual=value;}
  return grab;
 }
 static InventorySaveData Save(List<XRGrabInteractable> stack) {
  var d=stack.Last().GetComponent<ItemPersistente>().CriarSaveData(0,stack.Count,false);
  new CaptureAdapter().Capture(d,stack);d.ValidarEstadosIndividuais();return Copy(d);
 }
 static List<XRGrabInteractable> Load(InventorySaveData d) {
  d.ValidarEstadosIndividuais();var stack=new List<XRGrabInteractable>();
  foreach(var id in d.instanciaIds) {
   var unit=d.CriarDataParaInstancia(id);var grab=New(id,100,unit.durabilidade>=0);
   if(d.versaoEstadosIndividuais==1)ItemPersistente.ValidarEstadoIndividualNoPrefab(grab.gameObject,unit);
   grab.GetComponent<ItemPersistente>().AplicarSaveData(unit);stack.Add(grab);
  }return stack;
 }
 public static string Run(string legacy) {
  var ids=new[]{Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N")};
  var stack=new List<XRGrabInteractable>{New(ids[0],100),New(ids[1],35),New(ids[2],70)};
  InventorySaveData d=null;
  for(int cycle=0;cycle<3;cycle++){
   d=Save(stack); d.estadosIndividuais.Reverse(); // Payload order must not change identity.
   stack=Load(Copy(d));
   for(int i=0;i<3;i++){
    Check(stack[i].GetComponent<ItemPersistente>().ObterInstanciaId()==ids[i],"Identity/order");
    Check(stack[i].GetComponent<ItemPersistente>().LerDurabilidade()==new[]{100,35,70}[i],"Durability drift");
    Check(stack[i].GetComponent<Espada>().vidaMaxima==100,"Maximum lost");
    Check(stack[i].GetComponent<ItemPersistente>().CriarSaveData(0,1,false).dadosExtrasJson=="custom:"+ids[i],"Personal state lost");
   }
  }
  for(int i=2;i>=0;i--){Check(stack.Last().GetComponent<ItemPersistente>().ObterInstanciaId()==ids[i],"LIFO");stack.RemoveAt(stack.Count-1);}
  stack=Load(d);var used=stack[1];stack.RemoveAt(1);used.GetComponent<Espada>().vidaAtual=12;stack.Add(used);
  stack=Load(Save(stack));Check(stack.Last().GetComponent<ItemPersistente>().LerDurabilidade()==12,"Reinsert updated value");Check(stack[0].GetComponent<ItemPersistente>().LerDurabilidade()==100&&stack[1].GetComponent<ItemPersistente>().LerDurabilidade()==70,"Changed unrelated units");
  var old=new InventorySaveData{itemId="type-1",instanciaId=ids[0],instanciaIds=ids.ToList(),quantidade=3,durabilidade=35};
  stack=Load(Copy(old));Check(stack.All(x=>x.GetComponent<ItemPersistente>().LerDurabilidade()==35),"Legacy representative");
  var migrated=Save(stack);Check(migrated.versaoEstadosIndividuais==1&&migrated.estadosIndividuais.Count==3,"Migration");Check(migrated.instanciaIds.SequenceEqual(ids),"Legacy IDs");
  var resources=Enumerable.Range(0,99).Select(i=>New(Guid.NewGuid().ToString("N"),0,false)).ToList();
  var full=Save(resources);Check(Load(full).Count==99,"99 units");Check(full.estadosIndividuais.All(x=>x.durabilidade==-1&&!x.possuiDurabilidadeMaxima),"Artificial durability");
  var invalid=Copy(d);invalid.estadosIndividuais.RemoveAt(0);Fail(()=>invalid.ValidarEstadosIndividuais());
  invalid=Copy(d);invalid.estadosIndividuais[0].instanciaId=invalid.estadosIndividuais[1].instanciaId;Fail(()=>invalid.ValidarEstadosIndividuais());
  invalid=Copy(d);invalid.estadosIndividuais[0].durabilidade=float.NaN;Fail(()=>invalid.ValidarEstadosIndividuais());
  invalid=Copy(d);invalid.estadosIndividuais[0].durabilidade=101;Fail(()=>invalid.ValidarEstadosIndividuais());
  invalid=Copy(d);invalid.estadosIndividuais[0].itemId="";Fail(()=>invalid.ValidarEstadosIndividuais());
  invalid=Copy(d);invalid.estadosIndividuais.Clear();Fail(()=>invalid.ValidarEstadosIndividuais());
  invalid=Copy(d);invalid.versaoEstadosIndividuais=2;Fail(()=>invalid.ValidarEstadosIndividuais());
  invalid=Copy(full);invalid.quantidade=100;Fail(()=>invalid.ValidarEstadosIndividuais());
  Fail(()=>d.CriarDataParaInstancia(Guid.NewGuid().ToString("N")));
  var variant=Copy(d);variant.estadosIndividuais[0].itemId="variant-type";variant.ValidarEstadosIndividuais();
  Check(variant.CriarDataParaInstancia(variant.estadosIndividuais[0].instanciaId).itemId=="variant-type","Per-instance prefab");
  var unit=d.CriarDataParaInstancia(ids[1]);unit.durabilidade=35.5f;Fail(()=>ItemPersistente.ValidarEstadoIndividualNoPrefab(New(ids[1],100).gameObject,unit));
  foreach(var type in new[]{typeof(Espada),typeof(Equipamento),typeof(Machado),typeof(Picareta),typeof(Escudo),typeof(Arco)}){
   var go=new UnityEngine.GameObject();var comp=(UnityEngine.Component)Activator.CreateInstance(type);comp.gameObject=go;go.components.Add(comp);
   var persistent=go.Add<ItemPersistente>();Field(persistent,"tipoItemId","type-1");persistent.DefinirInstanciaId(ids[0]);
   var state=d.CriarDataParaInstancia(ids[0]);state.durabilidade=0;state.durabilidadeMaxima=120;
   ItemPersistente.ValidarEstadoIndividualNoPrefab(go,state);persistent.AplicarSaveData(state);
   var saved=persistent.CriarSaveData(0,1,false);Check(saved.durabilidade==0&&saved.durabilidadeMaxima==120,type.Name+" zero/max");
   var broken=type.GetField("quebrada")??type.GetField("arcoQuebrado");Check((bool)broken.GetValue(comp),type.Name+" broken flag");
   state.durabilidade=35;persistent.AplicarSaveData(state);Check(persistent.LerDurabilidade()==35,type.Name+" current");
   Check(!(bool)broken.GetValue(comp),type.Name+" restored flag");
  }
  var folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"AUD05File-"+Guid.NewGuid().ToString("N"));
  System.IO.Directory.CreateDirectory(folder);var path=System.IO.Path.Combine(folder,"disposable.json");
  Func<string,bool> validator=json=>{try{System.Text.Json.JsonSerializer.Deserialize<InventorySaveData>(json,Options).ValidarEstadosIndividuais();return true;}catch{return false;}};
  var validJson=System.Text.Json.JsonSerializer.Serialize(d,Options);ArquivoSaveSeguro.Gravar(path,validJson,validator);
  invalid=Copy(d);invalid.estadosIndividuais.RemoveAt(0);
  Fail(()=>ArquivoSaveSeguro.Gravar(path,System.Text.Json.JsonSerializer.Serialize(invalid,Options),validator));
  Check(System.IO.File.ReadAllText(path)==validJson,"Invalid individual state overwrote valid file");
  ArquivoSaveSeguro.Gravar(path,System.Text.Json.JsonSerializer.Serialize(full,Options),validator);
  Check(System.IO.File.ReadAllText(path+".bak")==validJson,"Backup lost");
  if(!string.IsNullOrWhiteSpace(legacy)){
   using(var doc=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(legacy)))
    foreach(var row in doc.RootElement.GetProperty("inventario").EnumerateArray()){
     var entry=System.Text.Json.JsonSerializer.Deserialize<InventorySaveData>(row.GetRawText(),Options);
     entry.ValidarEstadosIndividuais();
     foreach(var id in entry.instanciaIds){var state=entry.CriarDataParaInstancia(id);Check(state.instanciaId==id&&state.durabilidade==entry.durabilidade&&state.itemId==entry.itemId,"Real legacy fixture");}
    }
  }
  return assertions+" verificacoes aprovadas. Host simulado; Unity/Quest nao executados.";
 }
}
'@
$source = $source -replace '(?m)^using .*;\s*',''
$item = $item -replace '(?m)^using .*;\s*',''
$safeFile = $safeFile -replace '(?m)^using .*;\s*',''
$code = "using System; using System.IO; using System.Text; using System.Linq; using System.Collections.Generic; using System.Reflection; using UnityEngine;`n" + $stub + $capture + "}`n" + $source + $item + $safeFile + $tests
Add-Type -TypeDefinition $code -IgnoreWarnings -WarningAction SilentlyContinue
$fixture = $null
if ($LegacySavePath) {
 $folder = Join-Path ([IO.Path]::GetTempPath()) ('AUD05-' + [Guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($folder) | Out-Null
 $fixture = Join-Path $folder 'legacy-copy.json'
 Copy-Item -LiteralPath $LegacySavePath -Destination $fixture
}
[Audit05]::Run($fixture)

