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
$shield = Get-Content -Raw (Join-Path $project 'Assets/Script/Armas/Escudo.cs')
$start = $shield.IndexOf('    internal void PrepararSorteioParaSalvar()')
$end = $shield.IndexOf('    internal static EstadoSorteio LerSorteioJson(', $start)
$shieldSave = $shield.Substring($start, $end-$start)
$start = $shield.IndexOf('    internal void RestaurarSorteio(')
$end = $shield.IndexOf('    internal Action CapturarSorteioParaCarga()', $start)
$shieldSave += $shield.Substring($start, $end-$start)
# Actual production DTO, ItemPersistente and capture method; fake Unity host.
# Does not execute Unity lifecycle, physics, sockets or Unity JsonUtility.
$stub = @'
// Component absent in these legacy fixtures; generation/selection has its own tests.
public class AtributosManoplas:UnityEngine.MonoBehaviour { public float vidaMaxima=100,vidaAtual=100; public float VidaMaxima=>vidaMaxima; public void AtualizarDurabilidadeVisual(){}  public bool Gerado; public string estado; public string ExportarEstadoJson()=>estado; public void RestaurarEstadoJson(string json){estado=json;Gerado=true;} public void PrepararMigracaoSaveLegado(){Gerado=false;} public static object LerEValidarEstadoJson(string json){if(json!=null&&json.StartsWith("roll:"))return null;throw new System.IO.InvalidDataException("Bad roll");} }
public class AtributosPeitoral:UnityEngine.MonoBehaviour { public float vidaMaxima=100,vidaAtual=100; public float VidaMaxima=>vidaMaxima; public void AtualizarDurabilidadeVisual(){}  public bool Gerado; public string estado; public string ExportarEstadoJson()=>estado; public void RestaurarEstadoJson(string json){estado=json;Gerado=true;} public void PrepararMigracaoSaveLegado(){Gerado=false;} public static object LerEValidarEstadoJson(string json){if(json!=null&&json.StartsWith("roll:"))return null;throw new System.IO.InvalidDataException("Bad roll");} }
public class EquiparElmo:UnityEngine.MonoBehaviour { public float vidaMaxima=100,vidaAtual=100; public float VidaMaxima=>vidaMaxima; public void AtualizarDurabilidadeVisual(){}  public bool Gerado; public string estado; public string ExportarEstadoJson()=>estado; public void RestaurarEstadoJson(string json){estado=json;Gerado=true;} public void PrepararMigracaoSaveLegado(){Gerado=false;} public static object LerEValidarEstadoJson(string json){if(json!=null&&json.StartsWith("roll:"))return null;throw new System.IO.InvalidDataException("Bad roll");} }
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute {}
 [AttributeUsage(AttributeTargets.Field)] public class Header : Attribute { public Header(string x) {} }
 [AttributeUsage(AttributeTargets.Field)] public class Tooltip : Attribute { public Tooltip(string x) {} }
 [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute {}
 [AttributeUsage(AttributeTargets.Field)] public class HideInInspector : Attribute {}
 [AttributeUsage(AttributeTargets.Method)] public class ContextMenu : Attribute { public ContextMenu(string x) {} }
 public class Component { public GameObject gameObject; public Transform transform=>gameObject.transform; public T GetComponent<T>() where T:Component { return gameObject.GetComponent<T>(); } public T GetComponentInChildren<T>(bool inactive=false) where T:Component=>GetComponent<T>(); }
 public class MonoBehaviour:Component {}
 public class GameObject {
  public string name="Test"; public Transform transform=new Transform(); public List<Component> components=new List<Component>();
  public T Add<T>() where T:Component,new() { var x=new T(); x.gameObject=this; components.Add(x); return x; }
  public T GetComponent<T>() where T:Component { return components.OfType<T>().FirstOrDefault(); }
  public T GetComponentInChildren<T>(bool inactive=false) where T:Component=>GetComponent<T>();
  public Component GetComponent(string name) { return components.FirstOrDefault(x=>x.GetType().Name==name); }
 }
 public struct Vector3 {public float x,y,z;public static Vector3 operator -(Vector3 a,Vector3 b)=>default;public float sqrMagnitude=>0;}
 public struct Quaternion {public static float Angle(Quaternion a,Quaternion b)=>0;}
 public class Transform {public Vector3 position;public Quaternion rotation;}
 public static class Time {public static float unscaledTime;}
 public static class Application { public static bool isPlaying=true; }
 public static class Mathf { public static int Max(int a,int b)=>Math.Max(a,b); public static int RoundToInt(float x)=>(int)Math.Round(x); }
 public struct Hash128 { public static Hash128 Compute(string x)=>new Hash128(); public override string ToString()=>"test-type"; }
 public static class JsonUtility { public static string ToJson<T>(T x)=>System.Text.Json.JsonSerializer.Serialize(x, Audit05.Options); public static T FromJson<T>(string x)=>System.Text.Json.JsonSerializer.Deserialize<T>(x,Audit05.Options); }
}
public class SaveManager { public enum EstadoCarga { Concluido,SessaoNova } public EstadoCarga EstadoCarregamento; public static SaveManager Instancia; public void RegistrarRemocaoPermanente(string id) {} public void SolicitarSalvamentoInventario(){} }
public class Cocumelo:UnityEngine.Component {public bool collected;public bool EstaDesarmadoComoItemColetado()=>collected;public void DesarmarComoItemColetado(){collected=true;}public void NotificarEntradaInventario(){}public void CapturarEstadoRespawn(ItemPersistente.EstadoCenaItem s){}public void RestaurarEstadoRespawn(ItemPersistente.EstadoCenaItem s,bool original){} }
public class EstadoItemInventario:UnityEngine.Component { public bool estaNoInventario; }
public class Respawnitem:UnityEngine.Component { public bool EstaDesarmadoComoItemColetado()=>false; public void DesarmarComoItemColetado() {} }
namespace UnityEngine.XR.Interaction.Toolkit.Interactables {public class XRGrabInteractable:UnityEngine.Component {public bool isSelected;}}
public class XRGrabInteractable:UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable {}
public enum ClasseManopla { Comum }
public class Arcopopup:Espada {}
public class Flecha:UnityEngine.Component {
 public string CapturarUsosJson()=>null;
 public static object LerUsosJson(string json)=>null;
 public void RestaurarUsos(object estado,string json){}
}
public class Espada:UnityEngine.Component {
 public class EstadoEspada { public bool gerado; public float vidaMaxima; public int dano; public ClasseManopla classe; public List<int> atributos; }
 public bool Gerado; public string estado;
 public float VidaMaximaLegada=>vidaMaxima;
 public string ExportarEstadoJson()=>estado;
 public void RestaurarEstadoJson(string json){estado=json;Gerado=true;}
 public void RestaurarEstadoLegado(float atual,float maxima){}
 public static object LerEValidarEstadoJson(string json){if(json!=null&&json.StartsWith("roll:"))return null;throw new System.IO.InvalidDataException("Bad sword roll");}
 public int vidaAtual=100, vidaMaxima=100; public bool quebrada; public int VidaAtual=>vidaAtual; public int VidaMaxima=>vidaMaxima; public void AtualizarDurabilidadeVisual() {} }
