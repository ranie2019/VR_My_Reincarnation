$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/Status/StatusVR.cs')
$a=$source.IndexOf('    private void LateUpdate()')
$b=$source.IndexOf('    private void OnValidate()', $a)
$fixture=@'
using System;
public struct Vector3 {public float x,y,z;}
public struct Quaternion {public int value;}
public class Transform {public Vector3 position;public Quaternion rotation;public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}}
public class GameObject {public bool activeInHierarchy=true;public Transform transform=new Transform();}
public class MenuPoseHost {
 private GameObject painelStatus=new GameObject();private Transform cameraJogador=new Transform();
 private bool alturaOriginalMenuCapturada=true,poseMundoMenuCapturada=true;
 private float alturaOriginalMenuRelativaCamera=.2f,alturaMenuNaCamera=.1f;
 private Vector3 posicaoMundoMenuAberto=new Vector3{x=3,y=2,z=4};private Quaternion rotacaoMundoMenuAberto=new Quaternion{value=42};
'@+$source.Substring($a,$b-$a)+@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var h=new MenuPoseHost();
 h.cameraJogador.position=new Vector3{y=1.5f};h.painelStatus.transform.position=new Vector3{x=99,y=-3,z=80};h.painelStatus.transform.rotation=new Quaternion{value=77};h.LateUpdate();
 c(h.painelStatus.transform.position.x==3&&h.painelStatus.transform.position.z==4,"Rig rotation must not displace menu XZ");c(h.painelStatus.transform.rotation.value==42,"Rig rotation must not rotate open menu");c(Math.Abs(h.painelStatus.transform.position.y-1.8f)<.001,"Preserve vertical headset tracking");
 h.cameraJogador.position=new Vector3{y=.7f};h.LateUpdate();c(Math.Abs(h.painelStatus.transform.position.y-1f)<.001,"Seated height follows camera");
 h.painelStatus.activeInHierarchy=false;h.painelStatus.transform.position=new Vector3{x=88};h.LateUpdate();c(h.painelStatus.transform.position.x==88,"Closed menu untouched");
 h.painelStatus.activeInHierarchy=true;h.poseMundoMenuCapturada=false;h.LateUpdate();c(h.painelStatus.transform.position.x==88,"Uninitialized pose cannot jump to zero");return n;
 }
}
'@
Add-Type -TypeDefinition $fixture
Write-Output ('PASS: '+[MenuPoseHost]::Run()+' verificacoes do LateUpdate real, host simulado.')
