$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Enemy/Slime/SlimeIA.cs')
$motor=$source
function Extract($text,$start,$end){$a=$text.IndexOf($start);if($a -lt 0){throw "Missing method $start"};$b=$text.IndexOf($end,$a+$start.Length);if($b -lt 0){throw "Missing end $end"};return $text.Substring($a,$b-$a)}
$methods=(Extract $source '    private void AplicarDanoRecebido(' '    public void AcionarDano(')+(Extract $source '    private void CancelarImpactoAtaquePendente(' '    private void AtualizarAlvoPlayer(')+(Extract $source '    private void MudarEstado(' '    // ---------------------------------------------------------------')+(Extract $source '    private void Morrer()' '    private void EntregarExperienciaAoMorrer()')
$methods += Extract $source '    private void ResolverImpactoCollider(' '    private void ResolverImpactoAtaque('
$shield=Get-Content -Raw (Join-Path $project 'Assets/Script/Armas/Escudo.cs')
$shieldMethod=Extract $shield '    internal static bool TentarInterceptarGolpe(' '    private void ProcessarBloqueio('
$shieldMethod+=Extract $shield '    private void ReduzirVidaDoEscudo()' '    private void QuebrarEscudo()'
$shieldMethod+=Extract $shield '    private bool RegistrarContato(' '    private void NormalizarVida()'
$math=Extract $motor '    internal static bool SoloTransitavel(' '    private bool PassoSeguro('
$code=@'
using System;
using System.Collections;
using System.Collections.Generic;
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3(0,1,0);public float sqrMagnitude=>x*x+y*y+z*z;public Vector3 normalized=>this*(1f/(float)Math.Sqrt(sqrMagnitude));public static Vector3 operator*(Vector3 v,float n)=>new Vector3(v.x*n,v.y*n,v.z*n);public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n){float d=(v.x*n.x+v.y*n.y+v.z*n.z)/n.sqrMagnitude;return new Vector3(v.x-n.x*d,v.y-n.y*d,v.z-n.z*d);}}
public static class Mathf {public const float Deg2Rad=(float)Math.PI/180;public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float n,float a,float b)=>Math.Min(b,Math.Max(a,n));public static float Cos(float n)=>(float)Math.Cos(n);}
public class GameObject {public bool activeInHierarchy=true;public void SetActive(bool v){activeInHierarchy=v;}}
public class Component {}
public class StatusPlayer {}
public class Transform {
 public Vector3 position;public object rotation;
 public T GetComponentInParent<T>() where T:class => null;
 public T GetComponentInChildren<T>(bool includeInactive) where T:class => new StatusPlayer() as T;
}
public class Canvas {public GameObject gameObject=new GameObject();}
public class Coroutine {}
public class Collider {
 public bool player, enabled=true;
 public GameObject gameObject=new GameObject();
 public Escudo shield;
 public T GetComponentInParent<T>() where T:class => shield as T;
}
public class Escudo {
 public int blocks, vidaAtual=3;
 private int desgastePorDano=1;
 private readonly Dictionary<Collider,(Component fonte,int quantidade)> contatosDeDano=new();
 private readonly List<Collider> contatosInvalidos=new();
 public static bool TestarContatos(){
   var escudo=new Escudo();var fonte=new Component();var a=new Collider();var b=new Collider();
   if (!escudo.RegistrarContato(a,fonte) || escudo.RegistrarContato(a,fonte) ||
       escudo.RegistrarContato(b,fonte)) return false;
   escudo.EncerrarContato(a);escudo.EncerrarContato(b);
   if (escudo.RegistrarContato(b,fonte)) return false;
   escudo.EncerrarContato(a);escudo.EncerrarContato(b);
   if (!escudo.RegistrarContato(b,fonte)) return false;
   b.enabled=false;
   return escudo.RegistrarContato(a,fonte);
 }
 public bool Quebrado=>vidaAtual<=0;
 public bool Held=true, Qualified=true;
 public class Sorteio {public int defesa=3;}
 public Sorteio sorteio=new Sorteio();
 public bool EstaProtegendoPlayer(Transform alvo)=>Held && alvo!=null && !Quebrado;
 public static bool IgnorarNoAtaque(Collider contato,Transform alvo)=>contato.shield!=null && !contato.shield.EstaProtegendoPlayer(alvo);
 private bool PertenceAoDonoAtual(GameObject atacante)=>false;
 private bool PodeUsar(StatusPlayer jogador)=>Qualified;
 private void GarantirSorteio(){}
 private void RegistrarImpacto(bool tocarSom){blocks++;ReduzirVidaDoEscudo();}
 public void AtualizarDurabilidadeVisual(){}
 private void QuebrarEscudo(){}
'@+$shieldMethod+@'
}
public class SlimeMovimento {public bool PreservarArcoInvestida;public float RaioMundial=>.45f;public float AlturaMundial=>1f;public Vector3 CentroRelativo=>new Vector3(0,.5f,0);public bool dead;public int resumes;public void Suspender(bool v){if(!v)resumes++;}public void Morrer(){dead=true;}}
public class SlimeAnimacao {public int restarts;public float deathTime=1.5f;public void ReiniciarAcaoConfigurada(string s){restarts++;}public float ObterTempoMorteConfigurado()=>deathTime;}
public class RespawnMonstro {public static RespawnMonstro Instancia=new RespawnMonstro();public int scheduled;public bool fail;public Transform[] patrol;public void AgendarRespawnVerde(string id,Vector3 p,object r,Transform[] t,int key){if(fail)throw new Exception("respawn callback");scheduled++;patrol=t;}public void AgendarRespawn(string id,Vector3 p,object r,Transform[] t){scheduled++;}public void AgendarRespawnVermelho(string id,Vector3 p,object r,Transform[] t,int key,float radius,float height,Vector3 center){AgendarRespawnVerde(id,p,r,t,key);}}
public static class Debug {public static int errors;public static void LogException(Exception e,object o){errors++;}}
public class Host {
 enum EstadoSlime{Parado,Patrulhando,Observando,Alerta,Perseguindo,Atacando,TomandoDano,Morto}
 SlimeAnimacao animacao;SlimeMovimento movimentoVerde;bool morto,impactoAtaqueResolvido,investidaAtaqueEmAndamento;int vidaAtual=10;Transform ultimoPlayerResponsavel;Coroutine rotinaDano,rotinaInvestidaAtaque;EstadoSlime estadoAtual,estadoAntesDano;object somDano;float volumeDano;bool esconderCanvasAoMorrer=true;Canvas canvasVida=new Canvas();float tempoParaMorrer=.5f;GameObject gameObject=new GameObject();Transform transform=new Transform();string idRespawnMonstro="SlimeVerde";Transform[] pontosPatrulha=new[]{new Transform()};List<GameObject> pontosAutoProprios=new List<GameObject>();
 public int xp,rein,missions,loot,missionLoot,destroys,stops,playerHits,blocks;int danoAtaque=3;Transform alvoPlayer=new Transform();public bool failXp,failRein;public static Host New(bool green=true){var h=new Host();h.movimentoVerde=green?new SlimeMovimento():null;return h;}
 void AtualizarBarraVida(){}void TocarSomUmaVez(object s,float v){}void PararMovimento(){}void PararSomAndar(){}void TocarSomMorrer(){}void AtualizarAnimatorPorEstado(){}void AtualizarAudioPorEstado(){}void RetomarAgentDepoisInvestida(){}void StopCoroutine(Coroutine c){stops++;}Coroutine StartCoroutine(IEnumerator r){r.MoveNext();return new Coroutine();}IEnumerator RotinaDano(){MudarEstado(EstadoSlime.TomandoDano);yield return null;}void Destroy(GameObject g,float t){destroys++;}int GetInstanceID()=>1;
 float ObterDano()=>danoAtaque;
 bool ColliderBloqueiaMesmoSendoDoPlayer(Collider c)=>c.shield!=null && c.shield.Held;
 bool PertenceAoPlayerAlvo(Collider c,Transform p)=>c.player;
 float danoAplicado;
 bool TentarAplicarDanoNoPlayer(Transform p,float d){if(d<=0f)return false;playerHits++;danoAplicado=d;return true;}
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
 c(Escudo.TestarContatos(),"Compound contacts count once and re-entry counts immediately after separation");
 var shield=new Escudo();
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.ResolverImpactoCollider(new Collider{shield=shield});
 c(shield.blocks==1&&h.playerHits==0,"Defense equal to attack absorbs it");
 h=New();h.danoAtaque=8;h.estadoAtual=EstadoSlime.Atacando;h.ResolverImpactoCollider(new Collider{shield=shield});
 c(shield.blocks==2&&h.playerHits==1&&h.danoAplicado==5,"Only damage above shield defense reaches the player");
 c(shield.vidaAtual==1,"Two valid hits cost two durability even with the same attacker");
 shield.Qualified=false;
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.ResolverImpactoCollider(new Collider{shield=shield});
 c(shield.blocks==2&&h.danoAplicado==3,"Unmet requirement leaves full incoming attack");
 shield.Qualified=true;
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.ResolverImpactoCollider(new Collider{shield=shield});
 c(shield.Quebrado && h.playerHits==0,"Last durability point still blocks its hit");
 shield.Held=false;
 c(!Escudo.TentarInterceptarGolpe(new Collider{shield=shield},new GameObject(),new Transform(),3,out float restante)&&restante==3,
   "Unheld shield does not provide defense");
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.ResolverImpactoCollider(new Collider{player=true});h.ResolverImpactoCollider(new Collider{player=true});c(h.playerHits==1,"Multiple target colliders resolve a lunge only once");
 h=New();h.estadoAtual=EstadoSlime.Atacando;h.AplicarDanoRecebido(1,null);h.ResolverImpactoCollider(new Collider{player=true});c(h.playerHits==0,"Canceled lunge cannot apply late damage");h.Morrer();h.ResolverImpactoCollider(new Collider{player=true});c(h.playerHits==0,"Dead slime cannot apply attack damage");return n;}
}
'@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0649,0414'
Write-Output ("PASS: "+[Host]::Run()+" verificacoes de metodos reais de dano, interrupcao, morte e matematica do solo. Host simulado; fisica Unity/Quest nao executadas.")
