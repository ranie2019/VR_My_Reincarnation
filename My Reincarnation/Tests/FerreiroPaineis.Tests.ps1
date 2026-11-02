$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/NPC/Ferreiro/FerreiroUI.cs')
$source=[regex]::Replace($source,'(?m)^using .*?;\s*$','')
$stub=@'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
 public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
 public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
 public class SerializeField:Attribute {}
 public class Coroutine {public IEnumerator routine;}
 public class GameObject {public bool activeSelf;public GameObject parent;public bool activeInHierarchy=>activeSelf&&(parent==null||parent.activeInHierarchy);public void SetActive(bool a){activeSelf=a;}}
 public class MonoBehaviour {public bool isActiveAndEnabled=true;public List<object> children=new List<object>();public T[] GetComponentsInChildren<T>(bool inactive)=>children.OfType<T>().ToArray();public Coroutine StartCoroutine(IEnumerator e)=>new Coroutine{routine=e};public void StopCoroutine(Coroutine c){}}
 public static class Time {public static float time;}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
 public static class Debug {public static void LogWarning(string s){}}
}
namespace UnityEngine.XR.Interaction.Toolkit.Interactables {public class XRBaseInteractable {public bool isHovered;}}
namespace UnityEngine.XR.Interaction.Toolkit.Interactors {public class XRSocketInteractor {public bool hasSelection;}}
public class GerenciadorMissoes {public int opens,closes;public void NotificarHoverNPC(){opens++;}public void FecharPopupAtual(){closes++;}}
'@
$checks=@'
public static class PanelChecks {
 static int checks;
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);checks++;}
 static void Set(FerreiroUI h,string n,object v)=>typeof(FerreiroUI).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(h,v);
 static object Call(FerreiroUI h,string n)=>typeof(FerreiroUI).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(h,null);
 public static int Run(){
  var h=new FerreiroUI();var greeting=new GameObject();var categories=new GameObject();var create=new GameObject{parent=categories};var repair=new GameObject{parent=categories};var upgrade=new GameObject{parent=categories};var mission=new GameObject{parent=categories};var manager=new GerenciadorMissoes();
  Set(h,"popupSaudacao",greeting);Set(h,"botoesCategoria",categories);Set(h,"painelCriar",create);Set(h,"painelReparar",repair);Set(h,"painelMelhorar",upgrade);Set(h,"painelMissao",mission);Set(h,"gerenciadorMissoes",manager);
  Call(h,"Awake");h.AbrirPopupSaudacao();Check(greeting.activeSelf&&!categories.activeSelf,"Greeting alone opens");
  h.OnClickYes();Check(!greeting.activeSelf&&categories.activeSelf&&!create.activeSelf&&!repair.activeSelf&&!upgrade.activeSelf&&!mission.activeSelf,"Yes opens categories without stale content");
  h.OnClickCriar();h.OnClickReparar();Check(!create.activeSelf&&repair.activeSelf,"Switching categories closes previous panel");
  h.OnClickMissao();Check(mission.activeSelf&&!repair.activeSelf&&manager.opens==1,"Mission requires category action");
  int before=manager.closes;h.OnClickCriar();Check(!mission.activeSelf&&manager.closes>before,"Leaving mission closes its managed popups");
  h.OnClickFechar();Check(!categories.activeSelf&&!greeting.activeSelf&&!create.activeSelf&&!mission.activeSelf,"Close resets all panels");
  h.OnClickYes();h.OnClickMissao();categories.SetActive(false);h.AbrirPopupSaudacao();Check(greeting.activeSelf&&!categories.activeSelf,"External closure does not leave stale current panel blocking greeting");
  Time.time=0;h.OnClickYes();h.OnClickCriar();var timer=(IEnumerator)Call(h,"RotinaTimerInatividade");
  var hover=new UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable{isHovered=true};h.children.Add(hover);Time.time=29;Call(h,"Update");Time.time=40;
  Check(timer.MoveNext()&&create.activeSelf,"Hover refreshes inactivity clock instead of unconditional 30 second close");
  hover.isHovered=false;Time.time=60;Check(!timer.MoveNext()&&!categories.activeSelf,"True inactivity closes panels");
  Time.time=70;h.OnClickYes();h.OnClickReparar();timer=(IEnumerator)Call(h,"RotinaTimerInatividade");var socket=new UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor{hasSelection=true};h.children.Add(socket);Time.time=99;Call(h,"Update");Time.time=110;
  Check(timer.MoveNext()&&repair.activeSelf,"Selected equipment keeps service open");
  socket.hasSelection=false;Time.time=130;Check(!timer.MoveNext()&&!repair.activeSelf,"After removal normal inactivity resumes");
  h.OnClickYes();h.OnClickReparar();h.OnMissaoAceita();Check(!categories.activeSelf&&!repair.activeSelf,"Mission acceptance reset preserved");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($stub+$source+$checks) -CompilerOptions '/nowarn:0649,0414'
$total=[PanelChecks]::Run()
$prefab=Get-Content -Raw (Join-Path $project 'Assets/NPC/Ferreiro/Prefab/Ferreiro.prefab')
$blocks=[regex]::Split($prefab,'(?m)(?=^--- !u!)')
foreach($block in $blocks){
 if($block -match '^--- !u!114 &(\d+)' -and $block -match 'UnityEngine.XR.Interaction.Toolkit'){
  $componentId=[regex]::Match($block,'^--- !u!114 &(\d+)').Groups[1].Value
  if($block.Contains("m_ParentInteractableObject: {fileID: $componentId}")){throw 'Self-parenting XR interactable remains.'}
 }
}
if($prefab -match 'Assembly-CSharp::SlotInventario'){throw 'Blacksmith service slot still controlled as player inventory.'}
foreach($id in @('3038800295000737323','6517313145967113128')){
 $block=$blocks | Where-Object {$_ -match "^--- !u!114 &$id\b"}
 if($block -notmatch 'm_Enabled: 0'){throw 'Unimplemented upgrade confirmation remains enabled.'}
}
Write-Output "PASS: $total verificacoes da navegacao e inatividade reais em host simulado; slots de servico, confirmacao pendente e parent XR validados no prefab."

