$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SaveManager.cs')
$a=$source.IndexOf('    private ItemPersistente EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(')
$b=$source.IndexOf('    private List<ItemPersistente> ObterItensPersistentesSoltosCena()', $a)
$methods=$source.Substring($a,$b-$a)
$fixture=@'
using System;using System.Collections.Generic;
public class ItemPersistente {
 public string OrigemCenaId="",id=Guid.NewGuid().ToString("N"),tipo="Wood";
 public bool runtime,stored;
 public bool CorrespondeAoItemId(string value)=>tipo==value;
 public bool InstanciaIdFoiGeradoEmRuntime()=>runtime;
 public string ObterInstanciaIdSemGerar()=>id;
 public static bool InstanciaIdEhGuidValido(string id)=>Guid.TryParseExact(id,"N",out _);
}
public class CloneHost {
 private bool ItemEstaNoInventarioOuSlot(ItemPersistente item)=>item.stored;
'@+$methods+@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};
  var h=new CloneHost();var used=new HashSet<ItemPersistente>();var origin=Guid.NewGuid().ToString("N");
  var original=new ItemPersistente{OrigemCenaId=origin,runtime=true};var list=new List<ItemPersistente>{original};
  var reserved=new HashSet<string>{origin};
  c(h.EncontrarItemSoltoComIdRuntimeParaMigrar(list,"Wood",used,out int candidates,reserved,true)==null&&candidates==0,"Do not steal a reserved scene original for a clone");
  c(h.EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(list,"Wood",used,reserved,true)==null,"Reserved scene origin must not block independent clone");
  c(h.EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(list,"Wood",used,reserved)==original,"Legacy default protection preserved");
  reserved.Clear();c(h.EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(list,"Wood",used,reserved,true)==original,"Unknown origin remains blocked");
  var a=new ItemPersistente{runtime=true};var b=new ItemPersistente{runtime=true};list=new List<ItemPersistente>{a,b};
  c(h.EncontrarItemSoltoComIdRuntimeParaMigrar(list,"Wood",used,out candidates,reserved,true)==null&&candidates==2,"Real runtime ambiguity must still fail");
  c(h.EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(list,"Wood",used,reserved,true)!=null,"Unidentified copies remain protected");
  list.Remove(b);c(h.EncontrarItemSoltoComIdRuntimeParaMigrar(list,"Wood",used,out candidates,reserved,true)==a&&candidates==1,"Unique migration preserved");
  a.stored=true;c(h.EncontrarItemSoltoComIdRuntimeParaMigrar(list,"Wood",used,out candidates,reserved,true)==null,"Never reuse inventory item");
  return n;
 }
}
'@
Add-Type -TypeDefinition $fixture
Write-Output ('PASS: '+[CloneHost]::Run()+' verificacoes dos resolvedores reais de cena, host simulado.')
