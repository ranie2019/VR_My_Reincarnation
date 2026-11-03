$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../Assets/Script/Enemy/Slime/SlimeDeteccao.cs')
$source=[regex]::Replace($source,'(?m)^using .*?;\s*$','')
$stub=@'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
public class DisallowMultipleComponent:Attribute {}public class SerializeField:Attribute {}public class MinAttribute:Attribute {public MinAttribute(float f){}}
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 forward=>new Vector3(0,0,1);public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector3 normalized=>new Vector3(x/magnitude,y/magnitude,z/magnitude);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static float Angle(Vector3 a,Vector3 b)=>(float)(Math.Acos(Math.Clamp((a.x*b.x+a.y*b.y+a.z*b.z)/(a.magnitude*b.magnitude),-1,1))*180/Math.PI);}
public class GameObject {public Transform transform;public AvatarBody avatar;public Camera camera;public bool activeInHierarchy=true;public GameObject(){transform=new Transform{gameObject=this};}public static GameObject[] players=new GameObject[0];public static int searches;public static GameObject[] FindGameObjectsWithTag(string tag){searches++;return players;}public T GetComponent<T>() where T:class=>avatar as T;public T GetComponentInChildren<T>() where T:class=>camera as T;}
public class Transform {public Vector3 position;public Transform parent;public GameObject gameObject;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public T GetComponentInChildren<T>() where T:class=>gameObject.GetComponentInChildren<T>();public bool IsChildOf(Transform t){for(var p=parent;p!=null;p=p.parent)if(p==t)return true;return false;}}
public class MonoBehaviour {public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;}
public class Camera {public Transform transform=new GameObject().transform;}
public class Collider {public Transform transform=new GameObject().transform;}
public struct RaycastHit {public Collider collider;}
public enum QueryTriggerInteraction {Ignore}public struct LayerMask {public int value;public static implicit operator LayerMask(int n)=>new LayerMask{value=n};}
public static class Time {public static float time;}
public class UnityException:Exception {}
public static class Physics {public static Collider[] blockers=new Collider[0];public static int calls;public static int RaycastNonAlloc(Vector3 p,Vector3 d,RaycastHit[] hits,float distance,LayerMask mask,QueryTriggerInteraction q){calls++;for(int i=0;i<Math.Min(hits.Length,blockers.Length);i++)hits[i]=new RaycastHit{collider=blockers[i]};return Math.Min(hits.Length,blockers.Length);}}
}
public class AvatarBody {public Transform corpoAvatar;}
'@
$checks=@'
public static class VisionChecks {
public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var d=new SlimeDeteccao();var p=new GameObject();var body=new GameObject();body.transform.position=new Vector3(0,1,3);p.avatar=new AvatarBody{corpoAvatar=body.transform};var origin=new Vector3(0,1,0);
c(d.PosicaoJogador(p.transform).z==3,"Uses tracked avatar body instead of stationary XR origin");
c(d.Visivel(p.transform,origin,Vector3.forward,8,120,65),"Clear visible player acquired");int queries=Physics.calls;d.Visivel(p.transform,origin,Vector3.forward,8,120,65);c(queries==Physics.calls,"Repeated frame checks share cached visibility");
Time.time=1;Physics.blockers=new[]{new Collider()};c(!d.Visivel(p.transform,origin,Vector3.forward,8,120,65),"Solid obstacle blocks vision");
Time.time=2;var own=new Collider();own.transform.parent=d.transform;var playerCollider=new Collider();playerCollider.transform.parent=p.transform;Physics.blockers=new[]{own,playerCollider};c(d.Visivel(p.transform,origin,Vector3.forward,8,120,65),"Self and target colliders do not hide player");
body.transform.position=new Vector3(0,1,10);c(!d.Visivel(p.transform,origin,Vector3.forward,8,120,65),"Tracked movement outside range invalidates cached visibility");
body.transform.position=new Vector3(0,1,-3);c(!d.Visivel(p.transform,origin,Vector3.forward,8,120,65),"Player behind field of view rejected");
body.transform.position=new Vector3(0,1,3);Physics.blockers=new Collider[0];GameObject.players=new[]{p};Time.time=4;d.ProcurarJogador("Player",origin,Vector3.forward,8,120,65);var d2=new SlimeDeteccao();d2.ProcurarJogador("Player",origin,Vector3.forward,8,120,65);c(GameObject.searches==1,"Multiple green slimes share player registry query");
var fallback=new GameObject();fallback.camera=new Camera();fallback.camera.transform.position=new Vector3(2,1,2);c(d.PosicaoJogador(fallback.transform).x==2,"Camera position fallback without AvatarBody");return n;}
}
'@
Add-Type -TypeDefinition ($stub+$source+$checks) -CompilerOptions '/nowarn:0649,0414'
Write-Output ("PASS: "+[VisionChecks]::Run()+" verificacoes da deteccao real; colliders, relogio e consultas fisicas simulados.")
