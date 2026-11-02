$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/AtributosManoplas.cs')
$start = $source.IndexOf('    internal static int SortearClasse(')
$end = $source.IndexOf('    private string ObterNomePrefab()', $start)
$method = $source.Substring($start, $end - $start)
$code = @'
using System;
using System.Collections.Generic;
public enum ClasseManopla { Comum, Incomum, Raro, Epico, Unico, Lendario, Mitico, Divino }
public enum AfinidadeDivinaManopla { Nenhuma, Luz, Trevas }
public static class ManoplasRandomTests {
'@ + $method + @'
 public static int Run() {
  int checks=0;
  Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
  var rng=new Random(87234);
  Func<double> draw=()=>rng.NextDouble();
  check(SortearClasse(new double[]{0,1,0},draw)==1,"Zero deve desativar");
  for(int i=0;i<1000;i++)check(SortearClasse(new double[]{0,.0001,0},draw)==1,"Chance baixa deve terminar");
  bool rejected=false;try{SortearClasse(new double[]{0,0},draw);}catch(InvalidOperationException){rejected=true;}
  check(rejected,"Todas zero devem ser rejeitadas");
  rejected=false;try{SortearClasse(new double[]{double.NaN,1},draw);}catch(ArgumentOutOfRangeException){rejected=true;}
  check(rejected,"NaN deve ser rejeitado");
  int n=200000, second=0;
  for(int i=0;i<n;i++) if(SortearClasse(new double[]{1,1},draw)==1)second++;
  check(Math.Abs((double)second/n-.5)<.005,"Desempate 50/50");
  var counts=new int[3];
  for(int i=0;i<n;i++)counts[SortearClasse(new double[]{1,1,1},draw)]++;
  foreach(int count in counts)check(Math.Abs((double)count/n-1d/3)<.005,"Desempate de tres");
  second=0;
  for(int i=0;i<n;i++)if(SortearClasse(new double[]{.7,.05},draw)==1)second++;
  double expected=(.3*.05+.7*.05/2)/(1-.3*.95);
  check(Math.Abs((double)second/n-expected)<.003,"Repeticao de rodadas vazias");
  check(SortearAfinidade(ClasseManopla.Comum,()=>{throw new Exception("Nao deve sortear afinidade");})==AfinidadeDivinaManopla.Nenhuma,"Nao Divino sem afinidade");
  check(SortearAfinidade(ClasseManopla.Divino,()=>.499999)==AfinidadeDivinaManopla.Luz,"Metade inferior Luz");
  check(SortearAfinidade(ClasseManopla.Divino,()=>.5)==AfinidadeDivinaManopla.Trevas,"Metade superior Trevas");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: " + [ManoplasRandomTests]::Run() + " verificacoes do sorteio real.")

# Real getters and bonus aggregation, with an isolated equipment host.
$status = Get-Content -Raw (Join-Path $project 'Assets/Script/MENU/Status/StatusPlayer.cs')
$start = $status.IndexOf('    private int ObterBonusElmo(')
$end = $status.IndexOf('    public int GetBonusConstituicao()', $start)
$bonus = $status.Substring($start, $end - $start)
$start = $status.IndexOf('    public int GetForcaTotal()')
$end = $status.IndexOf('    public int GetConstituicaoTotal()', $start)
$total = $status.Substring($start, $end - $start)
$bonusCode = @'
using System;
public enum TipoAtributoManopla { Forca }
public class FakeObject { public bool activeInHierarchy=true; }
public class AtributosManoplas { public int value; public int ObterValor(TipoAtributoManopla a)=>value; }
public class EquiparManoplas {
 public bool JaEquipado=true; public FakeObject gameObject=new FakeObject(); public AtributosManoplas data;
 public T GetComponent<T>() where T:class=>data as T;
}
public class AtributosPeitoral {public int value;public int ObterValor(TipoAtributoManopla a)=>value;}
public class EquiparPeitoral {public bool JaEquipado=true;public FakeObject gameObject=new FakeObject();public AtributosPeitoral data;public T GetComponent<T>() where T:class=>data as T;}
public class AtributosElmo {public int value;public int ObterValor(TipoAtributoManopla a)=>value;}
public class EquiparElmo {public bool JaEquipado=true;public FakeObject gameObject=new FakeObject();public AtributosElmo data;public T GetComponent<T>() where T:class=>data as T;}
public class GerenciadorEquipamentosPlayer { public EquiparManoplas LuvaDireita, LuvaEsquerda; public EquiparPeitoral PeitoralAtual; public EquiparElmo ElmoAtual; }
public class BonusManoplasTests {
 public int forca=21,bonusForca=0; public GerenciadorEquipamentosPlayer manager=new GerenciadorEquipamentosPlayer();
 public T GetComponent<T>() where T:class=>manager as T;
'@ + $bonus + $total + @'
 public static int Run() {
  var player=new BonusManoplasTests();int checks=0;
  Action<int,int> check=(a,b)=>{if(a!=b)throw new Exception(a+" != "+b);checks++;};
  player.manager.LuvaDireita=new EquiparManoplas {data=new AtributosManoplas {value=5}};
  player.manager.LuvaEsquerda=new EquiparManoplas {data=new AtributosManoplas {value=3}};
  check(player.GetBonusForca(),8);check(player.GetForcaTotal(),29);
  for(int i=0;i<100;i++)check(player.GetForcaTotal(),29);
  player.manager.LuvaDireita.JaEquipado=false;check(player.GetBonusForca(),3);
  player.manager.LuvaDireita=player.manager.LuvaEsquerda;check(player.GetBonusForca(),3);
  player.manager.LuvaDireita=null;player.manager.LuvaEsquerda=null;check(player.GetForcaTotal(),21);
  player.bonusForca=2;check(player.GetForcaTotal(),23);
  player.manager.PeitoralAtual=new EquiparPeitoral{data=new AtributosPeitoral{value=4}};
  check(player.GetForcaTotal(),27);player.manager.PeitoralAtual.JaEquipado=false;check(player.GetForcaTotal(),23);
  player.manager.PeitoralAtual.JaEquipado=true;
  player.manager.LuvaDireita=new EquiparManoplas{data=new AtributosManoplas{value=5}};
  player.manager.LuvaEsquerda=new EquiparManoplas{data=new AtributosManoplas{value=3}};
  check(player.GetBonusForca(),14);check(player.GetForcaTotal(),35);
  player.manager.ElmoAtual=new EquiparElmo{data=new AtributosElmo{value=6}};
  check(player.GetForcaTotal(),41);player.manager.ElmoAtual.JaEquipado=false;check(player.GetForcaTotal(),35);
  return checks;
 }
}
'@
Add-Type -TypeDefinition $bonusCode
Write-Output ("PASS: " + [BonusManoplasTests]::Run() + " verificacoes de soma, retirada e ausencia de acumulo.")
