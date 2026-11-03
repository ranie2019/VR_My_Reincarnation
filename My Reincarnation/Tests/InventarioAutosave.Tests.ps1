$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SaveManager.cs')
$a=$source.IndexOf('    private bool salvamentoInventarioPendente;')
$b=$source.IndexOf('    private void OnApplicationQuit()', $a)
$fixture=@'
using System;
public static class Time {public static float unscaledTime;}
public class AutosaveHost {
 public enum EstadoCarga {NaoIniciado,EmAndamento,Concluido,Falhou,RecuperacaoNecessaria,SessaoNova}
 public EstadoCarga EstadoCarregamento;private int operacoesCriticas,saves;
 private void SalvarJogo(){if(EstadoCarregamento==EstadoCarga.Concluido||EstadoCarregamento==EstadoCarga.SessaoNova)saves++;}
'@+$source.Substring($a,$b-$a)+@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var h=new AutosaveHost();
 foreach(var state in new[]{EstadoCarga.NaoIniciado,EstadoCarga.EmAndamento,EstadoCarga.Falhou,EstadoCarga.RecuperacaoNecessaria}){h.EstadoCarregamento=state;h.SolicitarSalvamentoInventario();c(!h.salvamentoInventarioPendente,"Unsafe state cannot schedule save");}
 h.EstadoCarregamento=EstadoCarga.SessaoNova;Time.unscaledTime=0;h.SolicitarSalvamentoInventario();h.Update();c(h.saves==0,"Debounce must wait");
 Time.unscaledTime=2;h.operacoesCriticas=1;h.Update();c(h.saves==0&&h.salvamentoInventarioPendente,"Wait for transaction");
 h.operacoesCriticas=0;h.Update();c(h.saves==1&&!h.salvamentoInventarioPendente,"Save new player after transaction");h.Update();c(h.saves==1,"No repeated write");
 h.SolicitarSalvamentoInventario();h.EstadoCarregamento=EstadoCarga.Falhou;Time.unscaledTime=4;h.Update();c(h.saves==1,"Failure after scheduling still blocks");
 return n;}
}
'@
Add-Type -TypeDefinition $fixture
Write-Output ('PASS: '+[AutosaveHost]::Run()+' verificacoes do agendamento real, host simulado.')
