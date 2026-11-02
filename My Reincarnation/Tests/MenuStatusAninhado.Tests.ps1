$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/MyMenu.cs')
$a=$source.IndexOf('    private static GameObject ResolverRaizPainel(')
$b=$source.IndexOf('    private static InventarioVR EncontrarInventario(', $a)
$methods=$source.Substring($a,$b-$a)
$code=@'
using System;
public class Transform {public Transform parent;public GameObject gameObject;}
public class GameObject {public bool activeSelf=true;public Transform transform;public GameObject(){transform=new Transform{gameObject=this};}public void SetActive(bool value){activeSelf=value;}public bool Visible()=>activeSelf&&(transform.parent==null||transform.parent.gameObject.Visible());}
public class Button {public Transform transform;}
public class InventarioVR {public int opens,closes;public void AbrirInventario(){opens++;}public void FecharInventario(){closes++;}}
public class NestedMenuHost {
 public class AbaMenuDados {public Button botao;public GameObject painel;}
 private static InventarioVR inventory;
 private static InventarioVR EncontrarInventario(GameObject p)=>inventory;
'@+$methods+@'
 public static int Run(){int checks=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);checks++;};
  var buttonGo=new GameObject();var container=new GameObject();container.transform.parent=buttonGo.transform;
  var panel=new GameObject();panel.transform.parent=container.transform;
  var tab=new AbaMenuDados{botao=new Button{transform=buttonGo.transform},painel=panel};
  container.SetActive(false);panel.SetActive(true);c(!panel.Visible(),"Reproduce disabled ancestor");
  AbrirPainelInterno(tab);c(panel.Visible(),"Open must enable ancestor and configured content");
  FecharPainelInterno(tab);c(!panel.Visible()&&buttonGo.activeSelf,"Close must hide branch without disabling button");
  panel.SetActive(false);AbrirPainelInterno(tab);c(panel.Visible(),"Previously disabled child must also open");
  inventory=new InventarioVR();FecharPainelInterno(tab);AbrirPainelInterno(tab);c(inventory.opens==1&&inventory.closes==1,"Inventory APIs must remain active");
  inventory=null;var external=new GameObject();tab.painel=external;c(ResolverRaizPainel(tab)==external,"Unrelated panels must not change root");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[NestedMenuHost]::Run()+" cenarios reais de abertura/fechamento em host simulado.")
