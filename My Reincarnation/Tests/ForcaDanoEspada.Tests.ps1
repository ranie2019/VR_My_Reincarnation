$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Armas/Espada.cs')
$a=$source.IndexOf('    internal static int CalcularDanoComForca(')
$b=$source.IndexOf('    public GameObject ObterDono()', $a)
$method=$source.Substring($a,$b-$a)
$code=@'
using System;
public static class SwordDamageTests {
'@ + $method + @'
 public static int Run(){int checks=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);checks++;};
 c(CalcularDanoComForca(10,29,1)==39,"Use total strength including equipment");
 c(CalcularDanoComForca(10,29,0)==10,"Zero multiplier preserves base damage");
 c(CalcularDanoComForca(10,29,2)==68,"Configurable multiplier");
 c(CalcularDanoComForca(10,29,.5f)==25,"Round consistently to integer damage");
 c(CalcularDanoComForca(10,0,1)==10,"No owner retains weapon damage");
 c(CalcularDanoComForca(10,29,float.NaN)==10,"Invalid multiplier safe");
 c(CalcularDanoComForca(int.MaxValue,int.MaxValue,100)==int.MaxValue,"Saturate instead of overflow");
 return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[SwordDamageTests]::Run()+" verificacoes da formula real de dano.")
$status=Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/Status/StatusPlayer.cs')
if (!$status.Contains('Mathf.Max(0f, (float)GetConstituicaoTotal() + GetResistenciaTotal())') -or !$status.Contains('ataqueBruto * (ataqueBruto / (ataqueBruto + defesaCalculada))')) {throw 'Formula original de defesa alterada'}
Write-Output 'PASS: formula original de defesa preservada, usando atributos totais.'
