$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cog=Get-Content -Raw (Join-Path $project 'Assets/Script/Natureza/Cocumelo.cs')
$manager=Get-Content -Raw (Join-Path $project 'Assets/Script/Respawn/RespawnNatureza.cs')
foreach($key in @('cog','manager')){
 $value=Get-Variable -Name $key -ValueOnly
 Set-Variable -Name $key -Value ([regex]::Replace($value,'(?m)^using .*?;\s*$',''))
}
$item=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/ItemPersistente.cs')
$a=$item.IndexOf('    public class EstadoCenaItem')
$dto=$item.Substring($a,$item.IndexOf('    private void Awake()',$a)-$a)
$a=$item.IndexOf('    internal static void ValidarPontoCogumelo(')
$validation=$item.Substring($a,$item.IndexOf('    private bool RespawnItemEstaDesarmado()',$a)-$a)
$stub=@'
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace UnityEngine {
 public class DisallowMultipleComponent:Attribute {}
 public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
 public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
 public class MinAttribute:Attribute {public MinAttribute(float f){}}
 public class SerializeField:Attribute {} public class HideInInspector:Attribute {}
 public enum FindObjectsInactive {Include} public enum FindObjectsSortMode {None}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3(0,1,0);public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}
 public struct Quaternion {public Vector3 eulerAngles;public static Quaternion Euler(Vector3 v)=>new Quaternion{eulerAngles=v};public static Quaternion identity=>new Quaternion();}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int RoundToInt(float f)=>(int)Math.Round(f);}
 public struct Scene {public static bool operator ==(Scene a,Scene b)=>true;public static bool operator !=(Scene a,Scene b)=>false;public override bool Equals(object o)=>o is Scene;public override int GetHashCode()=>1;}
 public static class Application {public static bool isPlaying=true;}
 public static class Debug {public static List<string> errors=new List<string>();public static void LogError(string s,object context){errors.Add(s);}}
 public class Transform:Component,IEnumerable<Transform> {public Vector3 position;public Quaternion rotation;public Transform parent;public void SetParent(Transform p,bool world){parent=p;}public bool IsChildOf(Transform p){for(var t=this;t!=null;t=t.parent)if(t==p)return true;return false;}public IEnumerator<Transform> GetEnumerator()=>GameObject.all.Where(g=>g.transform.parent==this).Select(g=>g.transform).GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
 public class Component {public GameObject gameObject;public Transform transform=>gameObject.transform;public string name=>gameObject.name;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>gameObject.GetComponentsInChildren<T>(inactive);}
 public class GameObject {
  public static List<GameObject> all=new List<GameObject>();public string name;public bool asset,activeSelf=true;public Scene scene;public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);public Transform transform;public List<Component> components=new List<Component>();
  public GameObject(string n=""){name=n;transform=new Transform{gameObject=this};components.Add(transform);all.Add(this);}
  public T Add<T>(T c) where T:Component{c.gameObject=this;components.Add(c);return c;}public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>components.OfType<T>().ToArray();public void SetActive(bool a){activeSelf=a;}
 }
 public class WaitForSeconds {public float seconds;public WaitForSeconds(float f){seconds=f;}}
 public class Coroutine {public MonoBehaviour owner;public IEnumerator routine;public double due;public bool alive=true;}
 public class MonoBehaviour:Component {
  public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
  public static T[] FindObjectsByType<T>(FindObjectsInactive inactive,FindObjectsSortMode order) where T:class=>GameObject.all.Where(g=>!g.asset).SelectMany(g=>g.components).OfType<T>().ToArray();
  public Coroutine StartCoroutine(IEnumerator e){var c=new Coroutine{owner=this,routine=e};if(Clock.Step(c))Clock.coroutines.Add(c);return c;}
  public void StopCoroutine(Coroutine c){c.alive=false;(c.routine as IDisposable)?.Dispose();}
  public void StopAllCoroutines(){foreach(var c in Clock.coroutines.Where(c=>c.owner==this).ToArray()){c.alive=false;(c.routine as IDisposable)?.Dispose();}}
  public static GameObject Instantiate(GameObject prefab,Vector3 p,Quaternion r){var g=new GameObject(prefab.name+"(Clone)");g.transform.position=p;g.transform.rotation=r;g.Add(new ItemInventarioDados{NomeItem=prefab.GetComponent<ItemInventarioDados>()?.NomeItem});g.Add(new ItemPersistente());if(prefab.GetComponent<Cocumelo>()!=null){var c=g.Add(new Cocumelo());TestHooks.Call(c,"Awake");}if(prefab.GetComponent<Respawnitem>()!=null)g.Add(new Respawnitem());Clock.spawned.Add(g);return g;}
  public static void Destroy(GameObject g){g.activeSelf=false;GameObject.all.Remove(g);}
 }
 public static class Clock {public static double now;public static List<Coroutine> coroutines=new List<Coroutine>();public static List<GameObject> spawned=new List<GameObject>();public static bool Step(Coroutine c){if(!c.routine.MoveNext()){c.alive=false;return false;}c.due=now+(c.routine.Current is WaitForSeconds w?w.seconds:.001);return true;}public static void Tick(double d){now+=d;foreach(var c in coroutines.ToArray())if(c.alive&&c.due<=now+.00001)Step(c);coroutines.RemoveAll(c=>!c.alive);}}
}
namespace UnityEngine.XR.Interaction.Toolkit {public class SelectEnterEventArgs {public object interactorObject;}}
namespace UnityEngine.XR.Interaction.Toolkit.Interactors {public class XRSocketInteractor {}}
namespace UnityEngine.XR.Interaction.Toolkit.Interactables {public class SelectionEvent {public event Action<SelectEnterEventArgs> handlers;public void AddListener(Action<SelectEnterEventArgs> a){handlers+=a;}public void RemoveListener(Action<SelectEnterEventArgs> a){handlers-=a;}public void Invoke(SelectEnterEventArgs a){handlers?.Invoke(a);}}public class XRGrabInteractable:Component {public List<object> interactorsSelecting=new List<object>();public SelectionEvent selectEntered=new SelectionEvent();}}
namespace UnityEngine.Serialization {public class FormerlySerializedAsAttribute:Attribute {public FormerlySerializedAsAttribute(string s){}}}
namespace UnityEngine.SceneManagement {public static class SceneManager {public static void MoveGameObjectToScene(GameObject g,Scene s){g.scene=s;}}}
public class SaveManager {public enum EstadoCarga{NaoIniciado,SessaoNova,EmAndamento,Concluido,Falhou}public static SaveManager Instancia;public EstadoCarga EstadoCarregamento;}
public class ItemInventarioDados:Component {public string NomeItem;}
public class Respawnitem:Component {public string id;public string ObterRespawnId()=>id;public int preparations;public void PrepararComoRecursoDisponivelNoMundo(){preparations++;}}
public class ItemPersistente:Component {public string OrigemCenaId;public bool stored;public bool EstaNoInventario()=>stored;public string ObterTipoItemId()=>gameObject.name;
'@+$dto+$validation+@'
}
public static class TestHooks {
 public static object Call(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 public static void Pickup(Cocumelo c){var g=c.GetComponent<XRGrabInteractable>()??c.gameObject.Add(new XRGrabInteractable());Call(c,"OnDisable");Call(c,"OnEnable");object hand=new object();g.interactorsSelecting.Clear();g.interactorsSelecting.Add(hand);g.selectEntered.Invoke(new SelectEnterEventArgs{interactorObject=hand});}
 public static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
}
'@
$checks=@'
public static class MushroomChecks {
 static int count;static void Check(bool v,string s){if(!v)throw new Exception(s);count++;}
 static void Update(Cocumelo c,RespawnNatureza m){TestHooks.Call(c,"Update");TestHooks.Call(m,"Update");}
 static void Reject(Action a,string s){bool caught=false;try{a();}catch(System.IO.InvalidDataException){caught=true;}Check(caught,s);}
 public static int Run(){
  var prefab=new GameObject("Mushroom_Orange"){asset=true};prefab.Add(new Cocumelo());prefab.Add(new ItemInventarioDados{NomeItem="Mushroom_Orange"});
  var plant=new GameObject("Plant"){asset=true};plant.Add(new Respawnitem());
  var source=new GameObject("Original");var anchor=new Vector3(10,2,20);source.transform.position=anchor;source.Add(new ItemInventarioDados{NomeItem="Mushroom_Orange"});source.Add(new ItemPersistente());var c=source.Add(new Cocumelo());TestHooks.Call(c,"Awake");
  var unrelatedFolder=new GameObject("Clone Cogumelo");var world=new GameObject("---STATIC---");var nature=new GameObject("NATUREZA");nature.transform.SetParent(world.transform,true);var group=new GameObject("COGUMELO");group.transform.SetParent(nature.transform,true);source.transform.SetParent(group.transform,true);var folder=new GameObject("clone Cogumelo");folder.transform.SetParent(group.transform,true);
  var source2=new GameObject("Second fixed point");source2.transform.position=new Vector3(40,3,25);source2.transform.SetParent(group.transform,true);source2.Add(new ItemInventarioDados{NomeItem="Mushroom_Orange"});source2.Add(new ItemPersistente());var c2=source2.Add(new Cocumelo());c2.tempoRespawn=3;TestHooks.Call(c2,"Awake");
  var root=new GameObject("Respawn");var m=root.Add(new RespawnNatureza());
  TestHooks.Set(m,"configuracoesNatureza",new[]{new RespawnNatureza.ConfiguracaoRespawnNatureza{idNatureza="Mushroom_Orange",prefabRespawn=prefab,tempoRespawn=15},new RespawnNatureza.ConfiguracaoRespawnNatureza{idNatureza="Plant",prefabRespawn=plant,tempoRespawn=3,offsetRespawn=new Vector3(1,0,0)}});
  TestHooks.Call(m,"Awake");Check(c.tempoRespawn==30&&c.distanciaParaRespawn==1&&c.alturaRespawn==.5f,"Requested public defaults");
  source.transform.SetParent(new GameObject("Other parent").transform,true);Update(c,m);Clock.Tick(35);Check(Clock.spawned.Count==0,"Parent change alone never triggers respawn");
  source.transform.position=new Vector3(10.99f,2,20);Update(c,m);Clock.Tick(35);Check(Clock.spawned.Count==0,"Movement below one meter does not respawn");
  source.transform.position=new Vector3(12,2,20);Update(c,m);Clock.Tick(35);Check(Clock.spawned.Count==0,"Unattended physics movement cannot trigger collection");source.transform.position=anchor;TestHooks.Pickup(c);Update(c,m);
  source.transform.position=new Vector3(11,2,20);for(int i=0;i<20;i++)Update(c,m);Clock.Tick(29.9);Check(Clock.spawned.Count==0,"No early spawn");Clock.Tick(.1);Check(Clock.spawned.Count==1,"Exactly one replacement after thirty seconds");
  var first=Clock.spawned[0];var replacement=first.GetComponent<Cocumelo>();Check(Vector3.Distance(first.transform.position,new Vector3(10,2.5f,20))<.001,"Original position plus half meter");Check(first.transform.parent==folder.transform,"Existing clone folder found automatically");
  first.transform.position=new Vector3(10.99f,2,20);Update(replacement,m);Clock.Tick(35);Check(Clock.spawned.Count==1,"Settling from birth height plus sub-meter horizontal drift does not collect");first.transform.position=new Vector3(10,2.5f,20);
  Update(replacement,m);Clock.Tick(90);TestHooks.Call(m,"Update");Check(Clock.spawned.Count==1,"Available clone does not multiply");
  TestHooks.Pickup(replacement);first.transform.position=new Vector3(12,2.5f,20);Update(replacement,m);Clock.Tick(30);Check(Clock.spawned.Count==2,"Collected replacement can replenish same point");var second=Clock.spawned[1];Check(Vector3.Distance(second.transform.position,new Vector3(10,2.5f,20))<.001,"Height never accumulates across generations");
  var secondC=second.GetComponent<Cocumelo>();secondC.tempoRespawn=2;TestHooks.Pickup(secondC);second.transform.position=new Vector3(12,2.5f,20);Update(secondC,m);Clock.Tick(1.9);Check(Clock.spawned.Count==2,"Public custom delay honored before expiry");Clock.Tick(.1);Check(Clock.spawned.Count==3,"Public custom delay honored at expiry");
  var third=Clock.spawned[2];var thirdC=third.GetComponent<Cocumelo>();TestHooks.Pickup(thirdC);third.transform.position=new Vector3(12,2.5f,20);Update(thirdC,m);third.transform.position=new Vector3(10,2.5f,20);Clock.Tick(2);Check(Clock.spawned.Count==3&&!thirdC.EstaDesarmadoComoItemColetado(),"Returned mushroom cancels replacement and rearms");
  SaveManager.Instancia=new SaveManager{EstadoCarregamento=SaveManager.EstadoCarga.EmAndamento};third.transform.position=new Vector3(12,2.5f,20);Update(thirdC,m);Clock.Tick(40);Check(Clock.spawned.Count==3,"Load movement never schedules respawn during application");SaveManager.Instancia.EstadoCarregamento=SaveManager.EstadoCarga.Concluido;TestHooks.Pickup(thirdC);Update(thirdC,m);Clock.Tick(2);Check(Clock.spawned.Count==4,"Processing resumes only after successful load");
  var fourth=Clock.spawned[3];var fourthC=fourth.GetComponent<Cocumelo>();var state=new ItemPersistente.EstadoCenaItem();fourthC.CapturarEstadoRespawn(state);Check(state.possuiPontoCogumelo&&Vector3.Distance(state.pontoCogumelo,anchor)<.001&&state.tempoPontoCogumelo==2,"Fixed anchor and public config exported");
  var restored=new GameObject("Restored");restored.Add(new ItemInventarioDados{NomeItem="Mushroom_Orange"});restored.Add(new ItemPersistente());var restoredC=restored.Add(new Cocumelo());TestHooks.Call(restoredC,"Awake");restoredC.RestaurarEstadoRespawn(state,false);restored.transform.position=new Vector3(10,2.5f,20);Update(restoredC,m);Check(Vector3.Distance(restoredC.PontoOriginal,anchor)<.001&&restored.transform.parent==folder.transform,"Saved clone restores anchor and grouping, not Instantiate origin");
  var legacy=new GameObject("Legacy clone");legacy.Add(new ItemPersistente());legacy.Add(new ItemInventarioDados{NomeItem="Mushroom_Orange"});var legacyC=legacy.Add(new Cocumelo());TestHooks.Call(legacyC,"Awake");legacyC.RestaurarEstadoRespawn(new ItemPersistente.EstadoCenaItem(),false);Update(legacyC,m);Clock.Tick(40);Check(legacyC.EstaDesarmadoComoItemColetado()&&Clock.spawned.Count==4,"Legacy runtime copies remain usable without creating zero-origin farms");
  fourth.GetComponent<ItemPersistente>().stored=true;fourthC.NotificarEntradaInventario();fourthC.DesarmarComoItemColetado();restored.GetComponent<ItemPersistente>().stored=true;restoredC.NotificarEntradaInventario();restoredC.DesarmarComoItemColetado();TestHooks.Call(m,"Update");Clock.Tick(2);Check(Clock.spawned.Count==5,"Stored collected copies cannot occupy world point; one replacement");
  ItemPersistente.ValidarPontoCogumelo(state);Check(true,"Valid point accepted");state.tempoPontoCogumelo=float.NaN;Reject(()=>ItemPersistente.ValidarPontoCogumelo(state),"Non finite metadata rejected");state.tempoPontoCogumelo=-1;Reject(()=>ItemPersistente.ValidarPontoCogumelo(state),"Negative delay rejected");state.tempoPontoCogumelo=2;state.distanciaPontoCogumelo=0;Reject(()=>ItemPersistente.ValidarPontoCogumelo(state),"Invalid distance rejected");
  int before=Clock.spawned.Count;m.AgendarRespawn("Plant",new Vector3(4,5,6),Quaternion.identity);m.AgendarRespawn("Plant",new Vector3(4,5,6),Quaternion.identity);Clock.Tick(3);Check(Clock.spawned.Count==before+1&&Clock.spawned.Last().transform.position.x==5,"Other resources preserve generic timer offset and deduplication");
  Check(Clock.spawned.Last().GetComponent<Respawnitem>().preparations==1,"Other resource initialization preserved");
  Update(c2,m);TestHooks.Pickup(c2);source2.transform.position=new Vector3(42,3,25);int previous=Clock.spawned.Count;Update(c2,m);Clock.Tick(3);Check(Clock.spawned.Count==previous+1,"Same mushroom type at second point replenishes independently");Check(Vector3.Distance(Clock.spawned.Last().transform.position,new Vector3(40,3.5f,25))<.001,"Second point uses its own original location");
  var recent=Clock.spawned.Last().GetComponent<Cocumelo>();SaveManager.Instancia.EstadoCarregamento=SaveManager.EstadoCarga.Falhou;recent.transform.position=new Vector3(42,3.5f,25);previous=Clock.spawned.Count;Update(recent,m);Clock.Tick(40);Check(Clock.spawned.Count==previous,"Failed save recovery cannot trigger spawns");SaveManager.Instancia.EstadoCarregamento=SaveManager.EstadoCarga.Concluido;
  TestHooks.Pickup(recent);Update(recent,m);Clock.Tick(1);recent.transform.position=new Vector3(40,3.5f,25);Update(recent,m);Clock.Tick(1);Check(!recent.EstaDesarmadoComoItemColetado()&&Clock.spawned.Count==previous,"Returning cancels timer immediately");recent.transform.position=new Vector3(42,3.5f,25);Update(recent,m);Clock.Tick(2.1);Check(Clock.spawned.Count==previous,"Second departure starts a full new delay");Clock.Tick(.9);Check(Clock.spawned.Count==previous+1,"Second departure replenishes only after full delay");
  var instant=Clock.spawned.Last().GetComponent<Cocumelo>();instant.tempoRespawn=0;TestHooks.Pickup(instant);instant.transform.position=new Vector3(42,3.5f,25);previous=Clock.spawned.Count;Update(instant,m);Clock.Tick(.001);Check(Clock.spawned.Count==previous+1,"Zero custom delay waits a frame and creates one clone");Update(Clock.spawned.Last().GetComponent<Cocumelo>(),m);Clock.Tick(10);Check(Clock.spawned.Count==previous+1,"Zero delay still cannot create a clone loop");
  foreach(var pair in new[]{new[]{"Mushroom Orange","Mushroom_Orange"},new[]{"Mushroom 01","Mushroom_01"},new[]{"Mushroom 02","Mushroom_02"}}){var alias=new GameObject("alias").Add(new Cocumelo());TestHooks.Set(alias,"respawnId",pair[0]);Check(alias.ObterRespawnId()==pair[1],"Legacy respawn aliases match configured prefab IDs");}
  var recoveryObject=new GameObject("runtime rollback");recoveryObject.transform.position=new Vector3(70,5,20);var recoveryC=recoveryObject.Add(new Cocumelo());TestHooks.Call(recoveryC,"Awake");recoveryC.DesarmarComoItemColetado();var recover=recoveryC.CapturarRecuperacaoRespawn();recoveryC.PrepararNoPonto(Vector3.zero,Quaternion.identity,1,30,.5f);recover();Check(Vector3.Distance(recoveryC.PontoOriginal,new Vector3(70,5,20))<.001&&recoveryC.EstaDesarmadoComoItemColetado()&&!recoveryC.EhReposicao,"Rollback restores private anchor collected and clone flags");
  state.tempoPontoCogumelo=2;state.distanciaPontoCogumelo=1;state.pontoCogumelo=new Vector3(float.NaN,0,0);Reject(()=>ItemPersistente.ValidarPontoCogumelo(state),"Invalid anchor rejected before application");
  var dropped=new GameObject("Unbound loaded copy");dropped.transform.position=new Vector3(100,0,0);dropped.Add(new ItemInventarioDados{NomeItem="Mushroom_Orange"});var unbound=dropped.Add(new Cocumelo());TestHooks.Call(unbound,"Awake");TestHooks.Pickup(unbound);dropped.transform.position=new Vector3(102,0,0);previous=Clock.spawned.Count;Update(unbound,m);Clock.Tick(40);Check(Clock.spawned.Count==previous,"Loaded or reward copies cannot register new authored spawn points");
  var current=Clock.spawned.Last().GetComponent<Cocumelo>();current.tempoRespawn=3;TestHooks.Pickup(current);current.transform.position=new Vector3(42,3.5f,25);Update(current,m);var pending=m.CapturarPontosPendentes();Check(pending.Count==1&&pending[0].espera==3,"Only a confirmed collection persists pending replacement");m.RestaurarPontosPendentes(pending);TestHooks.Call(m,"Update");Clock.Tick(3);Check(Clock.spawned.Count==previous+1,"Saved confirmed pending point resumes once");
  var final=Clock.spawned.Last().GetComponent<Cocumelo>();final.tempoRespawn=0;TestHooks.Pickup(final);final.transform.position=new Vector3(42,3.5f,25);folder.transform.SetParent(null,true);m.pastaClonesCogumelos=null;int folders=GameObject.all.Count(g=>string.Equals(g.name,"clone Cogumelo",StringComparison.OrdinalIgnoreCase));previous=Clock.spawned.Count;Update(final,m);Clock.Tick(.001);Check(Clock.spawned.Count==previous&&GameObject.all.Count(g=>string.Equals(g.name,"clone Cogumelo",StringComparison.OrdinalIgnoreCase))==folders,"Missing existing folder never creates a folder or loose clone");Check(Debug.errors.Count==1,"Missing hierarchy reports one clear error");
  var resources=new GameObject("RECURSOS");resources.transform.SetParent(nature.transform,true);
  foreach(var names in new[]{new[]{"Stone","Stone","Stone Clone"},new[]{"Wood","Wood","Wood Clone"},new[]{"Rocha Metalica","Metallic Rock","Metallic Rock"}}){
   var resourceGroup=new GameObject(names[1]);resourceGroup.transform.SetParent(resources.transform,true);var cloneFolder=new GameObject(names[2]);cloneFolder.transform.SetParent(resourceGroup.transform,true);
   var resourcePrefab=new GameObject(names[0]){asset=true};resourcePrefab.Add(new Respawnitem{id=names[0]});
   TestHooks.Set(m,"configuracoesNatureza",new[]{new RespawnNatureza.ConfiguracaoRespawnNatureza{idNatureza=names[0],prefabRespawn=resourcePrefab,tempoRespawn=0}});
   m.AgendarRespawn(names[0],new Vector3(7,8,9),Quaternion.identity);Clock.Tick(.001);var born=Clock.spawned.Last();Check(born.transform.parent==cloneFolder.transform&&born.transform.position.x==7,"Resource clone uses existing scoped folder and preserves world position");
   var restoredResource=new GameObject("restored resource");restoredResource.Add(new Respawnitem{id=names[0]});var restoredItem=restoredResource.Add(new ItemPersistente());m.OrganizarRecursoRestaurado(restoredItem);Check(restoredResource.transform.parent==cloneFolder.transform,"Saved loose clone organized by resource type");
   if(names[0]=="Rocha Metalica"){restoredResource.GetComponent<Respawnitem>().id="Metallic Rock";m.OrganizarRecursoRestaurado(restoredItem);Check(restoredResource.transform.parent==cloneFolder.transform,"Renamed Metallic Rock matches legacy Rocha Metalica config");}
   restoredItem.OrigemCenaId="authored";m.OrganizarRecursoRestaurado(restoredItem);Check(restoredResource.transform.parent==resourceGroup.transform,"Restored original remains under original resource group");
   restoredItem.stored=true;restoredResource.transform.SetParent(null,true);m.OrganizarRecursoRestaurado(restoredItem);Check(restoredResource.transform.parent==null,"Inventory resources never reparented to world folders");
  }
  return count;
 }
}
'@
Add-Type -TypeDefinition ($stub+$cog+$manager+$checks) -CompilerOptions '/nowarn:0649,0414'
$count=[MushroomChecks]::Run()
Write-Output "PASS: $count verificacoes das classes reais Cocumelo/RespawnNatureza em host simulado, com relogio/coroutines/Instantiate simulados; fisica e Unity JsonUtility nao executados."
