$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/Inventario/BotaoRolagemInventarioVR.cs')
$a=$source.IndexOf('    private bool ComponenteEhXRRelacionado(')
$method=$source.Substring($a).TrimEnd()
$fixture=@'
using System;
public class MonoBehaviour {}
namespace UnityEngine.XR.Fakes {public class XRDirectInteractor:MonoBehaviour{}public class XRHand:MonoBehaviour{}public class TrackedPoseDriver:MonoBehaviour{}public class XRGeneralGrabTransformer:MonoBehaviour{}public class XROrigin:MonoBehaviour{}}
public class FilterHost {
'@+$method+@'
public static class FilterTest {
 public static int Run(){var h=new FilterHost();int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};
 c(h.ComponenteEhXRRelacionado(new UnityEngine.XR.Fakes.XRDirectInteractor()),"Direct hand interactor accepted");c(h.ComponenteEhXRRelacionado(new UnityEngine.XR.Fakes.XRHand()),"XR hand accepted");
 c(!h.ComponenteEhXRRelacionado(new UnityEngine.XR.Fakes.TrackedPoseDriver()),"Tracked head pose cannot scroll");c(!h.ComponenteEhXRRelacionado(new UnityEngine.XR.Fakes.XRGeneralGrabTransformer()),"Held item transformer cannot scroll");c(!h.ComponenteEhXRRelacionado(new UnityEngine.XR.Fakes.XROrigin()),"Player rig cannot scroll");return n;
 }
}
'@
# O trecho extraido inclui o fechamento da classe original.
$fixture=$fixture.Replace('private bool ComponenteEhXRRelacionado','public bool ComponenteEhXRRelacionado')
Add-Type -TypeDefinition $fixture
Write-Output ('PASS: '+[FilterTest]::Run()+' verificacoes do filtro real, host simulado.')
