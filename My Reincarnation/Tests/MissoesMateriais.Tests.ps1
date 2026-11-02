$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/NPC/Sacerdote/GerenciadorMissoes.cs')
$start=$source.IndexOf('    public void EntregarRecompensaEAvancar()')
$end=$source.IndexOf('    private void EntregarRecompensasAoPlayer', $start)
$method=$source.Substring($start,$end-$start)
$stub=@'
namespace UnityEngine {
 public struct Vector3 {} public struct Quaternion {public static Quaternion identity=>new Quaternion();}
 public class Transform {public Vector3 position;public Quaternion rotation;}
 public class Component {public GameObject gameObject;public T GetComponent<T>() where T:class=>gameObject.component as T;}
 public class GameObject {public Transform transform=new Transform();public object component;public bool active=true,destroyed;public void SetActive(bool x){active=x;}}
 public static class Debug {public static void LogError(object a,object b){} }
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);}
 public static class JsonUtility {public static string ToJson(object s)=>((StatusPlayer)s).xp.ToString();public static void FromJsonOverwrite(string s,object o){((StatusPlayer)o).xp=int.Parse(s);}}
}
namespace UnityEngine.XR.Interaction.Toolkit.Interactables {public class XRGrabInteractable:Component {public int id;}}
public class SaveManager {public static SaveManager Instancia=new SaveManager();public int locks;public void NotificarFalhaRestauracao(string erro){}public void IniciarOperacaoCritica(){locks++;}public void FinalizarOperacaoCritica(){locks--;}}
public class CarteiraReinPlayer {public long ReinUnidades;public void DefinirSaldoPorUnidades(long x){ReinUnidades=x;}}
public class StatusPlayer {public int xp;public CarteiraReinPlayer wallet=new CarteiraReinPlayer();public CarteiraReinPlayer ObterCarteiraRein()=>wallet;}
public class ItemPersistente {public void RegistrarConsumoPermanente(){} }
public class RecuperacaoItemCarga {GameObject go;bool active;public RecuperacaoItemCarga(Component c){go=c.gameObject;active=go.active;}public void Restaurar(){go.active=active;}}
public class SlotInventario {
 public string name="Wood";public List<XRGrabInteractable> items=new List<XRGrabInteractable>();public int failAt=-1,calls;
 public int ObterQuantidadeAtual()=>items.Count;public List<XRGrabInteractable> ObterItensParaSave()=>new List<XRGrabInteractable>(items);
 public Action CapturarRecuperacaoCarga(){var before=new List<XRGrabInteractable>(items);return ()=>items=new List<XRGrabInteractable>(before);}
 public bool ReservarUnidadeParaConsumo(out XRGrabInteractable item){item=null;if(calls++==failAt||items.Count==0)return false;item=items[items.Count-1];items.RemoveAt(items.Count-1);item.gameObject.SetActive(false);return true;}
}
public class InventarioVR {public SlotInventario[] slots;public SlotInventario[] ObterSlotsParaSave()=>slots;}
public enum EstadoMissao {ProntaParaEntregar,Concluida}
public enum TipoObjetivo {ColetarItens,MatarInimigos}
public class MissaoDados {public EstadoMissao estado=EstadoMissao.ProntaParaEntregar;public TipoObjetivo tipoObjetivo;public int quantidadeAlvo=2,recompensaExperiencia=5;public string idAlvo="Wood",recompensaRein="10";public GameObject prefabRecompensa;public Transform pontoSpawnRecompensa;}
public class UI {public void Esconder(object x){} }
public class MissionHost {
 public bool entregaEmAndamento,failReward;public MissaoDados mission=new MissaoDados();public InventarioVR inventory;
 public int missaoAtualIndex,saves;public UI uiProgresso;public Transform transform=new Transform();public static StatusPlayer player;
 private T FindFirstObjectByType<T>() where T:class=>player as T;
 private MissaoDados ObterMissaoAtual()=>mission;private InventarioVR ObterInventarioJogador()=>inventory;
 private int ObterQuantidadeItemNoInventario(string name)=>inventory.slots.Where(s=>s.name==name).Sum(s=>s.items.Count);
 private string ObterNomeItemDoSlot(SlotInventario s)=>s.name;private long ConverterReinParaUnidades(string s)=>long.Parse(s);
 private void EntregarRecompensasAoPlayer(MissaoDados m){player.xp+=m.recompensaExperiencia;player.wallet.ReinUnidades+=10;if(failReward)throw new Exception("Injected reward failure");}
 private void FecharTodosPopupsMissao(){}private void FecharObjetosExtrasAoAceitar(){}private void SalvarEstadoAtual(){saves++;}
 private GameObject Instantiate(GameObject p,Vector3 v,Quaternion q)=>new GameObject();private void Destroy(GameObject g){g.destroyed=true;}
'@
$tests=@'
}
public static class MissionMaterialTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static MissionHost New(int count){MissionHost.player=new StatusPlayer();SaveManager.Instancia.locks=0;var s=new SlotInventario();for(int i=0;i<count;i++)s.items.Add(new XRGrabInteractable {id=i,gameObject=new GameObject {component=new ItemPersistente()}});return new MissionHost {inventory=new InventarioVR {slots=new[]{s}}};}
 public static string Run(){
  var m=New(1);m.EntregarRecompensaEAvancar();Check(m.mission.estado==EstadoMissao.ProntaParaEntregar&&MissionHost.player.xp==0,"Shortfall rewarded");Check(m.inventory.slots[0].items.Count==1,"Shortfall consumed materials");
  m=New(3);var before=m.inventory.slots[0].ObterItensParaSave();m.EntregarRecompensaEAvancar();Check(m.inventory.slots[0].items.Count==1&&m.inventory.slots[0].items[0].id==0,"LIFO/exact amount");Check(before[1].gameObject.destroyed&&before[2].gameObject.destroyed&&!before[0].gameObject.destroyed,"Wrong objects destroyed");Check(MissionHost.player.xp==5&&MissionHost.player.wallet.ReinUnidades==10,"Reward missing");m.EntregarRecompensaEAvancar();Check(MissionHost.player.xp==5&&m.saves==1,"Double reward");
  m=New(3);m.inventory.slots[0].failAt=1;m.EntregarRecompensaEAvancar();Check(m.inventory.slots[0].items.Count==3&&m.inventory.slots[0].items.All(i=>i.gameObject.active&&!i.gameObject.destroyed),"Partial reservation not recovered");Check(MissionHost.player.xp==0&&m.saves==0,"Partial reservation rewarded");
  m=New(3);m.failReward=true;m.EntregarRecompensaEAvancar();Check(m.inventory.slots[0].items.Count==3&&MissionHost.player.xp==0&&MissionHost.player.wallet.ReinUnidades==0,"Reward failure not recovered");Check(m.mission.estado==EstadoMissao.ProntaParaEntregar&&SaveManager.Instancia.locks==0&&!m.entregaEmAndamento,"Failure state/lock");
  return checks+" verificacoes aprovadas: metodo real de entrega, falhas injetadas; Unity simulado.";
 }
}
'@
$code="using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEngine.XR.Interaction.Toolkit.Interactables;`n"+$stub+$method+$tests
Add-Type -TypeDefinition $code -IgnoreWarnings -WarningAction SilentlyContinue
[MissionMaterialTests]::Run()
