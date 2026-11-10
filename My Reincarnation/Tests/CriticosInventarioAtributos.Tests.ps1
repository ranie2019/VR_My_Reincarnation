$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Body([string]$source,[string]$sig){
 $a=$source.IndexOf($sig);if($a -lt 0){throw "Missing $sig"};$o=$source.IndexOf('{',$a);$d=1;$e=$o+1
 while($d -gt 0){if($source[$e] -eq '{'){$d++}elseif($source[$e] -eq '}'){$d--};$e++}
 $source.Substring($a,$e-$a)
}
$adapter=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/InventarioSaveAdapter.cs')
$status=Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/Status/StatusPlayer.cs')
$code=@'
using System;using System.Collections.Generic;
public class SlotInventario {}
public class InventorySaveData {public string itemId="arrow",instanciaId="saved",origemCenaId="";public int versaoEstadosIndividuais=1;}
public class ItemPersistente {
 public string id="world",OrigemCenaId="",tipo="arrow";public bool stored,slotted;
 public bool EstaNoInventario()=>stored;
 public T GetComponentInParent<T>(bool all) where T:class=>slotted?new SlotInventario() as T:null;
 public bool CorrespondeAoItemId(string id)=>tipo==id;
 public string ObterInstanciaIdSemGerar()=>id;
 public static bool InstanciaIdEhGuidValido(string s)=>Guid.TryParseExact(s,"N",out _);
}
public class CleanupHost {
'@+(Body $adapter '    private ItemPersistente EncontrarOriginalParaLimpezaDireta(')+@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var h=new CleanupHost();var a=new ItemPersistente();var b=new ItemPersistente();var list=new List<ItemPersistente>{a};var used=new HashSet<ItemPersistente>();var data=new InventorySaveData();
 c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==null,"Crafted arrow must not delete world arrow");
 data.origemCenaId=Guid.NewGuid().ToString("N");a.OrigemCenaId=Guid.NewGuid().ToString("N");c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==null,"Mismatched origin must not delete arrow");
 a.OrigemCenaId=data.origemCenaId;c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==a,"Exact origin must remove original");
 a.OrigemCenaId="";a.id="saved";c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==a,"Exact instance should match");
 a.tipo="sword";c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==null,"Conflicting type must not be deleted");a.tipo="arrow";
 a.stored=true;c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==null,"Stored item must not be deleted");a.stored=false;used.Add(a);c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==null,"Used original must not be deleted twice");used.Clear();
 a.id="world";data.origemCenaId="";data.versaoEstadosIndividuais=0;c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==a,"Unique legacy compatibility");list.Add(b);c(h.EncontrarOriginalParaLimpezaDireta(list,data,used)==null,"Ambiguous legacy must not delete first arrow");
 a.id="saved";b.id="saved";bool failed=false;try{h.EncontrarOriginalParaLimpezaDireta(list,data,used);}catch(InvalidOperationException){failed=true;}c(failed,"Duplicate identity must abort load");return n;
 }
}
public enum TipoAtributoManopla {Forca,HP}
public static class Machado {public static long value; public static long ObterBonusEmpunhado(object player,TipoAtributoManopla tipo)=>value;}
public static class Picareta {public static long ObterBonusEmpunhado(object player,TipoAtributoManopla tipo)=>0;}
public static class Escudo {public static long ObterBonusEmpunhado(object player,TipoAtributoManopla tipo)=>0;}
public static class Flecha {public static long ObterBonusEmUso(object player,TipoAtributoManopla tipo)=>0;}
public static class Espada {public static int value;public static int ObterBonusEmpunhado(object p,TipoAtributoManopla t)=>value;}
public class AttributeHost {
 int bonusForca,forca,gloves,chest,helmet;
 int ObterBonusManoplas(TipoAtributoManopla t)=>gloves;
 int ObterBonusPeitoral(TipoAtributoManopla t)=>chest;
 int ObterBonusElmo(TipoAtributoManopla t)=>helmet;
'@
foreach($sig in @('    internal static int SaturarAtributo(', '    public int GetBonusForca()', '    public int GetForcaTotal()', '    private long ObterBonusArmas(', '    public int GetBonusHP()')){$code+=Body $status $sig}
$code+=@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var h=new AttributeHost{forca=21,gloves=3,chest=5};c(h.GetBonusForca()==8&&h.GetForcaTotal()==29,"Normal formula changed");Machado.value=2;c(h.GetBonusForca()==10&&h.GetForcaTotal()==31,"Held axe bonus not included");Machado.value=0;c(h.GetBonusForca()==8,"Released axe bonus remained");h.gloves=int.MaxValue;h.chest=int.MaxValue;h.helmet=int.MaxValue;Espada.value=int.MaxValue;c(h.GetBonusForca()==int.MaxValue&&h.GetForcaTotal()==int.MaxValue,"Gear overflow");c(h.GetBonusHP()==int.MaxValue,"HP overflow");h.gloves=0;h.chest=0;h.helmet=0;Espada.value=0;h.bonusForca=int.MaxValue;h.forca=10;c(h.GetForcaTotal()==int.MaxValue,"Base bonus overflow");c(SaturarAtributo(long.MinValue)==int.MinValue,"Lower saturation failed");return n;}
}
'@
Add-Type -TypeDefinition $code
Write-Output ('PASS: '+([CleanupHost]::Run()+[AttributeHost]::Run())+' verificacoes reais de limpeza e overflow; host simulado.')
