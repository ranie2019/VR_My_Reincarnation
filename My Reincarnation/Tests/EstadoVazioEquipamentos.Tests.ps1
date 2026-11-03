$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($script in @('AtributosManoplas','AtributosPeitoral','AtributosElmo')) {
    $source = Get-Content -Raw (Join-Path $project "Assets/Script/Equipamento/$script.cs")
    $a = $source.IndexOf('    internal static bool EhResultadoVazioLegado(')
    $b = $source.IndexOf('    private static List<ConfiguracaoClasse> CriarClassesPadrao()', $a)
    $methods = $source.Substring($a, $b - $a)
    $fixture = @'
using System; using System.Collections.Generic;
public enum ClasseManopla { Comum, Incomum, Raro, Epico, Unico, Lendario, Mitico, Divino }
public enum AfinidadeDivinaManopla { Nenhuma, Luz, Trevas }
public enum TipoAtributoManopla { Forca }
public struct Color { public int value; public static bool operator ==(Color a,Color b)=>a.value==b.value; public static bool operator !=(Color a,Color b)=>a.value!=b.value; public override bool Equals(object o)=>o is Color && this==(Color)o; public override int GetHashCode()=>value; }
public class ValorAtributo { public TipoAtributoManopla atributo; public int valor; }
public class EstadoGerado { public int versao=1; public string nome="",descricao=""; public ClasseManopla classe; public AfinidadeDivinaManopla afinidade; public Color cor; public List<ValorAtributo> atributos=new List<ValorAtributo>(); }
public static class JsonUtility { public static object next; public static T FromJson<T>(string json)=>(T)next; }
public class HOST {
'@
    $fixture += $methods
    $fixture += @'
public static int Run() {
 int n=0; Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};
 var s=new EstadoGerado(); JsonUtility.next=s;
 c(EhResultadoVazioLegado(s),"Inline placeholder must not count as generated");
 c(LerEValidarEstadoJson("placeholder")==null,"Known empty save must migrate");
 s.nome="Valid Common"; c(!EhResultadoVazioLegado(s),"Zero-stat Common is valid");
 c(object.ReferenceEquals(s,LerEValidarEstadoJson("valid")),"Keep existing zero-stat result");
 s.atributos.Add(new ValorAtributo {valor=7}); c(object.ReferenceEquals(s,LerEValidarEstadoJson("valid")),"Keep rolled attributes");
 s.nome=""; bool rejected=false;try{LerEValidarEstadoJson("invalid");}catch(System.IO.InvalidDataException){rejected=true;}
 c(rejected,"Nameless nonempty result must still fail");
 s=new EstadoGerado {versao=2}; JsonUtility.next=s; rejected=false;try{LerEValidarEstadoJson("invalid");}catch(System.IO.InvalidDataException){rejected=true;}
 c(rejected,"Unknown version must still fail");
 s=new EstadoGerado {classe=ClasseManopla.Raro}; JsonUtility.next=s; rejected=false;try{LerEValidarEstadoJson("invalid");}catch(System.IO.InvalidDataException){rejected=true;}
 c(rejected,"Do not migrate other malformed results"); return n;
}}
'@
    $hostName = $script + 'EmptyStateTests'
    Add-Type -TypeDefinition ($fixture.Replace('using System; using System.Collections.Generic;', 'using System; using System.Collections.Generic; namespace ' + $hostName + 'Space {').Replace('HOST', $hostName) + '}')
    $type = ($hostName + 'Space.' + $hostName) -as [type]
    Write-Output ("PASS: $script - " + $type::Run() + ' verificacoes')
}
