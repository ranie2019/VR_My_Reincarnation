$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/NPC/Sacerdote/GerenciadorMissoes.cs')
$a=$source.IndexOf('    internal Action CapturarRecuperacaoSave()')
$b=$source.IndexOf('    public List<MissionSaveData> CapturarEstadoParaSave()', $a)
$method=$source.Substring($a,$b-$a)
$code=@'
using System;using System.Collections;using System.Collections.Generic;
public enum EstadoMissao { NaoIniciada, EmAndamento, ProntaParaEntregar, Concluida }
public class MissaoDados {public EstadoMissao estado;public int quantidadeAtualObjetivo;}
public class Transform:IEnumerable<Transform> {public bool active=true;public List<Transform> children=new List<Transform>();public IEnumerator<Transform> GetEnumerator()=>children.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
public class RecuperacaoItemCarga {Transform alvo;bool ativo;public RecuperacaoItemCarga(Transform t){alvo=t;ativo=t.active;}public void Restaurar(){alvo.active=ativo;}}
public class MissionRollbackHost {
 private int missaoAtualIndex;private List<MissaoDados> missoes=new List<MissaoDados>();private Transform transform=new Transform();
'@+$method+@'
 public static int Run(){
  var host=new MissionRollbackHost();var original=new MissaoDados{estado=EstadoMissao.EmAndamento,quantidadeAtualObjetivo=7};
  host.missoes.Add(original);host.missoes.Add(new MissaoDados{estado=EstadoMissao.NaoIniciada});host.missaoAtualIndex=1;
  var child=new Transform{active=false};host.transform.children.Add(child);var undo=host.CapturarRecuperacaoSave();
  original.estado=EstadoMissao.Concluida;original.quantidadeAtualObjetivo=99;host.missaoAtualIndex=0;child.active=true;
  undo();
  if(!object.ReferenceEquals(host.missoes[0],original))throw new Exception("Mission identity changed");
  if(original.estado!=EstadoMissao.EmAndamento||original.quantidadeAtualObjetivo!=7)throw new Exception("Mission progress not restored");
  if(host.missaoAtualIndex!=1||child.active)throw new Exception("Index or UI state not restored");
  return 3;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[MissionRollbackHost]::Run()+" cenarios de recuperacao real das missoes em host simulado.")
