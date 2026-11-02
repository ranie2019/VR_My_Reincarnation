$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$receiver=(Get-Content -Raw (Join-Path $project 'Assets/Script/Armas/ReceptorDanoInimigo.cs')) -replace '(?m)^using .*;\s*',''
$arrow=Get-Content -Raw (Join-Path $project 'Assets/Script/Armas/Flecha.cs')
$start=$arrow.IndexOf('    private void ProcessarColisao(')
$end=$arrow.IndexOf('    private bool TentarResolverAlvoValido(', $start)
$method=$arrow.Substring($start,$end-$start).Replace('private void ProcessarColisao','public void ProcessarColisao')
$stub=@'
namespace UnityEngine {
 public struct Vector3 {}
 public class Component {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:class=>gameObject.parts.OfType<T>().FirstOrDefault();}
 public class Transform:Component {public Transform parent;}
 public class GameObject {public Transform transform;public List<Component> parts=new List<Component>();public GameObject(){transform=new Transform {gameObject=this};}public T Add<T>() where T:Component,new(){var c=new T {gameObject=this};parts.Add(c);return c;}}
 public class Collider:Component {}
 public static class Mathf {public static int RoundToInt(float f)=>(int)Math.Round(f);}
}
public class SlimeIA:Component {public bool Morto;public int hits,total;public GameObject owner;public void ReceberDano(int n,GameObject o){hits++;total+=n;owner=o;}}
public class GoblinIA:Component {public bool Morto;public int hits,total;public GameObject owner;public void ReceberDano(int n,GameObject o){hits++;total+=n;owner=o;}}
public class EsperitoDeFogoIA:Component {public bool Morto;public int hits;public void ReceberDano(float f){hits++;}}
public class ExperienciaInimigo:Component {public GameObject owner;public void RegistrarPrimeiroAtacante(GameObject o){owner=o;}}
public static class EstadoItemInventario {public static bool EstaNoInventario(Component c)=>false;}
public class ArrowHost:Component {
 public bool foiLancada,colisaoProcessada,jaCausouDano,causarDanoUmaVez=true,destruirAoColidir;
 public float atrasoDestruirAposColisao,dano=10,multiplicadorDano=2;
 private bool DeveIgnorarColisao(Collider c)=>false;private void TocarSomColisao(Vector3 p){}private void InstanciarEfeitoColisao(Vector3 p,Vector3 n){}private void Destroy(GameObject o,float f){}
 private float ObterDano()=>dano*multiplicadorDano;
 private bool TentarResolverAlvoValido(Collider c,out GameObject g){var r=ReceptorDanoInimigo.Resolver(c);g=r?.gameObject;return g!=null;}
 private bool TentarAplicarDano(GameObject g,int d){var r=ReceptorDanoInimigo.Resolver(g.transform);if(r==null)return false;ReceptorDanoInimigo.Aplicar(r,d,gameObject);return true;}
'@
$tests=@'
}
public static class CombatTargetTests {
 static int checks;static void Check(bool v,string m){checks++;if(!v)throw new Exception(m);}
 static GameObject Child(GameObject p){var g=new GameObject();g.transform.parent=p.transform;return g;}
 public static string Run(){
  var root=new GameObject();var s=Child(root).Add<SlimeIA>();var g=Child(root).Add<GoblinIA>();var e=Child(root).Add<EsperitoDeFogoIA>();var xp=e.gameObject.Add<ExperienciaInimigo>();
  var gc=Child(g.gameObject).Add<Collider>();var gc2=Child(g.gameObject).Add<Collider>();var sc=Child(s.gameObject).Add<Collider>();var ec=Child(e.gameObject).Add<Collider>();
  Check(ReceptorDanoInimigo.Resolver(gc)==g,"Goblin resolved to sibling Slime");Check(ReceptorDanoInimigo.Resolver(sc)==s,"Slime resolution");Check(ReceptorDanoInimigo.Resolver(ec)==e,"Spirit resolution");Check(ReceptorDanoInimigo.Resolver(Child(root).Add<Collider>())==null,"Shared root selected unrelated enemy");
  var owner=new GameObject();ReceptorDanoInimigo.Aplicar(e,7,owner);Check(e.hits==1&&xp.owner==owner,"Spirit attribution");
  var a=new GameObject().Add<ArrowHost>();a.ProcessarColisao(gc,new Vector3(),new Vector3());Check(g.hits==0,"Unlaunched arrow caused damage");
  a.foiLancada=true;a.ProcessarColisao(gc,new Vector3(),new Vector3());a.ProcessarColisao(gc2,new Vector3(),new Vector3());Check(g.hits==1&&g.total==20&&g.owner==a.gameObject,"Arrow duplicated damage/multiplier/owner");Check(s.hits==0,"Arrow hit sibling Slime");
  g.Morto=true;Check(ReceptorDanoInimigo.EstaMorto(g),"Dead receiver state");
  return checks+" verificacoes aprovadas: receptor real e fluxo real de impacto; Unity simulado.";
 }
}
'@
$code="using System;using System.Linq;using System.Collections.Generic;using UnityEngine;`n"+$stub+$method+$tests+$receiver
Add-Type -TypeDefinition $code -IgnoreWarnings -WarningAction SilentlyContinue
[CombatTargetTests]::Run()
