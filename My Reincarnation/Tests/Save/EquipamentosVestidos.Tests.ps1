$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manager=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/GerenciadorEquipamentosPlayer.cs')
$start=$manager.IndexOf('    // Identificadores persistentes;')
$methods=$manager.Substring($start,$manager.IndexOf('    private void Awake()',$start)-$start)
$save=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SaveManager.cs')
$start=$save.IndexOf('    private static void ValidarPosicaoEquipamentoSalva(')
$validate=$save.Substring($start,$save.IndexOf('    private GerenciadorEquipamentosPlayer ResolverGerenciadorEquipamentos()',$start)-$start)
$start=$save.IndexOf('    private void RestaurarItemSoltoCena(')
$restore=$save.Substring($start,$save.IndexOf('    private ItemPersistente EncontrarItemSoltoPorOrigem(',$start)-$start)
$start=$save.IndexOf('    private bool ItemDeveSerSalvoComoCena(')
$shouldSave=$save.Substring($start,$save.IndexOf('    private bool ItemEstaNoInventarioOuSlot(',$start)-$start)

$item=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/ItemPersistente.cs')
$start=$item.IndexOf('    public class EstadoCenaItem')
$dto=$item.Substring($start,$item.IndexOf('    private void Awake()',$start)-$start)
$scene=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SceneObjectSaveData.cs')
$scene=[regex]::Replace($scene,'(?m)^using .*?;\s*$','')
$stub=@'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
public struct Quaternion {public static Quaternion Euler(Vector3 v)=>new Quaternion();}
public class Transform {public Vector3 position,localScale;public Quaternion rotation;public void SetParent(Transform p,bool world){}}
public class Renderer {public bool enabled=true;}
public class Rigidbody {public Vector3 position;public Quaternion rotation;public bool isKinematic,useGravity,detectCollisions;public void WakeUp(){}}
public class GameObject {
 public Transform transform=new Transform();public bool activeSelf=true;public List<object> components=new List<object>();
 public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();public void SetActive(bool a){activeSelf=a;}
}
public class RespawnNatureza {public static RespawnNatureza Instancia;public void OrganizarRecursoRestaurado(ItemPersistente item){}}
public class Respawnitem {}
public class Cocumelo {public void OrganizarClone(){}}
public enum LadoMaoEquipamento {Direita,Esquerda}
public class EquiparManoplas {public LadoMaoEquipamento LadoMao;public bool JaEquipado;public void Desequipar(){JaEquipado=false;}public Action CapturarEstadoEquipadoParaCarga(){bool old=JaEquipado;return ()=>JaEquipado=old;}}
public class EquiparElmo {public bool JaEquipado;public void Desequipar(){JaEquipado=false;}public Action CapturarEstadoEquipadoParaCarga(){bool old=JaEquipado;return ()=>JaEquipado=old;}}
public class EquiparPeitoral {public bool JaEquipado;public void Desequipar(){JaEquipado=false;}public Action CapturarEstadoEquipadoParaCarga(){bool old=JaEquipado;return ()=>JaEquipado=old;}}
public class ItemPersistente {
 public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;public string id,extras;public float vida;public bool loose,inBag;public bool DeveSalvarComoCena()=>loose;
 public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public void AplicarEstadoCena(EstadoCenaItem s){id=s.instanciaId;vida=s.durabilidade;extras=s.dadosExtrasJson;}public void MarcarComoSoltoNaCena(){loose=true;}
'@+$dto+@'
}
public class GerenciadorEquipamentosPlayer {
 public Transform maoDireita=new Transform(),maoEsquerda=new Transform(),cabecaAvatar=new Transform(),corpoAvatar=new Transform();
 public EquiparManoplas luvaDireita,luvaEsquerda;public EquiparElmo elmoAtual;public EquiparPeitoral peitoralAtual;public Renderer[] renderers=new[]{new Renderer()};public bool forceFailure;
 void ResolverReferencias(){}
 T[] GetComponentsInChildren<T>(bool inactive)=>renderers.OfType<T>().ToArray();
 bool TentarEquipar(EquiparManoplas v){if(forceFailure)return false;v.JaEquipado=true;if(v.LadoMao==LadoMaoEquipamento.Direita)luvaDireita=v;else luvaEsquerda=v;return true;}
 bool TentarEquiparElmo(EquiparElmo v,bool restaurandoSave){if(!restaurandoSave||forceFailure)return false;v.JaEquipado=true;elmoAtual=v;return true;}
 bool TentarEquipar(EquiparPeitoral v){if(forceFailure)return false;v.JaEquipado=true;peitoralAtual=v;return true;}
'@+$methods+@'
}
public class RestoreHost {
 public readonly List<KeyValuePair<ItemPersistente,string>> equipamentosParaRestaurar=new List<KeyValuePair<ItemPersistente,string>>();
 static bool VetorFinito(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x);
 static bool EscalaValida(Vector3 v)=>v.x>0&&v.y>0&&v.z>0;
'@+$restore+$validate+$shouldSave+@'
 private bool ItemEstaNoInventarioOuSlot(ItemPersistente i)=>i.inBag;
 public bool ShouldSave(ItemPersistente i)=>ItemDeveSerSalvoComoCena(i);
 public void Cleanup()=>LiberarEquipamentosDaCarga();
 public void Restore(ItemPersistente i,SceneObjectSaveData d,ItemPersistente.EstadoCenaItem s)=>RestaurarItemSoltoCena(i,d,s);
 public static void Validate(ItemPersistente.EstadoCenaItem s,HashSet<string> seen,bool active=true)=>ValidarPosicaoEquipamentoSalva(new SceneObjectSaveData{ativo=active,objectId="test"},s,seen);
}
public static class WornSaveChecks {
 static int count;static void Check(bool ok,string m){if(!ok)throw new Exception(m);count++;}
 static void Reject(Action action,string m){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,m);}
 public static int Run(){
  var owner=new GerenciadorEquipamentosPlayer();var units=new List<ItemPersistente>();
  foreach(string slot in new[]{"mao-direita","mao-esquerda","cabeca","peitoral"}){
   var i=new ItemPersistente();if(slot.StartsWith("mao"))i.gameObject.components.Add(new EquiparManoplas{LadoMao=slot=="mao-direita"?LadoMaoEquipamento.Direita:LadoMaoEquipamento.Esquerda});
   else if(slot=="cabeca")i.gameObject.components.Add(new EquiparElmo());else i.gameObject.components.Add(new EquiparPeitoral());
   var state=new ItemPersistente.EstadoCenaItem{posicaoEquipamento=slot,instanciaId=Guid.NewGuid().ToString("N"),durabilidade=42.5f,dadosExtrasJson="unchanged rolled attributes"};
   var json=JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true});var loaded=JsonSerializer.Deserialize<ItemPersistente.EstadoCenaItem>(json,new JsonSerializerOptions{IncludeFields=true});
   RestoreHost.Validate(loaded,new HashSet<string>());var h=new RestoreHost();h.Restore(i,new SceneObjectSaveData{ativo=true,scaleX=1,scaleY=1,scaleZ=1},loaded);
   Check(h.equipamentosParaRestaurar.Count==1&&GerenciadorEquipamentosPlayer.IdentificarPosicaoVestida(i.gameObject)=="","Restoration queues after item data, before equipping");
   owner.RestaurarEquipamentoVestido(h.equipamentosParaRestaurar[0].Key,h.equipamentosParaRestaurar[0].Value);
   Check(GerenciadorEquipamentosPlayer.IdentificarPosicaoVestida(i.gameObject)==slot,"Correct wearable slot restored");
   Check(i.id==state.instanciaId&&i.vida==42.5f&&i.extras==state.dadosExtrasJson,"Per item identity durability and rolls unchanged");units.Add(i);
  }
  var captureHost=new RestoreHost();units[0].loose=false;Check(captureHost.ShouldSave(units[0]),"Worn item saved even with loose-world saving disabled");units[0].inBag=true;Check(!captureHost.ShouldSave(units[0]),"No duplicate worn entry for inventory item");units[0].inBag=false;
  var partial=new ItemPersistente();var partialGlove=new EquiparManoplas{JaEquipado=true};partial.gameObject.components.Add(partialGlove);var partialHost=new RestoreHost();partialHost.Restore(partial,new SceneObjectSaveData{ativo=true},new ItemPersistente.EstadoCenaItem{posicaoEquipamento="mao-direita"});partialHost.Cleanup();Check(!partialGlove.JaEquipado,"Cleanup releases partial equip not yet registered in manager");
  var rollback=owner.CapturarRecuperacaoEquipamentos();var oldRight=owner.luvaDireita;owner.renderers[0].enabled=false;owner.PrepararRestauracaoEquipamentos();
  Check(owner.luvaDireita==null&&owner.elmoAtual==null&&!oldRight.JaEquipado,"Load preparation detaches without inventory insertion");rollback();
  Check(owner.luvaDireita==oldRight&&oldRight.JaEquipado&&owner.renderers[0].enabled,"Recovery preserves previous equipment references and renderer state");
  var old=JsonSerializer.Deserialize<ItemPersistente.EstadoCenaItem>("{\"instanciaId\":\"legacy\",\"durabilidade\":80}",new JsonSerializerOptions{IncludeFields=true});var oldHost=new RestoreHost();oldHost.Restore(new ItemPersistente(),new SceneObjectSaveData{ativo=true},old);
  Check(oldHost.equipamentosParaRestaurar.Count==0,"Old saves without slot remain loose, no guessing");
  var seen=new HashSet<string>();var right=new ItemPersistente.EstadoCenaItem{posicaoEquipamento="mao-direita",durabilidade=50};RestoreHost.Validate(right,seen);
  Reject(()=>RestoreHost.Validate(right,seen),"Duplicate equipped slot rejected before mutation");
  Reject(()=>RestoreHost.Validate(new ItemPersistente.EstadoCenaItem{posicaoEquipamento="unknown"},new HashSet<string>()),"Unknown slot rejected");
  Reject(()=>RestoreHost.Validate(right,new HashSet<string>(),false),"Inactive equipped item rejected");
  Reject(()=>RestoreHost.Validate(new ItemPersistente.EstadoCenaItem{posicaoEquipamento="cabeca",durabilidade=0},new HashSet<string>()),"Broken equipped item rejected");
  foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-5f})
   Reject(()=>RestoreHost.Validate(new ItemPersistente.EstadoCenaItem{posicaoEquipamento="cabeca",durabilidade=bad},new HashSet<string>()),"Non finite or invalid durability rejected");
  Reject(()=>owner.ValidarEquipamentoParaCarga(units[0].gameObject,"mao-esquerda"),"Wrong glove hand rejected");
  owner.cabecaAvatar=null;Reject(()=>owner.ValidarEquipamentoParaCarga(units[2].gameObject,"cabeca"),"Missing avatar target rejected");
  var ambiguous=new GameObject();ambiguous.components.Add(new EquiparManoplas());ambiguous.components.Add(new EquiparElmo());Reject(()=>owner.ValidarEquipamentoParaCarga(ambiguous,"mao-direita"),"Conflicting equipment controllers rejected");
  owner.forceFailure=true;bool failed=false;try{owner.RestaurarEquipamentoVestido(units[0],"mao-direita");}catch(InvalidOperationException){failed=true;}Check(failed,"Equip failure cannot be reported as successful load");
  return count;
 }
}
'@
Add-Type -TypeDefinition ($stub+$scene) -CompilerOptions '/nowarn:0649,0414'
$checks=[WornSaveChecks]::Run()
if($save.IndexOf('RestaurarEquipamentoVestido(equipamento.Key') -gt $save.IndexOf('RestaurarStatusPlayer(statusRestaurado')){throw 'Resources restored before gear bonuses.'}
if($save.IndexOf('gerenciadorEquipamentos?.PrepararRestauracaoEquipamentos();') -lt $save.IndexOf('iniciouAplicacao = true;')){throw 'Equipment mutated before preflight.'}
Write-Output "PASS: $checks verificacoes dos metodos reais de slots/estado/restauracao em host simulado; ordem da carga verificada. Json simulado via System.Text.Json, sem Unity/Quest."
