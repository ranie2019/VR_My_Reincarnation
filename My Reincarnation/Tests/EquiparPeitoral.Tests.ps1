$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/GerenciadorEquipamentosPlayer.cs')
$a=$source.IndexOf('    public bool TentarEquipar(EquiparPeitoral')
$b=$source.IndexOf('    private bool TentarGuardarLuvaAtual', $a)
$manager=$source.Substring($a,$b-$a)
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/EquiparPeitoral.cs')
$a=$source.IndexOf('    private void AtualizarVisibilidadeCorpo()')
$b=$source.IndexOf('    private bool EstaArmazenadoNoInventario()', $a)
$visibility=$source.Substring($a,$b-$a)
$stub=@'
namespace UnityEngine {
 public class Component {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:class=>gameObject.components.OfType<T>().FirstOrDefault();}
 public class GameObject {public List<Component> components=new List<Component>();public Transform transform;public GameObject(){transform=new Transform {gameObject=this};}public T Add<T>() where T:Component,new(){var c=new T {gameObject=this};components.Add(c);return c;}}
 public class Transform:Component {public Transform parent;public bool IsChildOf(Transform t){for(var p=parent;p!=null;p=p.parent)if(p==t)return true;return false;}}
 public class Renderer:Component {public bool enabled=true;}
 public static class Debug {public static void LogWarning(object m,object c){} }
}
public class XRGrabInteractable:Component {}
public class EstadoItemInventario:Component {public bool estaNoInventario;public SlotInventario slotAtual;}
public class SlotInventario {
 public int capacity=1;public List<XRGrabInteractable> items=new List<XRGrabInteractable>();
 public bool PodeRetirarItemParaEquipamento(XRGrabInteractable g)=>items.Count>0&&items[items.Count-1]==g;
 public bool RetirarItemParaEquipamento(XRGrabInteractable g){if(!PodeRetirarItemParaEquipamento(g))return false;items.RemoveAt(items.Count-1);var e=g.GetComponent<EstadoItemInventario>();e.estaNoInventario=false;e.slotAtual=null;return true;}
 public bool RestaurarItemSalvoNoSlot(XRGrabInteractable g,bool hidden){if(items.Count>=capacity)return false;items.Add(g);var e=g.GetComponent<EstadoItemInventario>();e.estaNoInventario=true;e.slotAtual=this;return true;}
 public void FinalizarRestauracaoDoSave(){}
}
public class InventarioVR {public SlotInventario[] slots;public SlotInventario[] ObterSlotsParaSave()=>slots;}
public class EquiparPeitoral:Component {
 public bool JaEquipado,esconderCorpoAoEquipar;public Transform CorpoConfigurado,corpoAvatar;public Renderer[] renderersCorpoAvatar;
 private bool jaEquipado=>JaEquipado;
 private readonly Dictionary<Renderer,bool> visibilidadeOriginalCorpo=new Dictionary<Renderer,bool>();
 public bool PodeEquiparNoCorpo(Transform t)=>t!=null;
 public bool EquiparNoCorpo(Transform t){if(t==null)return false;JaEquipado=true;CorpoConfigurado=t;corpoAvatar=t;AtualizarVisibilidadeCorpo();return true;}
 public void Desequipar(){JaEquipado=false;RestaurarVisibilidadeCorpo();}
'@
$middle=@'
}
public class ManagerHost {
 public EquiparPeitoral peitoralAtual;public bool trocandoPeitoral;public Transform corpoAvatar;public InventarioVR inventario;
 private void ResolverReferencias(){}
'@
$tests=@'
}
public static class ChestTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static EquiparPeitoral Armor(){var go=new GameObject();go.Add<XRGrabInteractable>();go.Add<EstadoItemInventario>();return go.Add<EquiparPeitoral>();}
 static ManagerHost Manager(){return new ManagerHost {corpoAvatar=new GameObject().transform,inventario=new InventarioVR {slots=new[]{new SlotInventario()}}};}
 public static string Run(){
  var m=Manager();var a=Armor();Check(m.TentarEquipar(a)&&a.JaEquipado&&m.peitoralAtual==a,"World equip");Check(m.TentarEquipar(a),"Repeated equip");
  var b=Armor();var slot=m.inventario.slots[0];slot.RestaurarItemSalvoNoSlot(b.GetComponent<XRGrabInteractable>(),false);Check(m.TentarEquipar(b),"Swap from full source slot");Check(m.peitoralAtual==b&&b.JaEquipado&&!a.JaEquipado&&slot.items.Single()==a.GetComponent<XRGrabInteractable>(),"Previous armor not returned");
  var c=Armor();Check(!m.TentarEquipar(c)&&m.peitoralAtual==b&&b.JaEquipado&&!c.JaEquipado,"Full inventory lost equipped armor");Check(!m.DesequiparPeitoral()&&b.JaEquipado,"Full inventory unequip");
  slot.items.Clear();Check(m.DesequiparPeitoral()&&m.peitoralAtual==null&&!b.JaEquipado&&slot.items.Count==1,"Unequip storage");
  m=Manager();m.corpoAvatar=null;a=Armor();Check(!m.TentarEquipar(a)&&!a.JaEquipado,"Missing body accepted");
  m=Manager();a=Armor();var body=new GameObject().Add<Renderer>();var alreadyHidden=new GameObject().Add<Renderer>();alreadyHidden.enabled=false;var own=a.gameObject.Add<Renderer>();a.renderersCorpoAvatar=new[]{body,alreadyHidden,own};
  m.TentarEquipar(a);Check(body.enabled,"Unchecked option hid body");a.Desequipar();a.esconderCorpoAoEquipar=true;m.TentarEquipar(a);Check(!body.enabled&&!alreadyHidden.enabled&&own.enabled,"Visibility hid armor/wrong state");a.Desequipar();Check(body.enabled&&!alreadyHidden.enabled,"Original renderer state not restored");Check(!m.trocandoPeitoral,"Exchange guard leaked");
  return checks+" verificacoes aprovadas: fluxo real de troca e visibilidade, Unity simulado.";
 }
}
'@
$code="using System;using System.Linq;using System.Collections.Generic;using UnityEngine;`n"+$stub+$visibility+$middle+$manager+$tests
Add-Type -TypeDefinition $code -IgnoreWarnings -WarningAction SilentlyContinue
[ChestTests]::Run()
