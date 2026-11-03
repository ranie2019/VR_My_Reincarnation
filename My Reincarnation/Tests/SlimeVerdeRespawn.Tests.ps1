$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Respawn/RespawnMonstro.cs')
$source=[regex]::Replace($source,'(?m)^using .*?;\s*$','')
$stub=@'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
public class DisallowMultipleComponent:Attribute {} public class SerializeField:Attribute {} public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>new Vector3(0,-1,0);public static Vector3 right=>new Vector3(1,0,0);public static Vector3 forward=>new Vector3(0,0,1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);}
public struct Quaternion {public static Quaternion identity=>new Quaternion();} public class Transform {public Vector3 position;public Quaternion rotation;}
public class GameObject {public Transform transform=new Transform();public SlimeIA slime;public T GetComponent<T>() where T:class=>slime as T;}
public class Collider {public T GetComponentInParent<T>() where T:class=>null;}
public struct RaycastHit {public Collider collider;public float distance;public Vector3 point,normal;}
public enum QueryTriggerInteraction {Ignore}
public static class Physics {public static bool blocked,noGround;public static float capsuleRadius;public static int RaycastNonAlloc(Vector3 o,Vector3 d,RaycastHit[] hits,float length,int mask,QueryTriggerInteraction q){if(noGround)return 0;hits[0]=new RaycastHit{collider=new Collider(),normal=Vector3.up,point=new Vector3(o.x,0,o.z),distance=o.y};return 1;}public static bool CheckSphere(Vector3 p,float r,int m,QueryTriggerInteraction q)=>blocked;public static bool CheckCapsule(Vector3 a,Vector3 b,float r,int m,QueryTriggerInteraction q){capsuleRadius=r;return blocked;}}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
public static class Time {public static float time;}
public class WaitForSeconds {public WaitForSeconds(float t){}}
public class Coroutine {public IEnumerator run;public bool stopped;}
public class MonoBehaviour {public bool enabled=true,isActiveAndEnabled=true;public static List<GameObject> born=new List<GameObject>();public List<Coroutine> routines=new List<Coroutine>();public Coroutine StartCoroutine(IEnumerator e){var c=new Coroutine{run=e};routines.Add(c);e.MoveNext();return c;}public void StopCoroutine(Coroutine c){c.stopped=true;}public GameObject Instantiate(GameObject g,Vector3 p,Quaternion r){var n=new GameObject{slime=new SlimeIA()};n.transform.position=p;n.transform.rotation=r;born.Add(n);return n;}}
public static class Debug {public static int errors;public static void LogError(string s,object o){errors++;}}
}
public class SlimeIA {public Transform[] patrol;public int resets;public void DefinirPontosPatrulha(Transform[] t){patrol=t;}public void ReiniciarAposRespawn(){resets++;}}
public class StatusPlayer {}
public static class SlimeMovimento {public static bool SoloTransitavel(Vector3 n,float a)=>n.y>.7f;}
'@
$checks=@'
public static class RespawnChecks {
static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
static int Pending(RespawnMonstro m)=>((IDictionary)typeof(RespawnMonstro).GetField("pendentesVerdes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(m)).Count;
public static int Run(){int n=0;Action<bool,string> check=(ok,s)=>{if(!ok)throw new Exception(s);n++;};var m=new RespawnMonstro();var prefab=new GameObject();var cfg=new RespawnMonstro.ConfiguracaoRespawnMonstro{idMonstro="SlimeVerde",prefabRespawn=prefab,tempoRespawn=15,offsetRespawn=Vector3.up};typeof(RespawnMonstro).GetField("configuracoesMonstros",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,new[]{cfg});Call(m,"Awake");Call(m,"OnEnable");
var patrol=new[]{new Transform()};
for(int cycle=0;cycle<3;cycle++){
float start=Time.time;m.AgendarRespawnVerde("SlimeVerde",new Vector3(10,0,20),Quaternion.identity,patrol,cycle);m.AgendarRespawnVerde("SlimeVerde",new Vector3(10,0,20),Quaternion.identity,patrol,cycle);check(Pending(m)==1,"One pending replacement per dead instance");var routine=m.routines[m.routines.Count-1];Time.time=start+14;routine.run.MoveNext();check(MonoBehaviour.born.Count==cycle,"Never spawns before configured delay");Time.time=start+15;routine.run.MoveNext();check(MonoBehaviour.born.Count==cycle+1&&Pending(m)==0,"Exactly one spawn completes each cycle");var born=MonoBehaviour.born[cycle];check(born.transform.position.x==10&&born.transform.position.y==1&&born.transform.position.z==20,"Death position plus offset preserved");check(born.slime.resets==1&&born.slime.patrol[0]==patrol[0],"Fresh life and original manual patrol restored");}
Physics.blocked=true;m.AgendarRespawnVerde("SlimeVerde",new Vector3(10,0,20),Quaternion.identity,patrol,7);var pending=m.routines[m.routines.Count-1];Time.time+=15;pending.run.MoveNext();check(MonoBehaviour.born.Count==3&&Pending(m)==1,"Occupied location keeps pending without instantiating duplicate corpses");
m.isActiveAndEnabled=false;Call(m,"OnDisable");check(RespawnMonstro.Instancia==null&&pending.stopped&&Pending(m)==1,"Disable retains pending descriptor and stops old coroutine");m.isActiveAndEnabled=true;Call(m,"OnEnable");check(RespawnMonstro.Instancia==m,"Reenable restores manager registration");Physics.blocked=false;m.routines[m.routines.Count-1].run.MoveNext();check(MonoBehaviour.born.Count==4&&Pending(m)==0,"Reenabled pending spawn completes once");
Physics.noGround=true;m.AgendarRespawnVerde("SlimeVerde",new Vector3(10,0,20),Quaternion.identity,patrol,8);Time.time+=15;m.routines[m.routines.Count-1].run.MoveNext();check(MonoBehaviour.born.Count==4,"No spawning without safe ground");return n;}
}
'@
Add-Type -TypeDefinition ($stub+$source+$checks) -CompilerOptions '/nowarn:0649,0414'
Write-Output ("PASS: "+[RespawnChecks]::Run()+" verificacoes do RespawnMonstro real, incluindo tres ciclos. Relogio, coroutines e fisica simulados; Unity/Quest nao executados.")
