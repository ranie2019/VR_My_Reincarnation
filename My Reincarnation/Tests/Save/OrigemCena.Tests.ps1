$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SaveManager.cs')
$a=$source.IndexOf('    private ItemPersistente EncontrarItemSoltoPorOrigem(')
$b=$source.IndexOf('    private ItemPersistente EncontrarItemSoltoPorInstancia(', $a)
$method=$source.Substring($a,$b-$a).Replace('private ItemPersistente EncontrarItemSoltoPorOrigem','public ItemPersistente EncontrarItemSoltoPorOrigem')
$code=@'
using System;using System.IO;using System.Collections.Generic;
public class ItemPersistente {
 public string OrigemCenaId,type;public bool stored;
 public bool CorrespondeAoItemId(string id)=>id==type;
 public static bool InstanciaIdEhGuidValido(string id)=>Guid.TryParseExact(id,"N",out _);
}
public class OriginHost {
 private bool ItemEstaNoInventarioOuSlot(ItemPersistente item)=>item.stored;
'@
$tests=@'
}
public static class OriginTests {
 static int count;static void Check(bool x,string m){count++;if(!x)throw new Exception(m);}
 public static string Run(){
  var h=new OriginHost();var id=Guid.NewGuid().ToString("N");var a=new ItemPersistente {OrigemCenaId=id,type="helmet"};var list=new List<ItemPersistente>{a};var used=new HashSet<ItemPersistente>();
  Check(h.EncontrarItemSoltoPorOrigem(list,id,"helmet",used)==a,"Exact origin not resolved");
  Check(h.EncontrarItemSoltoPorOrigem(list,id,"chest",used)==null,"Origin resolved wrong type");
  Check(h.EncontrarItemSoltoPorOrigem(list,Guid.NewGuid().ToString("N"),"helmet",used)==null,"Different origin matched by type");
  used.Add(a);Check(h.EncontrarItemSoltoPorOrigem(list,id,"helmet",used)==null,"Used instance reused");used.Clear();a.stored=true;Check(h.EncontrarItemSoltoPorOrigem(list,id,"helmet",used)==null,"Inventory instance reused as world item");a.stored=false;
  list.Add(new ItemPersistente {OrigemCenaId=id,type="helmet"});bool rejected=false;try{h.EncontrarItemSoltoPorOrigem(list,id,"helmet",used);}catch(InvalidDataException){rejected=true;}Check(rejected,"Duplicate origin accepted");
  Check(h.EncontrarItemSoltoPorOrigem(list,"","helmet",used)==null,"Legacy empty origin guessed");
  return count+" verificacoes aprovadas: resolucao real por origem; Unity Editor nao executado.";
 }
}
'@
Add-Type -TypeDefinition ($code+$method+$tests)
[OriginTests]::Run()
