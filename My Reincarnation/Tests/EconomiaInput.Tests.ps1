$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($consumer in @('NPC/Ferreiro/BotaoCriarItem.cs','NPC/Sacerdote/BotaoMissao.cs','Equipamento/Botaoequipar.cs','NPC/Ferreiro/NPCPopup.cs')) {
 $text = Get-Content -Raw (Join-Path $project ('Assets/Script/' + $consumer))
 if ($text -match '\.action\.(Enable|Disable)\(' -or $text -notmatch 'UsoAcaoCompartilhada.Adquirir' -or $text -notmatch 'UsoAcaoCompartilhada.LiberarTodas') {
  throw "Consumidor ainda controla diretamente a acao compartilhada: $consumer"
 }
}
$wallet = Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/Status/CarteiraReinPlayer.cs')
$inputSource = Get-Content -Raw (Join-Path $project 'Assets/Script/NPC/Sacerdote/BotaoMissao.cs')
$inputSource = $inputSource.Substring($inputSource.IndexOf('internal static class UsoAcaoCompartilhada'))
$repair = Get-Content -Raw (Join-Path $project 'Assets/Script/NPC/Ferreiro/Reparar.cs')
$start = $repair.IndexOf('    public void TentarReparar()')
$end = $repair.IndexOf('    private void MoverItemParaSpawn', $start)
$method = $repair.Substring($start, $end - $start)
$stub = @'
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent:Attribute {}
 [AttributeUsage(AttributeTargets.Field)] public class SerializeField:Attribute {}
 public enum RuntimeInitializeLoadType { SubsystemRegistration }
 [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
 public class GameObject {public void SetActive(bool value){} }
 public class Component {}
 public class MonoBehaviour:Component {protected static T FindFirstObjectByType<T>() where T:class=>Host.status as T;}
 public static class Mathf {public static float Round(float x)=>(float)Math.Round(x);}
 public static class Debug {public static void LogError(object m,object c){}public static void LogException(Exception e,object c){} }
 public static class JsonUtility {
  public static string ToJson(object x)=>((Equipment)x).current.ToString(System.Globalization.CultureInfo.InvariantCulture);
  public static void FromJsonOverwrite(string s,object x){((Equipment)x).current=float.Parse(s,System.Globalization.CultureInfo.InvariantCulture);}
 }
}
namespace UnityEngine.InputSystem {
 public class InputAction {public bool enabled;public int disables;public void Enable(){enabled=true;}public void Disable(){enabled=false;disables++;}}
}
public class SaveManager {public static SaveManager Instancia=new SaveManager();public int locks;public void IniciarOperacaoCritica(){locks++;}public void FinalizarOperacaoCritica(){locks--;}}
public class StatusPlayer {public CarteiraReinPlayer ObterCarteiraRein()=>Host.wallet;}
public static class Host {public static StatusPlayer status=new StatusPlayer();public static CarteiraReinPlayer wallet;}
public class Equipment:UnityEngine.Component {public float current=50,max=100;}
public class RepairHost:UnityEngine.MonoBehaviour {
 public bool reparoEmAndamento,failRepair;
 public UnityEngine.GameObject itemNoSlot=new UnityEngine.GameObject();
 public UnityEngine.Component equipamentoNoSlot=new Equipment();
 public float precoPorPontoDeVida=0.1f;
 public StatusPlayer statusPlayer;
 public UnityEngine.GameObject mensagemJaEstaInteiro,mensagemSemDinheiro,mensagemSucesso;
 private void EsconderTodasMensagens(){}private void AtualizarValorNaTela(float x){}private void MoverItemParaSpawn(UnityEngine.GameObject x){}
 private bool TentarCalcularVidaFaltando(UnityEngine.Component x,out float missing){var e=x as Equipment;missing=e==null?0:Math.Max(0,e.max-e.current);return e!=null;}
 private bool TentarRepararComponente(UnityEngine.Component x){var e=(Equipment)x;e.current=e.max;if(failRepair)throw new Exception("Injected failure");return true;}
 private void InvocarAtualizacaoVisual(UnityEngine.Component x){}private void AtualizarPersistenciaDurabilidade(UnityEngine.Component x){}
'@
$tests = @'
}
public static class EconomyInputTests {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static RepairHost New(long money){Host.wallet=new CarteiraReinPlayer();Host.wallet.DefinirSaldoPorUnidades(money);SaveManager.Instancia.locks=0;return new RepairHost();}
 public static string Run(){
  var a=new UnityEngine.InputSystem.InputAction();var x=new object();var y=new object();
  UsoAcaoCompartilhada.Adquirir(a,x);UsoAcaoCompartilhada.Adquirir(a,x);UsoAcaoCompartilhada.Adquirir(a,y);
  UsoAcaoCompartilhada.Liberar(a,x);Check(a.enabled,"Other owner lost input");
  UsoAcaoCompartilhada.Liberar(a,y);Check(!a.enabled&&a.disables==1,"Last owner did not release");
  a=new UnityEngine.InputSystem.InputAction();a.Enable();UsoAcaoCompartilhada.Adquirir(a,x);UsoAcaoCompartilhada.LiberarTodas(x);Check(a.enabled,"Externally owned action disabled");
  var r=New(1000000);var e=(Equipment)r.equipamentoNoSlot;int notifications=0;
  Host.wallet.AoSaldoReinAlterado+=_=>{notifications++;r.TentarReparar();};r.TentarReparar();
  Check(Host.wallet.ReinUnidades==500000&&notifications==1,"Debit/reentrant charge");Check(e.current==100&&r.itemNoSlot==null,"Repair did not finish");Check(SaveManager.Instancia.locks==0,"Critical lock leaked");
  r=New(1);e=(Equipment)r.equipamentoNoSlot;r.TentarReparar();Check(e.current==50&&Host.wallet.ReinUnidades==1,"Insufficient funds changed state");
  r=New(1000000);e=(Equipment)r.equipamentoNoSlot;r.failRepair=true;r.TentarReparar();Check(e.current==50&&Host.wallet.ReinUnidades==1000000,"Failure did not restore/refund");Check(!r.reparoEmAndamento&&SaveManager.Instancia.locks==0,"Failure left operation locked");
  r=New(1000000);e=(Equipment)r.equipamentoNoSlot;e.current=100;r.TentarReparar();Check(Host.wallet.ReinUnidades==1000000,"Full item charged");
  r=New(1000000);Host.wallet.AoSaldoReinAlterado+=_=>throw new Exception("Broken UI subscriber");r.TentarReparar();Check(Host.wallet.ReinUnidades==500000,"UI subscriber broke confirmed debit");
  return checks+" verificacoes aprovadas: carteira/input reais e metodo real de reparo; Unity simulado.";
 }
}
'@
$wallet = $wallet -replace '(?m)^using .*;\s*',''
$inputSource = $inputSource -replace '(?m)^using .*;\s*',''
$code = "using System;using System.Collections.Generic;using System.Globalization;using UnityEngine;using UnityEngine.InputSystem;`n" + $stub + $method + $tests + $wallet + $inputSource
Add-Type -TypeDefinition $code -IgnoreWarnings -WarningAction SilentlyContinue
[EconomyInputTests]::Run()
