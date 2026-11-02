$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/EquiparManoplas.cs')
$a=$source.IndexOf('    private void DesativarFisicaDoItem()')
$b=$source.IndexOf('    private void EsconderMaoSeConfigurado()', $a)
$disable=$source.Substring($a,$b-$a)
$a=$source.IndexOf('    private void CapturarEstadosAntesDeEquipar()')
$b=$source.IndexOf('    internal void PrepararQuebraPorDurabilidade()', $a)
$capture=$source.Substring($a,$b-$a)
$code=@'
using System;using System.Linq;using System.Collections.Generic;
public struct Vector3 {public static Vector3 zero=>new Vector3();}
public class XRBaseInteractable {public bool enabled;}
public class Collider {public bool enabled;}
public class SkinnedMeshRenderer {public bool enabled;}
public class Rigidbody {
 public bool isKinematic,detectCollisions;
 public Vector3 linearVelocity {set{if(isKinematic)throw new Exception("kinematic velocity");}}
 public Vector3 angularVelocity {set{if(isKinematic)throw new Exception("kinematic angular velocity");}}
}
public class GloveHost {
 private Action restaurarVisibilidadeMao;
 private Action restaurarEstadoEquipamento;private SkinnedMeshRenderer rendererMaoAvatar=new SkinnedMeshRenderer();
 public List<object> components=new List<object>();
 private T[] GetComponentsInChildren<T>(bool inactive)=>components.OfType<T>().ToArray();
'@ + $disable + $capture + @'
 public static int Run(){
  var h=new GloveHost();var colOff=new Collider{enabled=false};var colOn=new Collider{enabled=true};
  var xrOff=new XRBaseInteractable{enabled=false};var xrOn=new XRBaseInteractable{enabled=true};
  var dynamicBody=new Rigidbody{isKinematic=false,detectCollisions=true};var staticBody=new Rigidbody{isKinematic=true,detectCollisions=false};
  h.components.AddRange(new object[]{colOff,colOn,xrOff,xrOn,dynamicBody,staticBody});h.rendererMaoAvatar.enabled=false;
  h.CapturarEstadosAntesDeEquipar();h.DesativarFisicaDoItem();
  if(!dynamicBody.isKinematic||dynamicBody.detectCollisions||!staticBody.isKinematic)throw new Exception("Equip physics");
  xrOn.enabled=false;h.rendererMaoAvatar.enabled=true;h.restaurarEstadoEquipamento();
  if(colOff.enabled||!colOn.enabled||xrOff.enabled||!xrOn.enabled)throw new Exception("Original collider/interaction state lost");
  if(dynamicBody.isKinematic||!dynamicBody.detectCollisions||!staticBody.isKinematic||staticBody.detectCollisions)throw new Exception("Original rigidbody state lost");
  if(h.rendererMaoAvatar.enabled)throw new Exception("Originally hidden hand became visible");
  return 4;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[GloveHost]::Run()+" cenarios de fisica/estados reais em host simulado.")
