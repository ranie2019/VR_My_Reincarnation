$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/EquiparElmo.cs')
$a=$source.IndexOf('    private readonly List<Renderer> renderersOcultacaoLocal')
$b=$source.IndexOf('    public bool JaEquipado', $a)
$fields=$source.Substring($a,$b-$a)
$a=$source.IndexOf('    private void OnDestroy()')
$b=$source.IndexOf('    private bool EstaArmazenadoNoInventario()', $a)
$methods=$source.Substring($a,$b-$a)
$stub=@'
namespace UnityEngine {
 public class Component {
  public GameObject gameObject;public Transform transform=>gameObject.transform;
  public T GetComponent<T>() where T:class=>gameObject.parts.OfType<T>().FirstOrDefault();
  public T GetComponentInParent<T>(bool inactive) where T:class {for(var t=transform;t!=null;t=t.parent){var c=t.GetComponent<T>();if(c!=null)return c;}return null;}
 }
 public class Transform:Component {public Transform parent;}
 public class GameObject {
  public bool activeInHierarchy=true;public int layer=3;public Transform transform;public List<Component> parts=new List<Component>();
  public GameObject(){transform=new Transform {gameObject=this};}
  public T Add<T>() where T:Component,new(){var c=new T {gameObject=this};parts.Add(c);return c;}
 }
 public class Renderer:Component {public bool enabled=true,forceRenderingOff;public object material=new object();}
 public class Camera:Component {
  public int cullingMask=-1;public static event Action<Camera> onPreCull,onPostRender;
  public static void Begin(Camera c){onPreCull?.Invoke(c);}public static void End(Camera c){onPostRender?.Invoke(c);}
 }
 public static class Debug {public static void LogWarning(object a,object b){} }
}
namespace UnityEngine.Rendering {
 public struct ScriptableRenderContext {}
 public static class GraphicsSettings {public static object currentRenderPipeline=new object();}
 public static class RenderPipelineManager {
  public static event Action<ScriptableRenderContext,Camera> beginCameraRendering,endCameraRendering;
  public static event Action<ScriptableRenderContext,List<Camera>> endContextRendering;
  public static int Subscribers=>beginCameraRendering?.GetInvocationList().Length??0;
  public static void Begin(Camera c){beginCameraRendering?.Invoke(new ScriptableRenderContext(),c);}
  public static void End(Camera c){endCameraRendering?.Invoke(new ScriptableRenderContext(),c);}
  public static void Finish(){endContextRendering?.Invoke(new ScriptableRenderContext(),new List<Camera>());}
 }
}
namespace Unity.XR.CoreUtils {public class XROrigin:Component {public Camera Camera;}}
public class AvatarBody:Component {public Transform cameraCabeca;}
public class HelmetHost:Component {
 private GameObject raizPopupSemEscala;
 private void Destroy(GameObject go){}
 public bool jaEquipado,armazenado,ocultarParaCameraLocalQuandoEquipado=true,diagnosticoOcultacaoLocal,isActiveAndEnabled=true;
 private bool EstaArmazenadoNoInventario()=>armazenado;
 public Camera cameraLocalProprietario;public Transform cabecaAvatar;
 public List<Renderer> geometry=new List<Renderer>();
 public void GetComponentsInChildren<T>(bool inactive,List<T> result){result.Clear();result.AddRange(geometry.OfType<T>());}
 private void RemoverPopupVisivel(){}
 public void Equip(Camera c){jaEquipado=true;DefinirCameraLocalDoProprietario(c);RegistrarOcultacaoLocal();}
 public void EquipAuto(Transform head){jaEquipado=true;cabecaAvatar=head;cameraDefinidaPorCodigo=false;cameraLocalProprietario=null;RegistrarOcultacaoLocal();}
 public void Unequip(){EncerrarOcultacaoLocal();jaEquipado=false;}
 public void Stop(){EncerrarOcultacaoLocal();}
 public void DestroyForTest(){OnDestroy();}
 public void RepeatRegistration(){RegistrarOcultacaoLocal();}
'@
$tests=@'
}
public static class HelmetVisibilityTests {
 static int checks;static void Check(bool v,string m){checks++;if(!v)throw new Exception(m);}
 static Camera Cam()=>new GameObject().Add<Camera>();
 static HelmetHost Helmet(){var h=new GameObject().Add<HelmetHost>();h.geometry.Add(new GameObject().Add<Renderer>());h.geometry.Add(new GameObject().Add<Renderer>());return h;}
 public static string Run(){
  var local=Cam();var remote=Cam();var h=Helmet();var r=h.geometry[0];var child=h.geometry[1];var material=r.material;
  RenderPipelineManager.Begin(local);Check(!r.forceRenderingOff&&!child.forceRenderingOff,"Unequipped ground/held item hidden");
  h.Equip(local);h.armazenado=true;RenderPipelineManager.Begin(local);Check(!r.forceRenderingOff,"Inventory helmet hidden despite stored state");RenderPipelineManager.End(local);h.armazenado=false;h.RepeatRegistration();Check(RenderPipelineManager.Subscribers==1,"Duplicate render listeners");
  RenderPipelineManager.Begin(remote);Check(!r.forceRenderingOff,"Remote camera hidden");RenderPipelineManager.End(remote);
  RenderPipelineManager.Begin(local);Check(r.forceRenderingOff&&child.forceRenderingOff,"Local camera did not hide complete geometry");Check(r.enabled&&child.enabled&&ReferenceEquals(material,r.material)&&r.gameObject.layer==3&&local.cullingMask==-1,"Renderer enabled/material/layer/camera mask changed");
  RenderPipelineManager.Begin(remote);Check(!r.forceRenderingOff&&!child.forceRenderingOff,"Nested remote camera hidden");RenderPipelineManager.End(remote);Check(r.forceRenderingOff,"Nested camera did not resume owner scope");RenderPipelineManager.End(local);Check(!r.forceRenderingOff&&!child.forceRenderingOff,"Local scope leaked");
  child.forceRenderingOff=true;child.enabled=false;RenderPipelineManager.Begin(local);RenderPipelineManager.End(local);Check(child.forceRenderingOff&&!child.enabled,"Existing legitimate hidden state overwritten");child.forceRenderingOff=false;
  RenderPipelineManager.Begin(local);h.Unequip();Check(!r.forceRenderingOff&&!child.forceRenderingOff&&RenderPipelineManager.Subscribers==0,"Unequip/inventory transition left geometry hidden");
  h.Equip(local);RenderPipelineManager.Begin(local);RenderPipelineManager.Finish();Check(!r.forceRenderingOff,"Interrupted render/context did not restore");
  var fresh=Cam();h.DefinirCameraLocalDoProprietario(fresh);RenderPipelineManager.Begin(local);Check(!r.forceRenderingOff,"Old explicit camera still hides");RenderPipelineManager.End(local);RenderPipelineManager.Begin(fresh);Check(r.forceRenderingOff,"Replacement camera does not hide");RenderPipelineManager.End(fresh);
  h.DefinirCameraLocalDoProprietario(null);RenderPipelineManager.Begin(local);Check(!r.forceRenderingOff,"Remote/no local camera ownership hidden");RenderPipelineManager.End(local);h.Stop();
  var player=new GameObject();var avatar=player.Add<AvatarBody>();avatar.cameraCabeca=local.transform;var head=new GameObject();head.transform.parent=player.transform;h.EquipAuto(head.transform);RenderPipelineManager.Begin(local);Check(r.forceRenderingOff,"Owner camera not resolved from AvatarBody");RenderPipelineManager.End(local);avatar.cameraCabeca=fresh.transform;RenderPipelineManager.Begin(fresh);Check(r.forceRenderingOff,"Reconstructed automatic camera not followed");RenderPipelineManager.End(fresh);
  var extra=new GameObject().Add<Renderer>();h.geometry.Add(extra);RenderPipelineManager.Begin(fresh);Check(extra.forceRenderingOff,"New child geometry missed");h.DestroyForTest();Check(!r.forceRenderingOff&&!extra.forceRenderingOff&&RenderPipelineManager.Subscribers==0,"Destroy left state/listeners");
  var first=Helmet();var second=Helmet();first.Equip(local);second.Equip(local);first.Unequip();RenderPipelineManager.Begin(local);Check(!first.geometry[0].forceRenderingOff&&second.geometry[0].forceRenderingOff,"Old helmet interfered with new helmet");RenderPipelineManager.End(local);second.Stop();
  h.Equip(local);GraphicsSettings.currentRenderPipeline=null;Camera.Begin(local);Check(r.forceRenderingOff,"Built-in pre-cull did not hide");Camera.End(local);Check(!r.forceRenderingOff,"Built-in post-render did not restore");h.Stop();GraphicsSettings.currentRenderPipeline=new object();
  Check(RenderPipelineManager.Subscribers==0,"Final listener leak");
  return checks+" verificacoes aprovadas: metodos reais de ocultacao por camera; renderizacao Unity/Quest nao executada.";
 }
}
'@
$code="using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;`n"+$stub+$fields+$methods+$tests
Add-Type -TypeDefinition $code -IgnoreWarnings -WarningAction SilentlyContinue
[HelmetVisibilityTests]::Run()
