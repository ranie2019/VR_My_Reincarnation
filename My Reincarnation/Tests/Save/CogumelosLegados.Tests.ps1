param([string]$SavePath)
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SaveManager.cs')
$a=$source.IndexOf('    internal static bool RegistroCogumeloDeveSerIsolado(')
$method=$source.Substring($a,$source.IndexOf('    private bool DeveIsolarCogumelo(',$a)-$a)
$item=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/ItemPersistente.cs')
$a=$item.IndexOf('    public class EstadoCenaItem')
$dto=$item.Substring($a,$item.IndexOf('    private void Awake()',$a)-$a)
$scene=[regex]::Replace((Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SceneObjectSaveData.cs')),'(?m)^using .*?;\s*$','')
$code=@'
using System;
using System.Collections.Generic;
using System.Text.Json;
public struct Vector3 {public float x,y,z;}
public class ItemPersistente {
'@+$dto+@'
}
public static class LegacyChecks {
'@+$method+@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,s)=>{if(!ok)throw new Exception(s);n++;};var d=new SceneObjectSaveData{objectId="legacy",estadoJson="original payload",ativo=true};var s=new ItemPersistente.EstadoCenaItem{possuiPontoCogumelo=true,cogumeloReposicao=true,cogumeloColetado=true};
 c(RegistroCogumeloDeveSerIsolado(d,s,true,false),"Exact corrupt signature isolated");
 c(!RegistroCogumeloDeveSerIsolado(d,s,true,true),"Real authored point at zero preserved");
 c(!RegistroCogumeloDeveSerIsolado(d,s,false,false),"Other item types never isolated");
 s.origemCenaId="original";c(!RegistroCogumeloDeveSerIsolado(d,s,true,false),"Scene originals preserved");s.origemCenaId=null;
 s.coletaJogadorConfirmada=true;c(!RegistroCogumeloDeveSerIsolado(d,s,true,false),"Confirmed collected items preserved");s.coletaJogadorConfirmada=false;
 s.pontoCogumelo=new Vector3{x=500};c(!RegistroCogumeloDeveSerIsolado(d,s,true,false),"Nonzero points preserved");s.pontoCogumelo=new Vector3();
 s.posicaoEquipamento="mao-direita";c(!RegistroCogumeloDeveSerIsolado(d,s,true,false),"Equipment never quarantined");s.posicaoEquipamento=null;
 c(d.estadoJson=="original payload"&&d.ativo&&!d.quarentenaCogumelo,"Classification does not mutate raw data");
 d.quarentenaCogumelo=true;c(RegistroCogumeloDeveSerIsolado(d,s,true,true),"Already isolated data stays archived on subsequent loads");return n;}
 public class Root {public List<SceneObjectSaveData> objetosCena;}
 public static string Inspect(string json){var options=new JsonSerializerOptions{IncludeFields=true};var root=JsonSerializer.Deserialize<Root>(json,options);int all=0,isolated=0;foreach(var d in root.objetosCena){if(string.IsNullOrEmpty(d.estadoJson))continue;ItemPersistente.EstadoCenaItem s;try{s=JsonSerializer.Deserialize<ItemPersistente.EstadoCenaItem>(d.estadoJson,options);}catch{continue;}if(s==null||!s.possuiPontoCogumelo)continue;all++;if(RegistroCogumeloDeveSerIsolado(d,s,true,false))isolated++;}return "Registros com ponto de cogumelo: "+all+"; assinatura legada isolavel: "+isolated+". Leitura somente, arquivo nao alterado.";}
}
'@
Add-Type -TypeDefinition ($code+$scene)
$count=[LegacyChecks]::Run()
Write-Output "PASS: $count verificacoes do classificador real de migracao; dados originais preservados."
if($SavePath){
 $before=(Get-FileHash -LiteralPath $SavePath -Algorithm SHA256).Hash
 Write-Output ([LegacyChecks]::Inspect([IO.File]::ReadAllText($SavePath)))
 $after=(Get-FileHash -LiteralPath $SavePath -Algorithm SHA256).Hash
 if($before -ne $after){throw 'Save changed while being inspected (possibly external autosave).'}
}
