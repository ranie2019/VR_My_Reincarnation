$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/Status/StatusPlayer.cs')
$a=$source.IndexOf('    public int GetVidaAtual()')
$b=$source.IndexOf('    private void IniciarRegeneracaoMana()', $a)
$methods=$source.Substring($a,$b-$a)
$a=$source.IndexOf('    private void SincronizarVidaAtualReal()')
$b=$source.IndexOf('    private void LogDano(', $a)
$methods+=$source.Substring($a,$b-$a)
$code=@'
using System;
public enum TipoAtributoManopla { HP }
public static class Mathf {
 public static int Max(int a,int b)=>Math.Max(a,b);
 public static int CeilToInt(float value)=>(int)Math.Ceiling(value);
 public static int Clamp(int v,int a,int b)=>Math.Min(b,Math.Max(a,v));
 public static float Clamp(float v,float a,float b)=>Math.Min(b,Math.Max(a,v));
}
public class ResourceHost {
 private int vidaAtual=100,vidaMaxima=100,manaAtual=50,manaMaxima=50;
 private float vidaAtualReal=100;private bool vidaAtualRealInicializada=true;
 private int hpLuvas=3,hpPeitoral=4,hpElmo=5,mp=4;private int events;
 private int ObterBonusManoplas(TipoAtributoManopla t)=>hpLuvas;
 private int ObterBonusPeitoral(TipoAtributoManopla t)=>hpPeitoral;
 private int ObterBonusElmo(TipoAtributoManopla t)=>hpElmo;
 private int GetManaTotal()=>mp;
 private void NotificarStatusAlterado(){events++;}
'@+$methods+@'
 public static int Run(){
  int checks=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);checks++;};
  var h=new ResourceHost();
  c(h.GetVidaMaxima()==112&&h.GetManaMaxima()==54,"Equipment must increase actual HP/MP caps");
  c(h.GetVidaAtual()==100&&h.GetManaAtual()==50,"Equipping must not grant free healing");
  h.RecuperarMana(10);c(h.GetManaAtual()==54,"Regeneration must reach equipment MP cap");
  c(!h.TentarConsumirMana(55)&&h.GetManaAtual()==54,"Insufficient MP must not consume");
  c(h.TentarConsumirMana(54)&&h.GetManaAtual()==0,"Equipment MP must be spendable");
  h.RecuperarMana(int.MaxValue);c(h.GetManaAtual()==54,"Large recovery must clamp without overflow");
  for(int i=0;i<100;i++)h.AtualizarLimitesRecursos();
  c(h.GetManaMaxima()==54&&h.GetManaMaximaBase()==50&&h.GetVidaMaximaBase()==100,"Base caps must not absorb bonuses");
  h.mp=0;h.AtualizarLimitesRecursos();c(h.GetManaAtual()==50&&h.GetManaMaxima()==50,"Unequip must clamp excess MP");
  h.vidaAtual=112;h.vidaAtualReal=111.5f;h.hpLuvas=0;h.hpPeitoral=0;h.hpElmo=0;
  h.SincronizarVidaAtualRealSeNecessario();c(h.vidaAtualReal==100,"Damage must clamp HP even before LateUpdate after unequip");h.AtualizarLimitesRecursos();
  c(h.GetVidaAtual()==100&&h.vidaAtualReal==100,"Unequip must clamp both integer and fractional HP");
  h.mp=4;h.AtualizarLimitesRecursos();c(h.GetManaAtual()==50,"Repeated equip cannot restore MP for free");
  c(SomarLimiteRecurso(int.MaxValue,100)==int.MaxValue,"Cap sum must saturate");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[ResourceHost]::Run()+" cenarios dos metodos reais de HP/MP em host simulado.")
$save=Get-Content -Raw (Join-Path $project 'Assets/Script/Save/SaveManager.cs')
if (!$save.Contains('"GetVidaMaximaBase", "vidaMaxima"') -or !$save.Contains('"GetManaMaximaBase", "manaMaxima"')) {throw 'Save must capture base resource caps'}
$types=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/AtributosManoplas.cs')
if (!$types.Contains('Mana = 12') -or !$types.Contains('Defesa = 14') -or !$types.Contains('HP = 15')) {throw 'Existing equipment attribute IDs changed'}
Write-Output 'PASS: save usa limites base e IDs antigos de atributos preservados.'
