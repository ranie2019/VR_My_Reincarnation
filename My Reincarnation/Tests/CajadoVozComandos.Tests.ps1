$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $root 'Assets/Script/Armas/ComandosMagiaVoz.cs')
$stub=@'
using System;using System.Collections.Generic;using System.Reflection;
namespace UnityEngine {
 public class MonoBehaviour {public bool isActiveAndEnabled=true;public T GetComponent<T>() where T:class=>null;}
 public class SerializeField:Attribute {} public class DisallowMultipleComponent:Attribute {}
 public class Header:Attribute {public Header(string s){}} public class Tooltip:Attribute {public Tooltip(string s){}}
 public class Min:Attribute {public Min(float x){}} public class RangeAttribute:Attribute {public RangeAttribute(float x,float y){}}
 public class TextArea:Attribute {public TextArea(int x,int y){}}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);}
 public static class Time {public static float time;}
 public static class Debug {public static void LogWarning(string s,object o){}}
}
public class Evento {public List<Action<string>> listeners=new List<Action<string>>();public void AddListener(Action<string> a){listeners.Add(a);}public void RemoveListener(Action<string> a){listeners.RemoveAll(x=>x==a);}public void Invoke(string s){foreach(var a in listeners.ToArray())a(s);}}
public class CajadoMagicoVoz {public bool isActiveAndEnabled=true;public int IdSessaoVozAtual=1;public Evento AoComandoVozRecebido=new Evento(),AoTextoParcialVozRecebido=new Evento();}
public class CajadoMagico {public bool isActiveAndEnabled=true,CajadoSegurado=true,EstaDesenhando,success=true,PreparacaoBloqueadaPorFaltaDeMana;public object BolaDeFogoPreparadaAtual;public string StatusPreparacaoMagia;public int calls;public bool TentarPrepararBolaDeFogoPorVoz(){calls++;return success;}}
'@
$source=$source -replace '(?m)^using System;\r?\n',''
$source=$source -replace '(?m)^using System.Text;\r?\n',''
$source=$source -replace '(?m)^using UnityEngine;\r?\n',''
$code='using System.Text;using UnityEngine;'+$stub+$source+@'
public static class VoiceTests {
 static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
 static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var h=new ComandosMagiaVoz();var magic=new CajadoMagico();var voice=new CajadoMagicoVoz();Set(h,"cajadoMagico",magic);Set(h,"cajadoMagicoVoz",voice);Call(h,"OnEnable");Call(h,"OnEnable");c(voice.AoComandoVozRecebido.listeners.Count==1,"Repeated enable duplicates listeners");
 voice.AoTextoParcialVozRecebido.Invoke("fireball");c(magic.calls==1,"Partial command failed");Time.time=3;voice.AoComandoVozRecebido.Invoke("bola de fogo");c(magic.calls==1,"Late translated final duplicated same speech");voice.IdSessaoVozAtual++;voice.AoComandoVozRecebido.Invoke("bola de fogo");c(magic.calls==2,"New speech after cooldown blocked");
 voice.IdSessaoVozAtual++;voice.AoComandoVozRecebido.Invoke("cast fireball");c(magic.calls==2,"Alias bypassed cooldown");Time.time=5;magic.success=false;voice.AoComandoVozRecebido.Invoke("fireball");c(magic.calls==3,"Failed preparation not attempted");magic.success=true;voice.AoComandoVozRecebido.Invoke("fireball");c(magic.calls==4,"Failure consumed accepted-command session");
 h.isActiveAndEnabled=false;Time.time=8;voice.IdSessaoVozAtual++;h.ProcessarComandoVoz("fireball");c(magic.calls==4,"Disabled interpreter cast magic");h.isActiveAndEnabled=true;magic.isActiveAndEnabled=false;h.ProcessarComandoVoz("fireball");c(magic.calls==4,"Disabled magic component called");magic.isActiveAndEnabled=true;voice.isActiveAndEnabled=false;h.ProcessarComandoVoz("fireball");c(magic.calls==4,"Disabled voice component accepted command");voice.isActiveAndEnabled=true;
 magic.CajadoSegurado=false;h.ProcessarComandoVoz("fireball");c(magic.calls==4,"Unheld staff cast");magic.CajadoSegurado=true;magic.EstaDesenhando=true;h.ProcessarComandoVoz("fireball");c(magic.calls==4,"Voice interfered with drawing");magic.EstaDesenhando=false;
 var changed=new CajadoMagicoVoz();Set(h,"cajadoMagicoVoz",changed);Call(h,"OnDisable");c(voice.AoComandoVozRecebido.listeners.Count==0&&voice.AoTextoParcialVozRecebido.listeners.Count==0,"Listener remained on old source");Call(h,"OnEnable");c(changed.AoComandoVozRecebido.listeners.Count==1,"New source not bound");Call(h,"OnDisable");c(changed.AoComandoVozRecebido.listeners.Count==0,"Disable did not unsubscribe");var match=typeof(ComandosMagiaVoz).GetMethod("FraseContemComTolerancia",BindingFlags.Instance|BindingFlags.NonPublic);c(!(bool)match.Invoke(h,new object[]{"bola longe sem nenhum fogo","bola fogo"}),"Unrelated distant words formed command");c((bool)match.Invoke(h,new object[]{"por favor bola de fogo agora","bola de fogo"}),"Contiguous command in sentence rejected");Set(h,"usarToleranciaFonica",false);c(!(bool)match.Invoke(h,new object[]{"fireballoon","fireball"}),"Substring inside word accepted without phonetic tolerance");return n;}
}
'@
Add-Type -TypeDefinition $code -CompilerOptions /nowarn:0414,0649
Write-Output ('PASS: '+[VoiceTests]::Run()+' verificacoes do interpretador real; SDK/microfone simulados.')
$voiceSource=Get-Content -Raw (Join-Path $root 'Assets/Script/Armas/CajadoMagicoVoz.cs')
if($voiceSource -match 'VoiceEvents\.On(Partial|Full)Transcription\.AddListener'){throw 'Global transcription duplicates request events'}
if(!$voiceSource.Contains('eventos.OnFullTranscription.AddListener') -or !$voiceSource.Contains('eventos.OnPartialTranscription.AddListener')){throw 'Session request transcription disconnected'}
Write-Output 'PASS: uma origem de transcricao por requisicao, com callbacks de sessao preservados.'

