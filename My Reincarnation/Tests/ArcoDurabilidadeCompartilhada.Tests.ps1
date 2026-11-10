$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../Assets/Script/Armas/Arco.cs')
function Body([string]$sig){$a=$source.IndexOf($sig);if($a -lt 0){throw "Missing $sig"};$o=$source.IndexOf('{',$a);$d=1;$e=$o+1;while($d -gt 0){if($source[$e] -eq '{'){$d++}elseif($source[$e] -eq '}'){$d--};$e++};$source.Substring($a,$e-$a)}
$code=@'
using System;
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
public class Arcopopup {public float VidaAtual=30,VidaMaxima=40;public bool EstaQuebrada=>VidaAtual<=0;public void AplicarDesgasteCombate(float v){VidaAtual=Math.Max(0,VidaAtual-v);}public void RepararCompleto(){VidaAtual=VidaMaxima;}}
public class BowHost {
 private Arcopopup data;private float durabilidadeAtual=70,durabilidadeMaxima=100;private bool arcoQuebrado;
 private T GetComponent<T>() where T:class=>data as T;
 private void AtualizarTextoDurabilidade(bool criar=false){}
 private void QuebrarArco(){arcoQuebrado=true;}
 private class GameObject { public bool activeInHierarchy=true; }
 private GameObject gameObject=new GameObject();
 private bool flechaLimpa,cordaResetada;
 private void LimparFlechaPreparada(bool destruir){flechaLimpa=true;}
 private void ResetarCorda(){cordaResetada=true;}
'@
$code+=($source -split "`n" | Where-Object {$_ -match '^    public (float DurabilidadeAtual|float DurabilidadeMaxima|bool Quebrado) =>'}) -join "`n"
$code+=Body '    public void RepararCompleto()'
$code+=Body '    private void ReduzirDurabilidade(float valor)'
$code+=@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var b=new BowHost();b.ReduzirDurabilidade(5);c(b.DurabilidadeAtual==65,"Legacy bow damage changed");b.RepararCompleto();c(b.DurabilidadeAtual==100,"Legacy bow repair changed");b.data=new Arcopopup();c(b.DurabilidadeAtual==30&&b.DurabilidadeMaxima==40,"Shared roll not used");b.ReduzirDurabilidade(7);c(b.data.VidaAtual==23&&b.durabilidadeAtual==100,"Two separate durability values consumed");b.RepararCompleto();c(b.data.VidaAtual==40,"Repair did not heal shared durability");b.arcoQuebrado=true;c(!b.Quebrado,"Legacy state blocked valid shared bow");b.ReduzirDurabilidade(100);c(b.Quebrado&&b.data.VidaAtual==0,"Shared zero life not broken");c(b.flechaLimpa&&b.cordaResetada,"Broken bow left arrow or string prepared");return n;}
}
'@
Add-Type -TypeDefinition $code
Write-Output ('PASS: '+[BowHost]::Run()+' verificacoes reais da durabilidade do arco; host simulado.')
