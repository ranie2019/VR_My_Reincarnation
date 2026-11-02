$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/EquiparElmo.cs')
$a=$source.IndexOf('    private void CapturarPoseOriginalPopup()')
$b=$source.IndexOf('    private void AplicarEscalaMundoComoLocal(', $a)
$methods=$source.Substring($a,$b-$a)
$code=@'
using System;
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3(0,0,0);public static Vector3 one=>new Vector3(1,1,1);}
public struct Quaternion {public float x,y,z,w;public static Quaternion identity=>new Quaternion{w=1};}
public enum HideFlags {DontSave}
public static class Mathf {public static float Abs(float x)=>Math.Abs(x);}
public class Transform {
 public Transform parent;public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
 public Vector3 lossyScale=>parent==null?localScale:new Vector3(parent.lossyScale.x*localScale.x,parent.lossyScale.y*localScale.y,parent.lossyScale.z*localScale.z);
 public void SetParent(Transform p,bool keep){parent=p;}
 public void SetPositionAndRotation(Vector3 p,Quaternion r){localPosition=p;localRotation=r;}
}
public class BotaoEquipar {public object owner;public void DefinirElmoDoPopup(object elmo){owner=elmo;}}
public struct Scene {public bool isLoaded=>true;public bool IsValid()=>true;}
namespace UnityEngine.SceneManagement {public static class SceneManager {public static void MoveGameObjectToScene(GameObject go,Scene scene){}}}
public class GameObject {public Scene scene=new Scene();public BotaoEquipar[] buttons=new[]{new BotaoEquipar()}; public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>buttons as T[];public Transform transform=new Transform();public bool activeSelf=true;public HideFlags hideFlags;public GameObject(string n=""){}public void SetActive(bool active){activeSelf=active;}}
public class HelmetPopupScaleTests {
 private Transform paiOriginalPopup;private Vector3 posicaoOriginalPopup,escalaOriginalPopup,escalaMundoOriginalPopup;
 private Quaternion rotacaoOriginalPopup;private bool posePopupCapturada,popupIsolado;
 private GameObject gameObject=new GameObject();
 private GameObject raizPopupSemEscala,popupInfo;private Transform transform=new Transform();private string name="helmet";
'@+$methods+@'
 static bool Eq(Vector3 a,Vector3 b)=>Math.Abs(a.x-b.x)<.00001&&Math.Abs(a.y-b.y)<.00001&&Math.Abs(a.z-b.z)<.00001;
 public static int Run(){int checks=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);checks++;};
  var h=new HelmetPopupScaleTests();h.transform.localScale=new Vector3(1.6097145f,1.2402285f,1.2083441f);
  h.popupInfo=new GameObject();h.popupInfo.transform.parent=h.transform;h.popupInfo.transform.localScale=new Vector3(.3f,.3f,.3f);h.popupInfo.transform.localPosition=new Vector3(1,2,3);
  var originalScale=h.popupInfo.transform.localScale;var originalPos=h.popupInfo.transform.localPosition;var worldScale=h.popupInfo.transform.lossyScale;
  h.PrepararPopupSemDistorcao();var firstRoot=h.raizPopupSemEscala;
  c(h.popupInfo.buttons[0].owner==h,"Button must retain explicit helmet owner before delayed Awake");
  c(h.popupInfo.transform.parent==firstRoot.transform&&firstRoot.transform.parent==null,"Popup must leave scaled helmet hierarchy");
  c(Eq(firstRoot.transform.localScale,Vector3.one)&&Eq(h.popupInfo.transform.localScale,worldScale),"Preserve configured world dimensions under unit root");
  h.PrepararPopupSemDistorcao();c(h.raizPopupSemEscala==firstRoot,"Repeated updates must reuse root");
  h.RecolherPopupAoElmo();c(!h.popupInfo.activeSelf&&h.popupInfo.transform.parent==h.transform,"Closing must hide and restore parent");
  c(Eq(h.popupInfo.transform.localScale,originalScale)&&Eq(h.popupInfo.transform.localPosition,originalPos),"Closing must restore original local pose exactly");
  h.PrepararPopupSemDistorcao();c(h.raizPopupSemEscala==firstRoot&&Eq(h.popupInfo.transform.localScale,worldScale),"Reopen must not accumulate scale");
  h.RecolherPopupAoElmo();h.RecolherPopupAoElmo();c(Eq(h.transform.localScale,new Vector3(1.6097145f,1.2402285f,1.2083441f)),"Model scale must remain unchanged");
  // Numerical regression: columns of S*R become non-orthogonal; R*S columns remain orthogonal.
  double x=1.6097145,y=1.2402285,cos=Math.Cos(Math.PI/4),sin=Math.Sin(Math.PI/4);
  double oldDot=(x*cos)*(-x*sin)+(y*sin)*(y*cos);
  double isolatedDot=(cos*x)*(-sin*y)+(sin*x)*(cos*y);
  c(Math.Abs(oldDot)>.1&&Math.Abs(isolatedDot)<1e-10,"Unit parent removes roll-dependent shear");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[HelmetPopupScaleTests]::Run()+" verificacoes de isolamento/limpeza e regressao numerica de torcao.")
