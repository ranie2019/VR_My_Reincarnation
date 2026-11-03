$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Enemy/Slime/SlimeIA.cs')
$motor=Get-Content -Raw (Join-Path $project 'Assets/Script/Enemy/Slime/SlimeMovimento.cs')
function Extract($text,$start,$end){$a=$text.IndexOf($start);if($a -lt 0){throw "Missing method $start"};$b=$text.IndexOf($end,$a+$start.Length);if($b -lt 0){throw "Missing end $end"};return $text.Substring($a,$b-$a)}
$methods=(Extract $source '    private void AplicarDanoRecebido(' '    public void AcionarDano(')+(Extract $source '    private void CancelarImpactoAtaquePendente(' '    private void AtualizarAlvoPlayer(')+(Extract $source '    private void MudarEstado(' '    // ---------------------------------------------------------------')+(Extract $source '    private void Morrer()' '    private void EntregarExperienciaAoMorrer()')
$methods += Extract $source '    private void ResolverImpactoCollider(' '    private void ResolverImpactoAtaque('
$math=Extract $motor '    internal static bool SoloTransitavel(' '    private bool PassoSeguro('
$code=@'
using System;
using System.Collections;
using System.Collections.Generic;
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3(0,1,0);public float sqrMagnitude=>x*x+y*y+z*z;public Vector3 normalized=>this*(1f/(float)Math.Sqrt(sqrMagnitude));public static Vector3 operator*(Vector3 v,float n)=>new Vector3(v.x*n,v.y*n,v.z*n);public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n){float d=(v.x*n.x+v.y*n.y+v.z*n.z)/n.sqrMagnitude;return new Vector3(v.x-n.x*d,v.y-n.y*d,v.z-n.z*d);}}
public static class Mathf {public const float Deg2Rad=(float)Math.PI/180;public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float n,float a,float b)=>Math.Min(b,Math.Max(a,n));public static float Cos(float n)=>(float)Math.Cos(n);}
public class GameObject {public void SetActive(bool v){}}
public class Transform {public Vector3 position;public object rotation;}
public class Canvas {public GameObject gameObject=new GameObject();}
public class Coroutine {}
public class Collider {public bool shield,player;}
public class SlimeMovimento {public bool PreservarArcoInvestida;public float RaioMundial=>.45f;public float AlturaMundial=>1f;public Vector3 CentroRelativo=>new Vector3(0,.5f,0);public bool dead;public int resumes;public void Suspender(bool v){if(!v)resumes++;}public void Morrer(){dead=true;}}
public class SlimeAnimacao {public int restarts;public float deathTime=1.5f;public void ReiniciarAcaoConfigurada(string s){restarts++;}public float ObterTempoMorteConfigurado()=>deathTime;}
public class RespawnMonstro {public static RespawnMonstro Instancia=new RespawnMonstro();public int scheduled;public bool fail;public Transform[] patrol;public void AgendarRespawnVerde(string id,Vector3 p,object r,Transform[] t,int key){if(fail)throw new Exception("respawn callback");scheduled++;patrol=t;}public void AgendarRespawn(string id,Vector3 p,object r,Transform[] t){scheduled++;}public void AgendarRespawnVermelho(string id,Vector3 p,object r,Transform[] t,int key,float radius,float height,Vector3 center){AgendarRespawnVerde(id,p,r,t,key);}}
public static class Debug {public static int errors;public static void LogException(Exception e,object o){errors++;}}
public class Host {
 enum EstadoSlime{Parado,Patrulhando,Observando,Alerta,Perseguindo,Atacando,TomandoDano,Morto}
 SlimeAnimacao animacao;SlimeMovimento movimentoVerde;bool morto,impactoAtaqueResolvido,investidaAtaqueEmAndamento;int vidaAtual=10;Transform ultimoPlayerResponsavel;Coroutine rotinaDano,rotinaInvestidaAtaque;EstadoSlime estadoAtual,estadoAntesDano;object somDano;float volumeDano;bool esconderCanvasAoMorrer=true;Canvas canvasVida=new Canvas();float tempoParaMorrer=.5f;GameObject gameObject=new GameObject();Transform transform=new Transform();string idRespawnMonstro="SlimeVerde";Transform[] pontosPatrulha=new[]{new Transform()};List<GameObject> pontosAutoProprios=new List<GameObject>();
 public int xp,rein,missions,loot,missionLoot,destroys,stops,playerHits,blocks;int danoAtaque=3;Transform alvoPlayer=new Transform();public bool failXp,failRein;public static Host New(bool green=true){var h=new Host();h.movimentoVerde=green?new SlimeMovimento():null;return h;}
 void AtualizarBarraVida(){}void TocarSomUmaVez(object s,float v){}void PararMovimento(){}void PararSomAndar(){}void TocarSomMorrer(){}void AtualizarAnimatorPorEstado(){}void AtualizarAudioPorEstado(){}void RetomarAgentDepoisInvestida(){}void StopCoroutine(Coroutine c){stops++;}Coroutine StartCoroutine(IEnumerator r){r.MoveNext();return new Coroutine();}IEnumerator RotinaDano(){MudarEstado(EstadoSlime.TomandoDano);yield return null;}void Destroy(GameObject g,float t){destroys++;}int GetInstanceID()=>1;
 bool ColliderBloqueiaMesmoSendoDoPlayer(Collider c)=>c.shield;bool PertenceAoPlayerAlvo(Collider c,Transform p)=>c.player;void RegistrarBloqueioSeForEscudo(Collider c,Transform p){if(c.shield)blocks++;}bool TentarAplicarDanoNoPlayer(Transform p,int d){playerHits++;return true;}
 void EntregarExperienciaAoMorrer(){xp++;if(failXp)throw new Exception("XP callback");}void PagarRecompensaReinAoPrimeiroAtacante(){rein++;if(failRein)throw new Exception("REIN callback");}void NotificarMissaoDeMorte(){missions++;}void SpawnarPrefabsNormais(){loot++;}void SpawnarPrefabsMissao(){missionLoot++;}
'@+$methods+$math+@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};
 c(SoloTransitavel(Vector3.up,45),"Flat ground traversable");c(SoloTransitavel(new Vector3(0,.9f,.435f),45),"Gentle slope traversable");c(!SoloTransitavel(new Vector3(1,0,0),45),"Vertical wall rejected");c(!SoloTransitavel(new Vector3(0,.4f,.916f),45),"Steep slope rejected");
 var ascent=VelocidadeNoSolo(new Vector3(0,0,1),new Vector3(0,.8f,-.6f),1.2f);c(ascent.y>0&&Math.Abs(ascent.sqrMagnitude-1.44f)<.001,"Ascent follows slope at configured speed");var descent=VelocidadeNoSolo(new Vector3(0,0,1),new Vector3(0,.8f,.6f),1.2f);c(descent.y<0,"Descent follows negative tangent");c(VelocidadeNoSolo(Vector3.zero,Vector3.up,1).sqrMagnitude==0,"Zero direction stable");
 var h=New();h.estadoAtual=EstadoSlime.Atacando;h.investidaAtaqueEmAndamento=true;h.rotinaInvestidaAtaque=new Coroutine();h.AplicarDanoRecebido(1,null);c(h.stops==1&&!h.investidaAtaqueEmAndamento&&h.rotinaInvestidaAtaque==null,"Damage completely cancels old lunge");c(h.estadoAtual==EstadoSlime.TomandoDano&&h.estadoAntesDano==EstadoSlime.Perseguindo,"Recover into pursuit rather than canceled attack");h.AplicarDanoRecebido(1,null);c(h.vidaAtual==8,"No received damage cooldown introduced");
 var old=New(false);old.estadoAtual=EstadoSlime.Atacando;old.rotinaInvestidaAtaque=new Coroutine();old.AplicarDanoRecebido(1,null);c(old.rotinaInvestidaAtaque!=null&&old.estadoAntesDano==EstadoSlime.Atacando,"Legacy species keep previous interruption behaviour");
 RespawnMonstro.Instancia=new RespawnMonstro();h=New();h.failXp=true;h.failRein=true;h.Morrer();h.Morrer();c(h.xp==1&&h.rein==1&&h.loot==1&&h.missionLoot==1&&h.missions==1,"Rewards invoked once despite failed callbacks and repeated death");c(h.destroys==1&&RespawnMonstro.Instancia.scheduled==1,"Death and respawn survive reward exceptions");c(h.movimentoVerde.dead,"Death disables green movement");h.AplicarDanoRecebido(3,null);c(h.vidaAtual==0,"Dead slime rejects subsequent damage");
 RespawnMonstro.Instancia=new RespawnMonstro{fail=true};h=New();h.Morrer();c(h.destroys==1,"Respawn callback failure cannot leave permanent corpse");
 RespawnMonstro.Instancia=new RespawnMonstro();h=New();h.pontosAutoProprios.Add(new GameObject());h.Morrer();c(RespawnMonstro.Instancia.patrol==null,"Automatic points are recreated rather than transferred as dangling references");
 h=New();h.Morrer();c(RespawnMonstro.Instancia.patrol!=null,"Manual patrol references are retained");
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.ResolverImpactoCollider(new Collider{shield=true});c(h.blocks==1&&h.playerHits==0,"Shield intercepts attack without damaging player");
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.ResolverImpactoCollider(new Collider{player=true});h.ResolverImpactoCollider(new Collider{player=true});c(h.playerHits==1,"Multiple target colliders resolve a lunge only once");
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.AplicarDanoRecebido(1,null);h.ResolverImpactoCollider(new Collider{player=true});c(h.playerHits==0,"Canceled lunge cannot apply late damage");h.Morrer();h.ResolverImpactoCollider(new Collider{player=true});c(h.playerHits==0,"Dead slime cannot apply attack damage");return n;}
}
'@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0649,0414'
Write-Output ("PASS: "+[Host]::Run()+" verificacoes de metodos reais de dano, interrupcao, morte e matematica do solo. Host simulado; fisica Unity/Quest nao executadas.")
