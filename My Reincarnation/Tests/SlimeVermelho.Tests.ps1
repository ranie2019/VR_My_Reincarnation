$ErrorActionPreference='Stop'
# Reutiliza o host de regressao do Verde e acrescenta metodos reais exclusivos do Vermelho.
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'SlimeVerde.Tests.ps1')
$injection=@'
$arc=Extract $motor '    internal static Vector3 CalcularArco(' '    public bool MoverArco('
$routine=Extract $source '    private IEnumerator InvestidaArcoVermelho(' '    private IEnumerator InvestidaFisicaVerde('
$routine=$routine.Replace('SlimeMovimento.CalcularArco','CalcularArco')
$more=@"
 Rigidbody rb=new Rigidbody();float distanciaInvestida=1.5f,alturaInvestida=.6f,duracaoInvestida=.35f;
 Vector3 ObterDirecaoHorizontalInvestida()=>new Vector3(0,0,1);
 void ResolverImpactoAtaque(){ResolverImpactoCollider(new Collider{player=true});}
 public static int RunVermelho(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};
 var inicio=new Vector3(10,2,20);var direcao=new Vector3(0,0,1);
 c(CalcularArco(inicio,direcao,1.5f,.6f,0).z==20,"Arc starts at actual attack position");
 var peak=CalcularArco(inicio,direcao,1.5f,.6f,.5f);c(Math.Abs(peak.y-2.6f)<.001&&Math.Abs(peak.z-20.75f)<.001,"Red arc preserves original height and horizontal progress");
 var end=CalcularArco(inicio,direcao,1.5f,.6f,1);c(Math.Abs(end.y-2)<.001&&end.z==21.5f,"Red arc preserves distance and returns to original altitude before gravity resumes");
 var h=New();h.movimentoVerde.PreservarArcoInvestida=true;h.movimentoVerde.rb=h.rb;h.estadoAtual=EstadoSlime.Atacando;h.animacao=new SlimeAnimacao();
 var routine=h.InvestidaArcoVermelho(direcao);int steps=0;while(routine.MoveNext())steps++;
 c(Math.Abs(steps*Time.fixedDeltaTime-.35f)<.051,"Original duration retained to physics step precision");c(h.playerHits==1&&h.rb.useGravity,"Attack resolves once and restores gravity");
 h=New();h.movimentoVerde.PreservarArcoInvestida=true;h.movimentoVerde.rb=h.rb;h.estadoAtual=EstadoSlime.Atacando;h.animacao=new SlimeAnimacao();routine=h.InvestidaArcoVermelho(direcao);routine.MoveNext();routine.MoveNext();int before=h.movimentoVerde.moves;h.AplicarDanoRecebido(1,null);while(routine.MoveNext()){};
 c(h.movimentoVerde.moves==before&&h.playerHits==0&&h.rb.useGravity,"Damage interrupts arc completely with no late movement or hit");
 h=New();h.movimentoVerde.PreservarArcoInvestida=true;h.movimentoVerde.rb=h.rb;h.movimentoVerde.block=true;h.estadoAtual=EstadoSlime.Atacando;routine=h.InvestidaArcoVermelho(direcao);while(routine.MoveNext()){};c(h.movimentoVerde.escudo.blocks==1&&h.playerHits==0&&h.rb.useGravity,"Shield blocks arc and gravity recovers");
 h=New();h.movimentoVerde.rb=h.rb;h.movimentoVerde.land=true;h.estadoAtual=EstadoSlime.Atacando;routine=h.InvestidaArcoVermelho(direcao);while(routine.MoveNext()){};c(h.playerHits==1&&h.rb.useGravity,"Landing resolves the attack once and restores gravity");
 RespawnMonstro.Instancia=new RespawnMonstro();h=New();h.movimentoVerde.PreservarArcoInvestida=true;h.animacao=new SlimeAnimacao();h.tempoParaMorrer=.2f;h.failXp=true;h.Morrer();h.Morrer();c(h.destroys==1&&RespawnMonstro.Instancia.scheduled==1&&h.loot==1,"Red death finalizes and schedules once despite reward failure");c(h.destroyedAfter>=1.5f,"Destruction waits for configured death animation");
 return n;}
"@
$index=$code.LastIndexOf('}')
$code=$code.Insert($index,$arc+$routine+$more)
$code=$code.Replace('public static Vector3 operator*','public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator*')
$code=$code.Replace('public const float Deg2Rad=','public const float PI=(float)Math.PI;public static float Sin(float n)=>(float)Math.Sin(n);public static float Clamp01(float n)=>Math.Clamp(n,0,1);public const float Deg2Rad=')
$code=$code.Replace('public class Coroutine {}','public static class Time {public static float fixedDeltaTime=.05f;}public class WaitForFixedUpdate {}public class Rigidbody {public Vector3 position;public bool useGravity=true;}public class Coroutine {}')
$code=$code.Replace('public bool dead;public int resumes;','public Rigidbody rb;public bool block, land;public int moves;public Escudo escudo=new Escudo();public void IniciarArco(Vector3 d){rb.useGravity=false;}public bool MoverArco(Vector3 p,out Collider c,out bool aterrissou){moves++;rb.position=p;c=block?new Collider{shield=escudo}:null;aterrissou=land;return block||land;}public bool dead;public int resumes;')
$code=$code.Replace('if(!v)resumes++;','if(!v){resumes++;if(rb!=null)rb.useGravity=true;}')
$code=$code.Replace('void Destroy(GameObject g,float t){destroys++;}','public float destroyedAfter;void Destroy(GameObject g,float t){destroys++;destroyedAfter=t;}')
'@
$fixture=$fixture.Replace('Add-Type -TypeDefinition $code',$injection+"`nAdd-Type -TypeDefinition `$code")
$testDirectory=$PSScriptRoot
$fixture=$fixture.Replace('$PSScriptRoot','$testDirectory')
Invoke-Expression $fixture
Write-Output ("PASS: "+[Host]::RunVermelho()+" verificacoes especificas do arco, interrupcao, escudo e morte do Vermelho; fisica Unity simulada.")