public class Equipamento:Espada {}
public class Machado:Espada {
 public void PrepararSorteioParaSalvar(){}
 public string CapturarSorteioJson(string json)=>json;
 public static object LerSorteioJson(string json)=>null;
 public void RestaurarSorteio(string json,float durabilidade){}
}
public class Picareta:Espada {
 public void PrepararSorteioParaSalvar(){}
 public string CapturarSorteioJson(string json)=>json;
 public static object LerSorteioJson(string json)=>null;
 public void RestaurarSorteio(string json,float durabilidade){}
}
public class Escudo:Espada {
 public class EstadoSorteio {
  public const string Formato="my-reincarnation.escudo.v1";
  public string formato=Formato, anteriores;
  public int defesa, durabilidadeMaxima;
 }
 private EstadoSorteio sorteio;
 private bool durabilidadeRestaurada;
 private bool quebrado {get=>quebrada;set=>quebrada=value;}
 private void GarantirSorteio(){}
 public static EstadoSorteio LerSorteioJson(string json)=>json!=null && json.Contains(EstadoSorteio.Formato)
  ? UnityEngine.JsonUtility.FromJson<EstadoSorteio>(json) : null;
 // SHIELD_SAVE
}
public class Arco:UnityEngine.Component { public float durabilidadeAtual=100, durabilidadeMaxima=100; public bool arcoQuebrado; public float DurabilidadeAtual=>durabilidadeAtual; public float DurabilidadeMaxima=>durabilidadeMaxima; public void AtualizarDurabilidadeVisual() {} }
public class CaptureAdapter {
 private ItemPersistente GarantirItemPersistenteParaSave(XRGrabInteractable x)=>x.GetComponent<ItemPersistente>();
 private void AdicionarInstanciaIdUnico(List<string> ids,string id) { if(!ids.Contains(id))ids.Add(id); }
'@
$stub = $stub.Replace('// SHIELD_SAVE', $shieldSave)
$tests = @'
public static class Audit05 {
 public static readonly System.Text.Json.JsonSerializerOptions Options=new System.Text.Json.JsonSerializerOptions { IncludeFields=true };
 static int assertions;
 static void Check(bool x,string message) { assertions++; if(!x)throw new Exception(message); }
 static void Fail(Action a) { bool failed=false; try{a();}catch(Exception){failed=true;} Check(failed,"Expected rejection at assertion "+(assertions+1)); }
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
   if(unit.dadosExtrasJson!=null&&unit.dadosExtrasJson.Contains("my-reincarnation.manoplas.v1"))grab.gameObject.Add<AtributosManoplas>();
   if(unit.dadosExtrasJson!=null&&unit.dadosExtrasJson.Contains("my-reincarnation.peitoral.v1"))grab.gameObject.Add<AtributosPeitoral>();
   if(unit.dadosExtrasJson!=null&&unit.dadosExtrasJson.Contains("my-reincarnation.elmo.v1"))grab.gameObject.Add<EquiparElmo>();
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
  var originStack=new List<XRGrabInteractable>{New(ids[0],70)};
  originStack[0].GetComponent<ItemPersistente>().DefinirOrigemCena(ids[2]);
  var originData=Copy(Save(originStack));
  Check(originData.estadosIndividuais[0].origemCenaId==ids[2],"Origin lost in per-unit capture");
  Check(originData.CriarDataParaInstancia(ids[0]).origemCenaId==ids[2],"Origin lost in per-unit DTO");
  Check(Load(originData)[0].GetComponent<ItemPersistente>().OrigemCenaId==ids[2],"Origin lost restoring clone");
  Check(Load(originData)[0].GetComponent<ItemPersistente>().CriarEstadoCena().origemCenaId==ids[2],"Origin lost in scene state");
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
  var shieldGrab=New(ids[0],0,false);
  var shield=shieldGrab.gameObject.Add<Escudo>();
  var shieldPersist=shieldGrab.GetComponent<ItemPersistente>();
  var shieldScene=new ItemPersistente.EstadoCenaItem {
   instanciaId=ids[0],durabilidade=50,
   dadosExtrasJson=UnityEngine.JsonUtility.ToJson(new Escudo.EstadoSorteio {defesa=19,durabilidadeMaxima=30})
  };
  shieldPersist.AplicarEstadoCena(shieldScene);
  var shieldSlot=Save(new List<XRGrabInteractable>{shieldGrab});
  var shieldUnit=shieldSlot.CriarDataParaInstancia(ids[0]);
  var shieldRoll=Escudo.LerSorteioJson(shieldUnit.dadosExtrasJson);
  Check(shieldUnit.durabilidade==50&&shieldUnit.durabilidadeMaxima==50,"Scene shield 50/30 must be accepted by slot capture");
  Check(shieldRoll.durabilidadeMaxima==50&&shieldRoll.defesa==19,"Shield repair preserves the roll and synchronizes the limit");
  shieldScene.durabilidade=25;
  shieldScene.dadosExtrasJson=shieldUnit.dadosExtrasJson;
  shieldPersist.AplicarEstadoCena(shieldScene);
  Check(shield.vidaAtual==25&&shield.vidaMaxima==50,"Valid saved wear stays intact");
  shield.vidaMaxima=30;
  shieldScene.durabilidade=50;shieldScene.dadosExtrasJson=null;
  shieldPersist.AplicarEstadoCena(shieldScene);
  Check(shield.vidaAtual==50&&shield.vidaMaxima==50,"Legacy shield without roll keeps its saved durability");
  var swords=new List<XRGrabInteractable>{New(ids[0],80),New(ids[1],45)};
  for(int i=0;i<2;i++){var weapon=swords[i].GetComponent<Espada>();weapon.Gerado=true;weapon.estado="roll:sword"+i;}
  var swordSave=Save(swords);var swordLoad=Load(swordSave);
  for(int i=0;i<2;i++){
   var weapon=swordLoad[i].GetComponent<Espada>();Check(weapon.Gerado&&weapon.estado=="roll:sword"+i,"Sword per-instance roll lost");
   Check(weapon.VidaAtual==(i==0?80:45),"Sword restored with wrong durability");
   var ps=swordLoad[i].GetComponent<ItemPersistente>();var world=ps.CriarEstadoCena();weapon.vidaMaxima=50;ps.AplicarEstadoCena(world);Check(weapon.VidaMaxima==100,"World sword max lost");
  }
  var badSword=swordSave.CriarDataParaInstancia(ids[0]);badSword.dadosExtrasJson=badSword.dadosExtrasJson.Replace("roll:sword0","bad");Fail(()=>ItemPersistente.ValidarExtrasNoPrefab(swords[0].gameObject,badSword.dadosExtrasJson));
  var gloves=new List<XRGrabInteractable>{New(ids[0],100),New(ids[1],35)};
  for(int i=0;i<2;i++){var stats=gloves[i].gameObject.Add<AtributosManoplas>();stats.Gerado=true;stats.estado="roll:"+i;}
  var gloveSave=Save(gloves);var restoredGloves=Load(gloveSave);
  var badGlove=gloveSave.CriarDataParaInstancia(ids[0]);
  badGlove.dadosExtrasJson=badGlove.dadosExtrasJson.Replace("roll:0","invalid");
  Fail(()=>ItemPersistente.ValidarExtrasNoPrefab(New(ids[0],100).gameObject,badGlove.dadosExtrasJson));
  var noStats=new UnityEngine.GameObject();
  Fail(()=>ItemPersistente.ValidarExtrasNoPrefab(noStats,gloveSave.CriarDataParaInstancia(ids[0]).dadosExtrasJson));
  for(int i=0;i<2;i++){
   var stats=restoredGloves[i].GetComponent<AtributosManoplas>();
   Check(stats.Gerado&&stats.estado=="roll:"+i,"Individual glove roll lost");
   var expected=gloveSave.CriarDataParaInstancia(ids[i]);
   Check(restoredGloves[i].GetComponent<ItemPersistente>().CriarSaveData(0,1,false).dadosExtrasJson==expected.dadosExtrasJson,"Extra wrapper changed during validation");
   var scene=restoredGloves[i].GetComponent<ItemPersistente>().CriarEstadoCena();
   var next=New(ids[i],100);next.gameObject.Add<AtributosManoplas>();next.GetComponent<ItemPersistente>().AplicarEstadoCena(scene);
   Check(next.GetComponent<AtributosManoplas>().estado=="roll:"+i,"World glove roll lost");
   next.GetComponent<ItemPersistente>().AplicarSaveData(d.CriarDataParaInstancia(ids[i]));
   Check(!next.GetComponent<AtributosManoplas>().Gerado,"Legacy migration must defer roll until after validation");
  }
  var gloveGo=new UnityEngine.GameObject();var glovePersist=gloveGo.Add<ItemPersistente>();
  Field(glovePersist,"tipoItemId","glove-type");glovePersist.DefinirInstanciaId(ids[0]);
  var durability=gloveGo.Add<AtributosManoplas>();durability.vidaMaxima=150;durability.vidaAtual=99.5f;
  var durableData=glovePersist.CriarSaveData(0,1,false);
  Check(durableData.durabilidade==99.5f&&durableData.possuiDurabilidadeMaxima&&durableData.durabilidadeMaxima==150,"Float glove durability capture");
  ItemPersistente.ValidarEstadoIndividualNoPrefab(gloveGo,durableData);
  durability.vidaMaxima=100;durability.vidaAtual=100;glovePersist.AplicarSaveData(durableData);
  Check(durability.vidaAtual==99.5f&&durability.vidaMaxima==150,"Float glove durability restore");
  var gloveScene=glovePersist.CriarEstadoCena();durability.vidaMaxima=100;durability.vidaAtual=100;glovePersist.AplicarEstadoCena(gloveScene);
  Check(durability.vidaAtual==99.5f&&durability.vidaMaxima==150,"World glove durability restore");
  var legacyGlove=new InventorySaveData{durabilidade=-1};
  Check(ItemPersistente.EhMigracaoDurabilidadeManopla(gloveGo,legacyGlove),"Legacy glove migration");
  ItemPersistente.ValidarEstadoIndividualNoPrefab(gloveGo,legacyGlove);
  var chestGo=new UnityEngine.GameObject();var chestPersist=chestGo.Add<ItemPersistente>();
  Field(chestPersist,"tipoItemId","chest-type");chestPersist.DefinirInstanciaId(ids[1]);
  var chest=chestGo.Add<AtributosPeitoral>();chest.vidaMaxima=200;chest.vidaAtual=123.5f;chest.Gerado=true;chest.estado="roll:chest";
  var chestData=chestPersist.CriarSaveData(0,1,false);
  Check(chestData.durabilidade==123.5f&&chestData.durabilidadeMaxima==200,"Chest float durability capture");
  ItemPersistente.ValidarEstadoIndividualNoPrefab(chestGo,chestData);
  chest.vidaMaxima=100;chest.vidaAtual=100;chest.estado="changed";chestPersist.AplicarSaveData(chestData);
  Check(chest.vidaAtual==123.5f&&chest.vidaMaxima==200&&chest.estado=="roll:chest","Chest inventory persistence");
  var chestScene=chestPersist.CriarEstadoCena();chest.vidaAtual=1;chest.vidaMaxima=100;chest.estado="changed";chestPersist.AplicarEstadoCena(chestScene);
  Check(chest.vidaAtual==123.5f&&chest.vidaMaxima==200&&chest.estado=="roll:chest","Chest world persistence");
  Check(chestPersist.CriarSaveData(0,1,false).dadosExtrasJson==chestData.dadosExtrasJson,"Chest payload stable during verification");
  Check(ItemPersistente.EhMigracaoDurabilidadeManopla(chestGo,legacyGlove),"Chest legacy durability migration");
  Fail(()=>ItemPersistente.ValidarExtrasNoPrefab(gloveGo,chestData.dadosExtrasJson));
  var helmetGo=new UnityEngine.GameObject();var helmetPersist=helmetGo.Add<ItemPersistente>();
  Field(helmetPersist,"tipoItemId","helmet-type");helmetPersist.DefinirInstanciaId(ids[1]);
  var helmet=helmetGo.Add<EquiparElmo>();helmet.vidaMaxima=200;helmet.vidaAtual=123.5f;helmet.Gerado=true;helmet.estado="roll:helmet";
  var helmetData=helmetPersist.CriarSaveData(0,1,false);
  Check(helmetData.durabilidade==123.5f&&helmetData.durabilidadeMaxima==200,"Helmet float durability capture");
  ItemPersistente.ValidarEstadoIndividualNoPrefab(helmetGo,helmetData);
  helmet.vidaMaxima=100;helmet.vidaAtual=100;helmet.estado="changed";helmetPersist.AplicarSaveData(helmetData);
  Check(helmet.vidaAtual==123.5f&&helmet.vidaMaxima==200&&helmet.estado=="roll:helmet","Helmet inventory persistence");
  var helmetScene=helmetPersist.CriarEstadoCena();helmet.vidaAtual=1;helmet.vidaMaxima=100;helmet.estado="changed";helmetPersist.AplicarEstadoCena(helmetScene);
  Check(helmet.vidaAtual==123.5f&&helmet.vidaMaxima==200&&helmet.estado=="roll:helmet","Helmet world persistence");
  Check(helmetPersist.CriarSaveData(0,1,false).dadosExtrasJson==helmetData.dadosExtrasJson,"Helmet payload stable during verification");
  Check(ItemPersistente.EhMigracaoDurabilidadeManopla(helmetGo,legacyGlove),"Helmet legacy durability migration");
  Fail(()=>ItemPersistente.ValidarExtrasNoPrefab(gloveGo,helmetData.dadosExtrasJson));
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

