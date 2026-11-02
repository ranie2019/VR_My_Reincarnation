$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Get-Content -Raw (Join-Path $project 'Assets/Script/NPC/Ferreiro/Reparar.cs')
$typesStart = $source.IndexOf('    private static readonly string[] TiposReparaveis')
$typesEnd = $source.IndexOf('    private void Start()', $typesStart)
$types = $source.Substring($typesStart, $typesEnd - $typesStart)
$start = $source.IndexOf('    private void Update()')
$end = $source.IndexOf('    private void AtualizarValorNaTela(', $start)
$poll = $source.Substring($start, $end - $start)
$start = $source.IndexOf('    private void MoverItemParaSpawn(')
$end = $source.IndexOf('    private static Component ObterComponenteReparavel(', $start)
$move = $source.Substring($start, $end - $start)
$helpers = $source.Substring($end)
$code = @'
using System;
using System.Reflection;
using System.Collections.Generic;
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
public struct Quaternion {public int value;}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int RoundToInt(float f)=>(int)Math.Round(f);}
public class Component {
 public ItemPersistente persistence;
 public T GetComponentInParent<T>() where T:class=>persistence as T;
}
public class ItemPersistente:Component {public int reads;public void LerDurabilidade(){reads++;}}
public class Rigidbody:Component {public bool isKinematic=true;}
public class Transform {public GameObject gameObject;public Vector3 position;public Quaternion rotation;public Transform parent;public List<Transform> children=new List<Transform>();public int childCount=>children.Count;public Transform GetChild(int i)=>children[i];public void SetParent(Transform p){parent=p;}}
public class GameObject {
 public Transform transform;public Component[] components;public Rigidbody body=new Rigidbody();
 public GameObject(){transform=new Transform{gameObject=this};}
 public T GetComponent<T>() where T:class=>body as T;
 public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>components as T[];
}
public class Selected {public Transform transform;}
public class XRSocketInteractor {public Selected firstInteractableSelected;public Manager interactionManager=new Manager();}
public class Manager {public int exits;public Action onExit;public void SelectExit(XRSocketInteractor s,Selected selected){exits++;s.firstInteractableSelected=null;onExit?.Invoke();}}
public class AtributosManoplas:Component {public float VidaMaxima=>100;public float VidaAtual=70;public void RepararCompleto(){VidaAtual=100;}public void AtualizarDurabilidadeVisual(){}}
public class AtributosPeitoral:AtributosManoplas {}
public class AtributosElmo:AtributosManoplas {}
public class Espada:AtributosManoplas {}
public class RepairSocketChecks {
 private Transform slot,pontoSpawn;private GameObject itemNoSlot;private Component equipamentoNoSlot;private float valorAtualDoReparo;private float precoPorPontoDeVida=.1f;private XRSocketInteractor socketSlot;
 private void AtualizarValorNaTela(float value){valorAtualDoReparo=value;}
'@ + $types + $poll + $move + @'
 public static int Run(){int count=0;Action<bool,string> c=(ok,msg)=>{if(!ok)throw new Exception(msg);count++;};
  var h=new RepairSocketChecks();h.socketSlot=new XRSocketInteractor();h.slot=new Transform();
  var glove=new GameObject();var stats=new AtributosManoplas{persistence=new ItemPersistente()};glove.components=new Component[]{stats};
  h.socketSlot.firstInteractableSelected=new Selected{transform=glove.transform};h.Update();
  c(h.itemNoSlot==glove&&h.equipamentoNoSlot==stats,"Selected glove detected without inventory parenting");
  c(Math.Abs(h.valorAtualDoReparo-3)<.0001,"Existing repair price formula preserved");
  c(TentarRepararComponente(stats)&&stats.VidaAtual==100,"Glove repaired through actual durable adapter");
  c(stats.persistence.reads==1,"Durability persistence refreshed");
  foreach(Component armor in new Component[]{new AtributosPeitoral(),new AtributosElmo(),new Espada()}){
   var item=new GameObject{components=new[]{armor}};c(ObterComponenteReparavel(item)==armor,"Other armor and weapon remain repairable");
  }
  h.pontoSpawn=new Transform{position=new Vector3(1,2,3),rotation=new Quaternion{value=42}};
  bool releaseBeforeMove=false;h.socketSlot.interactionManager.onExit=()=>{releaseBeforeMove=glove.transform.position.x==0;};
  h.MoverItemParaSpawn(glove);
  c(h.socketSlot.firstInteractableSelected==null&&h.socketSlot.interactionManager.exits==1&&releaseBeforeMove,"XR released before transform movement");
  c(glove.transform.position.x==1&&glove.transform.position.y==2&&glove.transform.rotation.value==42&&!glove.body.isKinematic,"Spawn pose and dynamic physics preserved");
  h.Update();c(h.itemNoSlot==null&&h.valorAtualDoReparo==0,"Empty socket clears UI");
  var other=new GameObject{components=new Component[0]};h.socketSlot.firstInteractableSelected=new Selected{transform=other.transform};h.MoverItemParaSpawn(glove);
  c(h.socketSlot.firstInteractableSelected.transform.gameObject==other&&h.socketSlot.interactionManager.exits==1,"Does not release another selected item");
  h.socketSlot=null;h.slot.children.Add(glove.transform);h.Update();c(h.itemNoSlot==glove,"Non XR legacy fallback preserved");
  return count;
 }
'@ + $helpers
Add-Type -TypeDefinition $code
$checks = [RepairSocketChecks]::Run()

$prefab = Get-Content -Raw (Join-Path $project 'Assets/NPC/Ferreiro/Prefab/Ferreiro.prefab')
$blocks = [regex]::Split($prefab, '(?m)(?=^--- !u!)')
$repairSlotBlocks = $blocks | Where-Object { $_ -match 'm_GameObject: \{fileID: 3881159800803297619\}' }
if ($repairSlotBlocks -match 'Assembly-CSharp::SlotInventario') { throw 'Repair socket still has inventory filter/controller.' }
if (-not ($repairSlotBlocks -match 'UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor')) { throw 'Repair XR socket missing.' }
if (-not ($repairSlotBlocks -match 'm_IsTrigger: 1')) { throw 'Repair trigger missing.' }
Write-Output "PASS: $checks verificacoes dos metodos reais em host simulado; prefab de reparo sem SlotInventario, com socket e trigger preservados."
