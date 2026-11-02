$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$methods=@{}
foreach($name in @('Manoplas','Elmo','Peitoral')){
 $s=Get-Content -Raw (Join-Path $project "Assets/Script/Equipamento/Equipar$name.cs")
 $a=$s.IndexOf('    internal System.Action CapturarEstadoEquipadoParaCarga()')
 $b=$s.IndexOf('    public void Desequipar()',$a)
 $methods[$name]=$s.Substring($a,$b-$a)
}
$code=@'
using System;
using System.Collections.Generic;
public class Renderer {public bool enabled;}
public class GloveRollback {
 bool jaEquipado;Action restaurarEstadoEquipamento,restaurarVisibilidadeMao;
'@+$methods['Manoplas']+@'
 public static bool Run(){var g=new GloveRollback{jaEquipado=true};int state=0,hand=0;g.restaurarEstadoEquipamento=()=>state++;g.restaurarVisibilidadeMao=()=>hand++;var restore=g.CapturarEstadoEquipadoParaCarga();g.jaEquipado=false;g.restaurarEstadoEquipamento=null;g.restaurarVisibilidadeMao=null;restore();g.restaurarEstadoEquipamento();g.restaurarVisibilidadeMao();return g.jaEquipado&&state==1&&hand==1;}
}
public class HelmetRollback {
 bool jaEquipado;bool isActiveAndEnabled=true;int registered,ended;
 void EncerrarOcultacaoLocal(){ended++;}void RegistrarOcultacaoLocal(){registered++;}
'@+$methods['Elmo']+@'
 public static bool Run(){var h=new HelmetRollback{jaEquipado=true};var restore=h.CapturarEstadoEquipadoParaCarga();h.jaEquipado=false;restore();return h.jaEquipado&&h.registered==1&&h.ended==1;}
 public static bool Inactive(){var h=new HelmetRollback{jaEquipado=true};var restore=h.CapturarEstadoEquipadoParaCarga();h.isActiveAndEnabled=false;restore();return h.jaEquipado&&h.registered==0;}
}
public class ChestRollback {
 bool jaEquipado;Dictionary<Renderer,bool> visibilidadeOriginalCorpo=new Dictionary<Renderer,bool>();
 void AtualizarVisibilidadeCorpo(){foreach(var r in visibilidadeOriginalCorpo)r.Key.enabled=false;}
'@+$methods['Peitoral']+@'
 public static bool Run(){var body=new Renderer{enabled=false};var c=new ChestRollback{jaEquipado=true};c.visibilidadeOriginalCorpo.Add(body,true);var restore=c.CapturarEstadoEquipadoParaCarga();c.jaEquipado=false;c.visibilidadeOriginalCorpo.Clear();c.visibilidadeOriginalCorpo.Add(body,false);restore();bool hidden=c.jaEquipado&&!body.enabled;foreach(var r in c.visibilidadeOriginalCorpo)r.Key.enabled=r.Value;return hidden&&body.enabled;}
}
'@
Add-Type -TypeDefinition $code
if(-not [GloveRollback]::Run()){throw 'Glove runtime flags or original restore callbacks lost.'}
if(-not [HelmetRollback]::Run()){throw 'Helmet local visibility registration not restored.'}
if(-not [HelmetRollback]::Inactive()){throw 'Inactive helmet registers render callbacks.'}
if(-not [ChestRollback]::Run()){throw 'Original avatar visibility lost after chest rollback and unequip.'}
Write-Output 'PASS: 4 cenarios dos snapshots runtime reais; estado privado, callbacks da mao, ocultacao local e visibilidade original do corpo. Host simulado.'
