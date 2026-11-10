$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fire=Get-Content -Raw (Join-Path $root 'Assets/Script/Armas/BolaDeFogo.cs')
$area=Get-Content -Raw (Join-Path $root 'Assets/Script/Armas/EfeitoFogoArea.cs')
function Body([string]$s,[string]$sig){$a=$s.IndexOf($sig);if($a -lt 0){throw "Missing $sig"};$o=$s.IndexOf('{',$a);$d=1;$e=$o+1;while($d -gt 0){if($s[$e] -eq '{'){$d++}elseif($s[$e] -eq '}'){$d--};$e++};$s.Substring($a,$e-$a)}
$code='using System;'+$fire.Substring($fire.IndexOf('public enum ElementoAtaqueMagico'))+@'
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int RoundToInt(float f)=>(int)Math.Round(f);}
public class FireHost {
 public enum EstadoBolaDeFogo {Preparada,Lancada,Explodida}
 public int bonusAtaqueMagia;public ElementoAtaqueMagico elementoMagia=ElementoAtaqueMagico.Fogo;
 public float dano=20;public EstadoBolaDeFogo estadoAtual;public bool jaExplodiu;
 ContextoDanoMagico? danoConfiguradoPeloCajado;public ContextoDanoMagico Contexto=>danoConfiguradoPeloCajado.Value;
'@
foreach($sig in @('    public float ObterDano()', '    public int ObterDanoInteiro()', '    public void ConfigurarAtaqueDoCajado(')){$code+=Body $fire $sig}
$code+=@'
}
public class AreaHost {
 public bool inicializada;public int danoPorPulso=1;private ContextoDanoMagico? danoConfiguradoPelaMagia;
'@
$code+=($area -split "`n" | Where-Object {$_ -match '^    public int DanoPorPulsoAtual =>'}) -join "`n"
$code+=Body $area '    public void ConfigurarDanoMagico('
$code+=@'
}
public static class MagicTests {
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var f=new FireHost();c(f.ObterDanoInteiro()==20,"Legacy spell changed without staff configuration");f.ConfigurarAtaqueDoCajado(7);c(f.ObterDanoInteiro()==7&&f.ObterDano()==7,"Staff attack not used");c(f.Contexto.AtaquePrincipal==7&&f.Contexto.BonusSecundario==0&&f.Contexto.Elemento==ElementoAtaqueMagico.Fogo,"Secondary base or element wrong");
 f.bonusAtaqueMagia=3;f.ConfigurarAtaqueDoCajado(7);c(f.ObterDanoInteiro()==10,"Secondary bonus not additive");f.estadoAtual=FireHost.EstadoBolaDeFogo.Lancada;f.ConfigurarAtaqueDoCajado(99);c(f.ObterDanoInteiro()==10,"In-flight damage changed");
 var a=new AreaHost();c(a.DanoPorPulsoAtual==1,"Legacy area changed");a.ConfigurarDanoMagico(f.Contexto);a.inicializada=true;c(a.DanoPorPulsoAtual==10,"Area did not inherit projectile power");a.ConfigurarDanoMagico(new ContextoDanoMagico(99,0,ElementoAtaqueMagico.Neutro));c(a.DanoPorPulsoAtual==10,"Active area changed snapshot");
 var other=new FireHost();other.ConfigurarAtaqueDoCajado(2);c(other.ObterDanoInteiro()==2&&f.ObterDanoInteiro()==10&&a.DanoPorPulsoAtual==10,"Different launches shared mutable damage");
 c(new ContextoDanoMagico(int.MaxValue,50,ElementoAtaqueMagico.Fogo).DanoTotal==int.MaxValue,"Damage overflow");c(new ContextoDanoMagico(-5,-9,ElementoAtaqueMagico.Fogo).DanoTotal==0,"Negative damage accepted");
 var plant=new ContextoDanoMagico(7,0,ElementoAtaqueMagico.Planta);c(plant.DanoTotal==7,"Element applied unimplemented multiplier");var zero=new FireHost();zero.ConfigurarAtaqueDoCajado(0);c(zero.ObterDanoInteiro()==0,"Zero staff attack fell back to old spell damage");return n;}
}
'@
Add-Type -TypeDefinition $code
Write-Output ('PASS: '+[MagicTests]::Run()+' verificacoes reais do ataque principal/secundario e snapshot; Unity simulado.')
$staff=Get-Content -Raw (Join-Path $root 'Assets/Script/Armas/CajadoMagico.cs')
if(!$staff.Contains('bolaParaLancar.ConfigurarAtaqueDoCajado(dadosCajado.DanoBase)')){throw 'Staff roll not connected to launch'}
if(!$fire.Contains('area.ConfigurarDanoMagico(danoConfiguradoPeloCajado.Value)') -or !$area.Contains('ReceptorDanoInimigo.Aplicar(slime, danoEfetivo, origemDano)')){throw 'Area pulse bypasses shared attack'}
Write-Output 'PASS: lancamento, explosao e pulsos conectados ao ataque configurado.'
