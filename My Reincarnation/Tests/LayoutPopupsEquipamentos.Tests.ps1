$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/EquiparManoplas.cs')
$layout = $source.Substring($source.IndexOf('internal static class LayoutPopupsEquipamentos'))
$stubs = @'
using System;
using System.Collections.Generic;
public enum RuntimeInitializeLoadType { SubsystemRegistration }
public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
public struct Vector3 {
 public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}
 public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
 public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
}
public struct Quaternion {public static Quaternion Euler(Vector3 v)=>new Quaternion();public static Quaternion operator *(Quaternion a,Quaternion b)=>a;}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);}
public class Transform {public Vector3 position; public Quaternion rotation; public Vector3 right=new Vector3(1,0,0);public Vector3 TransformPoint(Vector3 p)=>position+p;}
public class Camera {public Transform transform=new Transform();}
public class RectTransform {
 public Transform owner;public Vector3 right;public float min,max;
 public void GetWorldCorners(Vector3[] corners){corners[0]=corners[1]=owner.position+right*min;corners[2]=corners[3]=owner.position+right*max;}
}
namespace UnityEngine.UI {public class Graphic {public bool isActiveAndEnabled=true;public RectTransform rectTransform;}}
public class GameObject {
 public bool activeInHierarchy=true;public Transform transform=new Transform();public UnityEngine.UI.Graphic[] graphics;
 public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>graphics as T[];
}
'@
$assertions = @'
public static class LayoutChecks {
 static int count;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
 static GameObject Panel(Camera c,float min,float max){var g=new GameObject();g.graphics=new[]{new UnityEngine.UI.Graphic{rectTransform=new RectTransform{owner=g.transform,right=c.transform.right,min=min,max=max}}};return g;}
 static float Edge(GameObject g,Camera c,bool upper){var r=g.graphics[0].rectTransform;return Vector3.Dot(g.transform.position-c.transform.position,c.transform.right)+(upper?r.max:r.min);}
 static void Register(GameObject g,Camera c,float spacing=.45f){LayoutPopupsEquipamentos.Atualizar(g,c,new Vector3(0,0,1.5f),new Vector3(),spacing);}
 static void Clear(GameObject g){g.activeInHierarchy=false;LayoutPopupsEquipamentos.Remover(g);}
 public static int Run(){
  var cam=new Camera();var helmet=Panel(cam,-.5f,.5f);var chest=Panel(cam,-.7f,.7f);
  Register(helmet,cam);Check(Math.Abs(Edge(helmet,cam,false)+Edge(helmet,cam,true))<.0001,"Single popup centered");
  Register(chest,cam);Check(Edge(chest,cam,false)-Edge(helmet,cam,true)>=.0299f,"Wide helmet and chest separated by edges");
  Check(Math.Abs(Edge(helmet,cam,false)+Edge(chest,cam,true))<.0001,"Whole group centered");
  var glove=Panel(cam,-.1f,.3f);Register(glove,cam);
  Check(Edge(glove,cam,false)-Edge(chest,cam,true)>=.0299f,"Third type separated including asymmetric pivot");
  Check(Math.Abs(Edge(helmet,cam,false)+Edge(glove,cam,true))<.0001,"Three popups centered");
  float first=helmet.transform.position.x;for(int i=0;i<50;i++){Register(helmet,cam);Register(chest,cam);Register(glove,cam);}
  Check(Math.Abs(first-helmet.transform.position.x)<.0001,"No accumulated drift on repeated updates");
  Clear(chest);Clear(helmet);Check(Math.Abs(Edge(glove,cam,false)+Edge(glove,cam,true))<.0001,"Closing neighbors recenters remaining visual");Clear(glove);
  var otherCam=new Camera();var a=Panel(cam,-.1f,.1f);var b=Panel(otherCam,-.5f,.5f);Register(a,cam);Register(b,otherCam);
  Check(Math.Abs(a.transform.position.x)<.0001&&Math.Abs(b.transform.position.x)<.0001,"Different cameras independent");Clear(a);Clear(b);
  cam.transform.right=new Vector3(.6f,.8f,0);a=Panel(cam,-.5f,.5f);b=Panel(cam,-.7f,.7f);Register(a,cam);Register(b,cam);
  Check(Edge(b,cam,false)-Edge(a,cam,true)>=.0299f,"Separation follows tilted camera right axis");Clear(a);Clear(b);
  cam.transform.right=new Vector3(1,0,0);a=Panel(cam,-.1f,.1f);b=Panel(cam,-.1f,.1f);Register(a,cam,2);Register(b,cam,2);
  Check(Math.Abs(b.transform.position.x-a.transform.position.x-2)<.0001,"Configured larger center distance preserved");Clear(a);Clear(b);
  a=Panel(cam,-.1f,.1f);a.graphics=new[]{a.graphics[0],new UnityEngine.UI.Graphic{isActiveAndEnabled=false,rectTransform=new RectTransform{owner=a.transform,right=cam.transform.right,min=-100,max=100}}};
  b=Panel(cam,-.1f,.1f);Register(a,cam);Register(b,cam);Check(Math.Abs(b.transform.position.x-a.transform.position.x-.45f)<.0001,"Disabled graphics do not inflate width");Clear(a);Clear(b);
  return count;
 }
}
'@
Add-Type -TypeDefinition ($stubs + $layout + $assertions)
$checks = [LayoutChecks]::Run()
Write-Output "PASS: $checks verificacoes do organizador real em host simulado (larguras, tipos mistos, pivots, camera inclinada, fechamento e estabilidade)."
