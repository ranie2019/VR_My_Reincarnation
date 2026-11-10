$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Armas/Espada.cs')
$manoplas=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/AtributosManoplas.cs')
function Get-Body([string]$text,[string]$signature) {
 $start=$text.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $open=$text.IndexOf('{',$start);$depth=1;$end=$open+1
 while($depth -gt 0){if($text[$end] -eq '{'){$depth++}elseif($text[$end] -eq '}'){$depth--};$end++}
 return $text.Substring($start,$end-$start)
}
$start=$source.IndexOf('    [Serializable]')
$end=$source.IndexOf('    [Header("Identidade atual do popup")]', $start)
$types=$source.Substring($start,$end-$start)
$flecha=Get-Content -Raw (Join-Path $project 'Assets/Script/Armas/Flecha.cs')
$a=$manoplas.IndexOf('    internal static int SortearClasse(')
$b=$manoplas.IndexOf('    private string ObterNomePrefab()', $a)
$code=@'
using System;using System.Collections;using System.Collections.Generic;using System.Globalization;using UnityEngine;
namespace UnityEngine {
 public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
 public class HideInInspector:Attribute {}
 public class InspectorNameAttribute:Attribute {public InspectorNameAttribute(string s){}}
 public class Collision {} public class Collider {}
 public class MinAttribute:Attribute {public MinAttribute(float f){}}
 public static class Random {static System.Random r=new System.Random(91234);public static float value=>(float)r.NextDouble();public static int Range(int min,int max)=>r.Next(min,max);}
 public static class Debug {public static void LogError(string s,object c){}}
 public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float v,float a,float b)=>Math.Min(b,Math.Max(a,v));}
}
public enum ClasseManopla {Comum,Incomum,Raro,Epico,Unico,Lendario,Mitico,Divino}
public enum AfinidadeDivinaManopla {Nenhuma,Luz,Trevas}
public enum TipoAtributoManopla {Forca,Constituicao,Agilidade,Velocidade,Inteligencia,Espirito,Destreza,Vigor,Percepcao,Resistencia,Carisma,Sorte,Mana,Critico,Defesa,HP}
public class AtributosManoplas {
 public class FaixaAtributo {public TipoAtributoManopla atributo;public int minimo=1,maximo=5;}
 public class ConfiguracaoClasse {public ClasseManopla classe;public float chance;public int quantidadeMinima,quantidadeMaxima=1;public List<FaixaAtributo> atributos=new List<FaixaAtributo>();}
 public class ValorAtributo {public TipoAtributoManopla atributo;public int valor;}
'@+$manoplas.Substring($a,$b-$a)+@'
}
'@+'public static class Flecha {'+(Get-Body $flecha '    internal static T SortearClassePonderada<T>(')+'}'+@'
public interface IXRHoverInteractor {}
public interface IXRSelectInteractor {}
namespace UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals {public interface ICurveInteractionDataProvider {bool isActive{get;}}}
public class NearFarInteractor:IXRHoverInteractor,UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.ICurveInteractionDataProvider {public bool isActive{get;set;}}
public class XRRayInteractor:IXRHoverInteractor,IXRSelectInteractor {}
public class XRGazeInteractor:XRRayInteractor {}
public class XRDirectInteractor:IXRHoverInteractor,IXRSelectInteractor {}
public class XRSocketInteractor:IXRHoverInteractor,IXRSelectInteractor {}
public class XRGrabInteractable {public List<IXRSelectInteractor> interactorsSelecting=new List<IXRSelectInteractor>();}
public class TMP_Text {public string text;public bool enabled=true;}
public class Transform {public Transform parent;public bool IsChildOf(Transform other){for(var t=parent;t!=null;t=t.parent)if(t==other)return true;return false;}}
public class GameObject {public bool activeInHierarchy=>activeSelf;public bool activeSelf=true;public Transform transform=new Transform();public void SetActive(bool active){activeSelf=active;}}
public class StatusPlayer {public int forca;public int GetForcaTotal()=>forca;public Dictionary<TipoAtributoManopla,int> valores=new Dictionary<TipoAtributoManopla,int>();private int V(TipoAtributoManopla t)=>valores.ContainsKey(t)?valores[t]:0;public int GetConstituicaoTotal()=>V(TipoAtributoManopla.Constituicao);public int GetAgilidadeTotal()=>V(TipoAtributoManopla.Agilidade);public int GetVelocidadeTotal()=>V(TipoAtributoManopla.Velocidade);public int GetInteligenciaTotal()=>V(TipoAtributoManopla.Inteligencia);public int GetEspiritoTotal()=>V(TipoAtributoManopla.Espirito);public int GetDestrezaTotal()=>V(TipoAtributoManopla.Destreza);public int GetVigorTotal()=>V(TipoAtributoManopla.Vigor);public int GetPercepcaoTotal()=>V(TipoAtributoManopla.Percepcao);public int GetResistenciaTotal()=>V(TipoAtributoManopla.Resistencia);public int GetCarismaTotal()=>V(TipoAtributoManopla.Carisma);public int GetSorteTotal()=>V(TipoAtributoManopla.Sorte);public int GetCriticoTotal()=>V(TipoAtributoManopla.Critico);public int GetDefesaTotal()=>V(TipoAtributoManopla.Defesa);public int GetVidaMaxima()=>V(TipoAtributoManopla.HP);public int GetManaMaxima()=>V(TipoAtributoManopla.Mana);}
public static class EstadoItemInventario {public static bool EstaNoInventario(Espada e)=>e.stored;}
public static class JsonUtility {
 static System.Text.Json.JsonSerializerOptions options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
 public static string ToJson<T>(T obj)=>System.Text.Json.JsonSerializer.Serialize(obj,options);
 public static T FromJson<T>(string json)=>System.Text.Json.JsonSerializer.Deserialize<T>(json,options);
}
public class Espada {
'@+$types+@'
 private static List<Espada> espadasAtivas=new List<Espada>();
 private List<ConfiguracaoClasseEspada> classes=CriarClassesPadrao();private EstadoEspada resultado;
 private bool resultadoDestaInstancia,migracaoLegadaPendente,quebrada;public bool isActiveAndEnabled=true,stored;public StatusPlayer owner;
 private TipoAtributoManopla atributoRestricao=TipoAtributoManopla.Forca;
 private string estadoJsonAnteriorSemRestricao;
 private XRGrabInteractable grabInteractable;
 private object fechamentoPopup;private int fechamentos;
 private void FecharPopup(){fechamentos++;popup.SetActive(false);fechamentoPopup=null;}
 private void PosicionarPopup(){}
 private GameObject grupoRestricao,popup=new GameObject();
 private TMP_Text textoRotuloRestricao,textoAtributoRestricao,textoValorRestricao,textoMensagemRestricao;
 private string rotuloRestricaoPopup="Restricao:",mensagemAbaixoMinimo="Seu valor esta abaixo do minimo";
 private int danoEspada;protected int valorMinimoParaUsar=10;private float vidaAtual,vidaMaxima,ultimaVidaPublicada=float.NaN;private double vidaPrecisa;
 private void AtualizarPopup(){} private void AtualizarTextoDurabilidade(bool criarSeNecessario=false){} private void TocarSomQuebra(){}
 private class ItemPersistente { public bool consumido; public void RegistrarConsumoPermanente(){consumido=true;} }
 private ItemPersistente persistente=new ItemPersistente();
 private T GetComponent<T>() where T:class => persistente as T;
 private GameObject gameObject=new GameObject();
 private bool destruido,destruirAoZerarDurabilidade=true;
 private void Destroy(GameObject item){destruido=item==gameObject;}
 private int danoLegadoConfigurado;private float vidaLegadaConfigurada;private bool configuracaoLegadaCapturada;
 private List<IEnumerator> coroutines=new List<IEnumerator>();private void StartCoroutine(IEnumerator r){coroutines.Add(r);}
 public void AplicarDesgasteCombate(float v)=>AplicarDesgaste(v);
 protected virtual void OnCollisionEnter(Collision c){} protected virtual void OnTriggerEnter(Collider c){} protected virtual int ObterDanoCalculado()=>1;
 public StatusPlayer GetStatusPlayerDonoAtual()=>owner;
'@
$code+=($source.Split([char]10) | Where-Object {$_.StartsWith('    protected virtual bool DestruirAoQuebrar =>') -or $_.StartsWith('    public bool EstaQuebrada =>') -or $_.StartsWith('    public bool Gerado =>') -or $_.StartsWith('    public TipoAtributoManopla AtributoExigido =>') -or $_.StartsWith('    public int RequisitoExigido =>')})
$code+=($source.Split([char]10) | Where-Object {$_.StartsWith('    public string ExportarEstadoJson() =>')})
foreach($sig in @('    private static List<ConfiguracaoClasseEspada> CriarClassesPadrao()',
 '    protected virtual bool DefinirRestricaoNova(', '    public bool GarantirGeracao()', '    internal static List<AtributosManoplas.ValorAtributo> SortearValoresAtributos(', '    internal static bool ConfiguracaoValida(', '    internal static bool FaixaSorteioValida(', '    public bool PodeUsar(', '    internal static bool AtendeRestricaoAtributo(', '    internal static int ObterAtributoTotal(', '    private void AtualizarRestricaoPopup(', '    private void DefinirVisibilidadeRestricao(', '    internal static bool EhLaserDoPopup(', '    private bool EstaSendoSegurada()', '    protected void LateUpdate()',
 '    public static EstadoEspada LerEValidarEstadoJson(', '    public void RestaurarEstadoJson(',
 '    public int ObterValor(', '    public static int ObterBonusEmpunhado(', '    private void AplicarDesgaste(',
 '    private void QuebrarEspada()', '    public void RepararCompleto()', '    public void RestaurarEstadoLegado(', '    private IEnumerator FinalizarMigracaoLegada()', '    private void CapturarConfiguracaoLegada()')) {$code+=Get-Body $source $sig}
$code+=@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};
 var all=new List<Espada>();var results=new HashSet<string>();
 for(int i=0;i<5;i++){
  var e=new Espada();e.classes=new List<ConfiguracaoClasseEspada>{new ConfiguracaoClasseEspada{classe=ClasseManopla.Raro,chance=100,danoMinimo=2,danoMaximo=10,vidaMinima=10,vidaMaxima=30,quantidadeMinima=1,quantidadeMaxima=1,atributos=new List<AtributosManoplas.FaixaAtributo>{new AtributosManoplas.FaixaAtributo{atributo=TipoAtributoManopla.Forca,minimo=2,maximo=5}}}};
  c(e.GarantirGeracao(),"Generation failed");c(e.danoEspada>=2&&e.danoEspada<=10&&e.vidaMaxima>=10&&e.vidaMaxima<=30,"Range violation");c(e.resultado.classe==ClasseManopla.Raro&&e.resultado.atributos.Count==1,"Wrong class/count");c(e.ObterValor(TipoAtributoManopla.Forca)>=2&&e.ObterValor(TipoAtributoManopla.Forca)<=5,"Attribute range violation");
  string before=e.ExportarEstadoJson();for(int j=0;j<10;j++)e.GarantirGeracao();c(before==e.ExportarEstadoJson(),"Reopened item rerolled");results.Add(before);all.Add(e);
 }
 c(results.Count>1,"Five seeded creations shared a roll");c(!object.ReferenceEquals(all[0].resultado.atributos,all[1].resultado.atributos),"Shared attribute collection");
 var restored=new Espada();restored.vidaAtual=7.5f;restored.RestaurarEstadoJson(all[0].ExportarEstadoJson());restored.GarantirGeracao();c(restored.ExportarEstadoJson()==all[0].ExportarEstadoJson(),"Save changed random state");c(restored.vidaAtual==7.5f,"Restoring roll healed durability");
 var copy=new Espada();copy.resultado=all[0].resultado;copy.GarantirGeracao();c(!object.ReferenceEquals(copy.resultado,all[0].resultado),"New clone inherited prefab roll");
 var invalid=new Espada();foreach(var cfg in invalid.classes)cfg.chance=0;c(!invalid.GarantirGeracao(),"All-zero chances accepted");
 c(!FaixaSorteioValida(8,2,0),"Inverted damage range accepted");c(!FaixaSorteioValida(0,50,1),"Zero life range accepted");
 bool rejected=false;var state=all[0].ExportarEstadoJson().Replace("\"versao\":1","\"versao\":2");try{LerEValidarEstadoJson(state);}catch(System.IO.InvalidDataException){rejected=true;}c(rejected,"Unknown save version accepted");
 var worn=new Espada();worn.vidaAtual=10;for(int i=0;i<60;i++)worn.AplicarDesgaste(.5d/60d);c(Math.Abs(worn.vidaAtual-9.5f)<.0001f,"Fractional wear lost precision");worn.stored=true;worn.AplicarDesgaste(3);c(Math.Abs(worn.vidaAtual-9.5f)<.0001f,"Stored item wore down");worn.stored=false;worn.vidaAtual=20;worn.AplicarDesgaste(1);c(worn.vidaAtual==19,"Repair did not reset precision baseline");worn.AplicarDesgaste(100);c(worn.quebrada&&worn.vidaAtual==0,"Zero life did not break");
 var p=new StatusPlayer();all[0].owner=p;all[1].owner=p;espadasAtivas=new List<Espada>{all[0],all[1]};int sum=all[0].ObterValor(TipoAtributoManopla.Forca)+all[1].ObterValor(TipoAtributoManopla.Forca);c(ObterBonusEmpunhado(p,TipoAtributoManopla.Forca)==sum,"Two held swords not summed");c(ObterBonusEmpunhado(new StatusPlayer(),TipoAtributoManopla.Forca)==0,"Bonus leaked to other player");all[1].stored=true;c(ObterBonusEmpunhado(p,TipoAtributoManopla.Forca)==all[0].ObterValor(TipoAtributoManopla.Forca),"Inventory sword grants bonus");all[0].owner=null;c(ObterBonusEmpunhado(p,TipoAtributoManopla.Forca)==0,"Dropped sword grants bonus");
 var legacy=new Espada();legacy.danoEspada=2;legacy.vidaMaxima=50;legacy.RestaurarEstadoLegado(19.5f,50);legacy.GarantirGeracao();c(!legacy.Gerado&&legacy.ExportarEstadoJson()==null,"Legacy extras changed before verification");foreach(var r in legacy.coroutines)while(r.MoveNext()){}c(legacy.Gerado&&legacy.danoEspada==2&&legacy.vidaMaxima==50&&legacy.vidaAtual==19.5f,"Legacy migration rerolled or healed");c(legacy.resultado.atributos.Count==0,"Legacy migration invented bonuses");
 c(!AtendeRestricaoAtributo(9,0,10),"Below minimum strength accepted");c(AtendeRestricaoAtributo(10,0,10),"Equal minimum strength rejected");c(AtendeRestricaoAtributo(11,0,10),"Above minimum strength rejected");c(!AtendeRestricaoAtributo(24,15,10),"Weapon grants its own requirement");c(AtendeRestricaoAtributo(25,15,10),"Base plus worn equipment should qualify");
 var requirementWeapon=all[0];requirementWeapon.resultado.atributoExigido=TipoAtributoManopla.Forca;requirementWeapon.resultado.requisitoExigido=10;requirementWeapon.owner=p;int own=requirementWeapon.ObterValor(TipoAtributoManopla.Forca);p.forca=9+own;c(!requirementWeapon.PodeUsar(p),"Real weapon self-qualified");p.forca=10+own;c(requirementWeapon.PodeUsar(p),"Real weapon threshold failed");c(!requirementWeapon.PodeUsar(null),"Unowned weapon met strength requirement");
 var fixedRange=new Espada();fixedRange.classes=new List<ConfiguracaoClasseEspada>{new ConfiguracaoClasseEspada{chance=100,danoMinimo=7,danoMaximo=7,vidaMinima=37,vidaMaxima=37,quantidadeMaxima=0}};c(fixedRange.GarantirGeracao()&&fixedRange.danoEspada==7&&fixedRange.vidaAtual==37&&fixedRange.vidaMaxima==37,"Fixed inclusive range or initial durability incorrect");
 var espadaReparavel=new Espada{vidaAtual=1,destruirAoZerarDurabilidade=false};espadaReparavel.AplicarDesgaste(1);
 c(espadaReparavel.EstaQuebrada&&espadaReparavel.gameObject.activeSelf&&!espadaReparavel.destruido&&!espadaReparavel.persistente.consumido,"Repairable sword was destroyed");
 var generic=new Espada{owner=p,valorMinimoParaUsar=56};
 foreach(TipoAtributoManopla tipo in Enum.GetValues(typeof(TipoAtributoManopla))){generic.atributoRestricao=tipo;p.forca=55;p.valores[tipo]=55;c(!generic.PodeUsar(p),"Below restriction accepted: "+tipo);p.forca=56;p.valores[tipo]=56;c(generic.PodeUsar(p),"Equal restriction rejected: "+tipo);}
 generic.grupoRestricao=new GameObject();generic.grupoRestricao.transform.parent=generic.popup.transform;generic.atributoRestricao=TipoAtributoManopla.Velocidade;generic.textoRotuloRestricao=new TMP_Text();generic.textoAtributoRestricao=new TMP_Text();generic.textoValorRestricao=new TMP_Text();generic.textoMensagemRestricao=new TMP_Text();p.valores[TipoAtributoManopla.Velocidade]=55;generic.AtualizarRestricaoPopup(p);
 c(generic.textoRotuloRestricao.text=="Restricao:"&&generic.textoAtributoRestricao.text=="Velocidade"&&generic.textoValorRestricao.text=="56","Restriction fields not independent");c(generic.textoMensagemRestricao.text=="Seu valor esta abaixo do minimo","Missing below-minimum message");p.valores[TipoAtributoManopla.Velocidade]=56;generic.AtualizarRestricaoPopup(p);c(generic.textoMensagemRestricao.text=="","Satisfied restriction still warns");generic.AtualizarRestricaoPopup(null);c(generic.textoMensagemRestricao.text=="","Unknown player should not show a false warning");
 p.valores[TipoAtributoManopla.Velocidade]=55;generic.AtualizarRestricaoPopup(p);c(generic.grupoRestricao.activeSelf&&generic.textoRotuloRestricao.enabled&&generic.textoAtributoRestricao.enabled&&generic.textoValorRestricao.enabled&&generic.textoMensagemRestricao.enabled,"Blocked player must see entire restriction group");
 p.valores[TipoAtributoManopla.Velocidade]=56;generic.AtualizarRestricaoPopup(p);c(!generic.grupoRestricao.activeSelf&&!generic.textoRotuloRestricao.enabled&&!generic.textoAtributoRestricao.enabled&&!generic.textoValorRestricao.enabled&&!generic.textoMensagemRestricao.enabled,"Qualified player must hide whole group and four texts");
 p.valores[TipoAtributoManopla.Velocidade]=55;generic.AtualizarRestricaoPopup(p);c(generic.grupoRestricao.activeSelf&&generic.textoValorRestricao.enabled,"Attribute drop must restore group while popup remains open");
 generic.AtualizarRestricaoPopup(null);c(!generic.grupoRestricao.activeSelf,"Unknown player should not show stale restriction");
 generic.grupoRestricao=null;generic.AtualizarRestricaoPopup(p);c(generic.textoRotuloRestricao.enabled,"No-group development model fallback must show text");p.valores[TipoAtributoManopla.Velocidade]=60;generic.AtualizarRestricaoPopup(p);c(!generic.textoRotuloRestricao.enabled&&!generic.textoValorRestricao.enabled,"No-group fallback must hide texts");
 generic.grupoRestricao=generic.popup;generic.AtualizarRestricaoPopup(p);c(generic.popup.activeSelf,"Misconfigured group must never disable whole popup");
 c(EhLaserDoPopup(new NearFarInteractor{isActive=true}),"NearFar laser must open popup");c(!EhLaserDoPopup(new NearFarInteractor{isActive=false}),"NearFar near hand must not open popup");
 c(EhLaserDoPopup(new XRRayInteractor()),"Laser should open popup");c(!EhLaserDoPopup(new XRDirectInteractor()),"Direct hand hover should not open popup");c(!EhLaserDoPopup(new XRSocketInteractor()),"Socket hover should not open popup");c(!EhLaserDoPopup(new XRGazeInteractor()),"Head gaze should not open popup");
 var hoverWeapon=new Espada();c(!hoverWeapon.EstaSendoSegurada(),"No grab component is not a held weapon");hoverWeapon.grabInteractable=new XRGrabInteractable();hoverWeapon.grabInteractable.interactorsSelecting.Add(new XRSocketInteractor());c(!hoverWeapon.EstaSendoSegurada(),"Stored weapon must remain inspectable by laser");
 hoverWeapon.grabInteractable.interactorsSelecting.Add(new XRDirectInteractor());c(hoverWeapon.EstaSendoSegurada(),"Hand selection must block popup");hoverWeapon.LateUpdate();c(!hoverWeapon.popup.activeSelf&&hoverWeapon.fechamentos==1,"Missed grab event must still close popup in LateUpdate");
 hoverWeapon.grabInteractable.interactorsSelecting.RemoveAt(1);hoverWeapon.LateUpdate();c(!hoverWeapon.popup.activeSelf,"Releasing weapon must not open popup without laser hover");hoverWeapon.grabInteractable.interactorsSelecting.Add(new XRRayInteractor());hoverWeapon.popup.SetActive(true);hoverWeapon.LateUpdate();c(!hoverWeapon.popup.activeSelf,"Distance grab must also block popup");
 foreach(ClasseManopla classe in Enum.GetValues(typeof(ClasseManopla))){
  var req=new Espada{atributoRestricao=TipoAtributoManopla.Velocidade,valorMinimoParaUsar=16};
  foreach(var cfg in req.classes)cfg.chance=cfg.classe==classe?100:0;
  c(req.GarantirGeracao()&&req.AtributoExigido==TipoAtributoManopla.Velocidade&&req.RequisitoExigido==16,"Sword did not use the configured requirement: "+classe);
  string saved=req.ExportarEstadoJson();
  var loaded=new Espada{atributoRestricao=TipoAtributoManopla.Forca,valorMinimoParaUsar=30};
  loaded.RestaurarEstadoJson(saved);
  c(loaded.RequisitoExigido==16&&loaded.AtributoExigido==req.AtributoExigido&&loaded.ExportarEstadoJson()==saved,"Reload changed the saved requirement");
 }
 var livre=new Espada{valorMinimoParaUsar=0};
 c(livre.GarantirGeracao()&&livre.PodeUsar(null),"Zero requirement should allow use");
 var higher=new Espada{atributoRestricao=TipoAtributoManopla.Resistencia,valorMinimoParaUsar=23};foreach(var cfg in higher.classes)cfg.chance=cfg.classe==ClasseManopla.Epico?100:0;c(higher.GarantirGeracao()&&higher.AtributoExigido==TipoAtributoManopla.Resistencia&&higher.RequisitoExigido==23,"Higher rarity lost manual policy");
 var invalidSwordReq=new Espada{valorMinimoParaUsar=-1};c(!invalidSwordReq.GarantirGeracao()&&!invalidSwordReq.Gerado,"Invalid requirement published partial item");
 var invalidReq=new Espada.EstadoEspada{gerado=true,vidaMaxima=20,restricaoDefinida=true,atributoExigido=(TipoAtributoManopla)999,requisitoExigido=5};bool badReq=false;try{LerEValidarEstadoJson(JsonUtility.ToJson(invalidReq));}catch(System.IO.InvalidDataException){badReq=true;}c(badReq,"Corrupt saved requirement accepted");
 string oldJson="{\"versao\":1,\"gerado\":true,\"classe\":0,\"afinidade\":0,\"dano\":3,\"vidaMaxima\":50,\"atributos\":[]}";
 var old=new Espada{valorMinimoParaUsar=10};old.RestaurarEstadoJson(oldJson);c(old.RequisitoExigido==10&&old.AtributoExigido==TipoAtributoManopla.Forca&&old.ExportarEstadoJson()==oldJson,"Legacy save changed extras during verification");
 var staff=new CajadoPopup();c(staff.GarantirGeracao(),"Staff generation failed");c(staff.AtributoExigido==TipoAtributoManopla.Inteligencia||staff.AtributoExigido==TipoAtributoManopla.Espirito||staff.AtributoExigido==TipoAtributoManopla.Mana||staff.AtributoExigido==TipoAtributoManopla.Percepcao,"Staff rolled physical requirement");
 var campoRestricao = typeof(CajadoPopup).GetField("atributoRestricaoMagica", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
 foreach (var tipo in new[]{TipoAtributoManopla.Mana,TipoAtributoManopla.Inteligencia,TipoAtributoManopla.Espirito,TipoAtributoManopla.Percepcao})
 {
  var configurado = new CajadoPopup();
  campoRestricao.SetValue(configurado, Enum.Parse(campoRestricao.FieldType, tipo.ToString()));
  c(configurado.GarantirGeracao() && configurado.AtributoExigido == tipo, "Staff ignored the selected requirement: " + tipo);
  configurado.RestaurarEstadoJson(staff.ExportarEstadoJson());
  c(configurado.AtributoExigido == staff.AtributoExigido, "Staff configuration changed saved requirement");
 }
 var tunedStaff=new CajadoPopup();
 ((Espada)tunedStaff).classes=new List<ConfiguracaoClasseEspada>{new ConfiguracaoClasseEspada{classe=ClasseManopla.Raro,chance=100,danoMinimo=7,danoMaximo=7,vidaMinima=31,vidaMaxima=31,quantidadeMaxima=0}};
 c(tunedStaff.GarantirGeracao()&&((Espada)tunedStaff).danoEspada==7&&((Espada)tunedStaff).vidaAtual==31,"Staff ignored the selected class ranges");
 p.valores[tunedStaff.AtributoExigido]=9;c(!tunedStaff.PodeLancarMagia(p),"Staff accepted a player below the requirement");
 p.valores[tunedStaff.AtributoExigido]=10;c(tunedStaff.PodeLancarMagia(p),"Staff rejected a player at the requirement");
 tunedStaff.RegistrarMagiaLancada();c(((Espada)tunedStaff).vidaAtual==30,"Staff cast did not wear exactly one point");
 tunedStaff.desgastePorMagiaLancada=3;
 tunedStaff.RegistrarMagiaLancada();c(((Espada)tunedStaff).vidaAtual==27,"Staff ignored configured wear");
 ((Espada)tunedStaff).vidaAtual=1;tunedStaff.RegistrarMagiaLancada();
 c(!tunedStaff.PodeLancarMagia(p)&&((Espada)tunedStaff).destruido&&((Espada)tunedStaff).persistente.consumido,"Default staff break did not destroy and consume the item");
 var reparavel=new CajadoPopup{destruirQuandoVidaZerar=false};
 c(reparavel.GarantirGeracao(),"Repairable staff generation failed");
 p.valores[reparavel.AtributoExigido]=10;
 ((Espada)reparavel).vidaAtual=1;reparavel.RegistrarMagiaLancada();
 c(!reparavel.PodeLancarMagia(p)&&((Espada)reparavel).gameObject.activeSelf&&!((Espada)reparavel).destruido&&!((Espada)reparavel).persistente.consumido,"Broken repairable staff did not remain in the world");
 reparavel.RepararCompleto();
 c(reparavel.PodeLancarMagia(p),"Repaired staff could not launch a spell");
 var staffSaved=staff.ExportarEstadoJson();var staffLoaded=new CajadoPopup();staffLoaded.RestaurarEstadoJson(staffSaved);c(staffLoaded.ExportarEstadoJson()==staffSaved&&staffLoaded.AtributoExigido==staff.AtributoExigido,"Staff lost individual roll on reload");
 for(int k=0;k<100;k++)staff.GarantirGeracao();c(staff.ExportarEstadoJson()==staffSaved,"Staff popup rerolled item");
 c((int)typeof(CajadoPopup).GetMethod("ObterDanoCalculado",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(staff,null)==0,"Staff gained sword contact damage");
 var bow=new Arcopopup();
 var bowBase=(Espada)bow;
 bowBase.atributoRestricao=TipoAtributoManopla.Destreza;
 bowBase.valorMinimoParaUsar=12;
 bowBase.classes=new List<ConfiguracaoClasseEspada>{new ConfiguracaoClasseEspada{classe=ClasseManopla.Raro,chance=100,danoMinimo=4,danoMaximo=4,vidaMinima=67,vidaMaxima=67,quantidadeMaxima=0}};
 c(bow.GarantirGeracao()&&bowBase.danoEspada==4&&bowBase.vidaMaxima==67,"Bow ignored its class ranges");
 c(bow.AtributoExigido==TipoAtributoManopla.Destreza&&bow.RequisitoExigido==12,"Bow ignored its configured requirement");
 var bowSaved=bow.ExportarEstadoJson();var bowLoaded=new Arcopopup();bowLoaded.RestaurarEstadoJson(bowSaved);c(bowLoaded.ExportarEstadoJson()==bowSaved,"Bow persistence changed roll");
 for(int k=0;k<100;k++)bow.GarantirGeracao();c(bow.ExportarEstadoJson()==bowSaved,"Bow rerolled on popup");
 c((int)typeof(Arcopopup).GetMethod("ObterDanoCalculado",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(bow,null)==0,"Bow gained sword contact damage");
 return n;}
}
'@
$staff=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/CajadoPopup.cs')
$staff=$staff -replace '(?m)^using UnityEngine;\r?\n',''
$staff=$staff -replace '(?m)^[ \t]*\[[^\r\n]+\]',''
$code+=$staff
$bow=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/Arcopopup.cs')
$bow=$bow -replace '(?m)^using UnityEngine;\r?\n',''
$bow=$bow -replace '(?m)^[ \t]*\[[^\r\n]+\]',''
$code+=$bow
Add-Type -TypeDefinition $code
Write-Output ('PASS: '+[Espada]::Run()+' verificacoes reais de sorteio, save, desgaste e bonus; host simulado.')
