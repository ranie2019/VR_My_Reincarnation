$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../Assets/Script/Enemy/Slime/SlimeAnimacao.cs')
$source=[regex]::Replace($source,'(?m)^using .*?;\s*$','')
$stub=@'
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}public class SerializeField:Attribute {}public class DisallowMultipleComponent:Attribute {}public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}public class MinAttribute:Attribute {public MinAttribute(float f){}}
public class MonoBehaviour {public T GetComponent<T>() where T:class=>null;public T GetComponentInChildren<T>(bool b) where T:class=>null;}
public class AnimationClip {public float length;}
public enum AnimatorControllerParameterType {Bool}
public class AnimatorControllerParameter {public int nameHash;public AnimatorControllerParameterType type;}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float a)=>Math.Abs(a);}
public class Animator {
public bool isActiveAndEnabled=true;public object runtimeAnimatorController=new object();public float speed=1;public int plays,lastState;public Dictionary<int,bool> values=new Dictionary<int,bool>();
public AnimatorControllerParameter[] parameters;public static int StringToHash(string s)=>s.GetHashCode();public string GetLayerName(int i)=>"Base Layer";public bool HasState(int i,int h)=>true;public void Play(int h,int i,float t){plays++;lastState=h;}public void SetBool(int h,bool b){values[h]=b;}
}
}
'@
$checks=@'
public static class AnimationChecks {
static void Set(object o,string name,object v)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var animator=new Animator();var pars=new List<AnimatorControllerParameter>();foreach(var name in new[]{"Andar","Patrulha","Alerta","Ataque","Dano","Morrer"})pars.Add(new AnimatorControllerParameter{nameHash=Animator.StringToHash(name),type=AnimatorControllerParameterType.Bool});animator.parameters=pars.ToArray();var a=new SlimeAnimacao();Set(a,"animator",animator);a.AtualizarCacheParametros();
a.AnimarAtacando();a.ReiniciarAcaoConfigurada("Ataque");c(animator.plays==0&&animator.values[Animator.StringToHash("Ataque")],"Green and other consumers retain default parameter-only animation");c(a.ObterTempoMorteConfigurado()==0,"Legacy destruction delay not extended by default");
Set(a,"reiniciarAcoesPorGolpe",true);Set(a,"clipMorte",new AnimationClip{length=1.333f});a.AnimarAlerta();c(animator.lastState==Animator.StringToHash("Base Layer.Alerta")&&!animator.values[Animator.StringToHash("Ataque")],"Red alert uses actual alert state without attacking");
int before=animator.plays;a.AnimarAtacando();a.ReiniciarAcaoConfigurada("Ataque");c(animator.plays==before+2,"Repeated attack can restart the existing clip");a.AnimarTomandoDano();c(animator.lastState==Animator.StringToHash("Base Layer.Dano")&&!animator.values[Animator.StringToHash("Ataque")],"Damage interrupts attack visuals");a.AnimarMorto();c(animator.lastState==Animator.StringToHash("Base Layer.Morto")&&animator.values[Animator.StringToHash("Morrer")],"Death uses existing Morto state and Morrer parameter");c(a.ObterTempoMorteConfigurado()>1.58f,"Death waits for real clip and transition margin");animator.speed=2;c(a.ObterTempoMorteConfigurado()<1,"Death duration respects animator playback speed");
animator.isActiveAndEnabled=false;before=animator.plays;a.ReiniciarAcaoConfigurada("Ataque");c(before==animator.plays,"Inactive animator is not restarted");return n;}
}
'@
Add-Type -TypeDefinition ($stub+$source+$checks) -CompilerOptions '/nowarn:0649,0414'
Write-Output ("PASS: "+[AnimationChecks]::Run()+" verificacoes do SlimeAnimacao real; Animator e clipes simulados.")
