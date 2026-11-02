$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Assets/Script/Save/SaveManager.cs')
$a=$source.IndexOf('    private void RemoverCogumelosDoMundoSalvo(')
$method=$source.Substring($a,$source.IndexOf('    private bool ConteudoSaveValido(',$a)-$a)
$code=@"
using System;
using System.Collections.Generic;
public enum FindObjectsInactive {Include} public enum FindObjectsSortMode {None}
public class ItemPersistente {public string id; public bool CorrespondeAoItemId(string s)=>s==id; public class EstadoCenaItem {public bool possuiPontoCogumelo; public string itemId;}}
public class Cocumelo { public ItemPersistente item; public T GetComponent<T>() where T:class => item as T; }
public class Prefab {public Cocumelo cog;public T GetComponent<T>() where T:class =>cog as T;}
public class ItemDatabaseLocal {public static ItemDatabaseLocal Instancia; public Dictionary<string,Prefab> prefabs=new Dictionary<string,Prefab>();public Prefab ObterPrefab(string id)=>prefabs.ContainsKey(id)?prefabs[id]:null;}
public class RespawnNatureza {public class EstadoPontoCogumelo {}}
public class SceneObjectSaveData {public bool item=true,quarentenaCogumelo;public ItemPersistente.EstadoCenaItem estado;}
public class SaveData {public List<SceneObjectSaveData> objetosCena=new List<SceneObjectSaveData>();public List<string> inventario=new List<string>();public List<RespawnNatureza.EstadoPontoCogumelo> cogumelosPendentes=new List<RespawnNatureza.EstadoPontoCogumelo>();}
public class WorldChecks {
public static Cocumelo[] scene=new Cocumelo[0];
T[] FindObjectsByType<T>(FindObjectsInactive a,FindObjectsSortMode b) where T:class =>scene as T[];
bool EhDadoItemPersistente(SceneObjectSaveData d)=>d.item;
ItemPersistente.EstadoCenaItem LerEstadoItemCena(SceneObjectSaveData d)=>d.estado;
$method
public static int Run(){int n=0;Action<bool,string> check=(ok,m)=>{if(!ok)throw new Exception(m);n++;};var runner=new WorldChecks();
var db=new ItemDatabaseLocal();ItemDatabaseLocal.Instancia=db;db.prefabs["mushroom"]=new Prefab{cog=new Cocumelo()};
scene=new[]{new Cocumelo{item=new ItemPersistente{id="scene-alias"}}};
var keep=new SceneObjectSaveData{estado=new ItemPersistente.EstadoCenaItem{itemId="sword"}};
var system=new SceneObjectSaveData{item=false};
var data=new SaveData();data.inventario.Add("saved mushroom stack");data.cogumelosPendentes.Add(new RespawnNatureza.EstadoPontoCogumelo());
data.objetosCena.AddRange(new[]{keep,system,new SceneObjectSaveData{estado=new ItemPersistente.EstadoCenaItem{possuiPontoCogumelo=true,itemId="deleted"}},new SceneObjectSaveData{quarentenaCogumelo=true,estado=new ItemPersistente.EstadoCenaItem{itemId="old"}},new SceneObjectSaveData{estado=new ItemPersistente.EstadoCenaItem{itemId="mushroom"}},new SceneObjectSaveData{estado=new ItemPersistente.EstadoCenaItem{itemId="scene-alias"}}});
runner.RemoverCogumelosDoMundoSalvo(data);
check(data.objetosCena.Count==2,"All mushroom world records removed, including nonzero valid clones and originals");
check(data.objetosCena.Contains(keep),"Other resources/equipment preserved");check(data.objetosCena.Contains(system),"Other persistent systems preserved");
check(data.inventario.Count==1,"Inventory mushrooms untouched");check(data.cogumelosPendentes.Count==0,"Old world timers cannot spawn on startup");
runner.RemoverCogumelosDoMundoSalvo(data);check(data.objetosCena.Count==2,"Migration is idempotent");
ItemDatabaseLocal.Instancia=null;scene=new Cocumelo[0];var old=new SaveData();old.objetosCena.Add(new SceneObjectSaveData{estado=new ItemPersistente.EstadoCenaItem{possuiPontoCogumelo=true,itemId="missing prefab"}});runner.RemoverCogumelosDoMundoSalvo(old);check(old.objetosCena.Count==0,"Metadata migration works before database initialization");return n;}
}
"@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[WorldChecks]::Run()+" verificacoes do filtro real de cogumelos do mundo; host simulado.")
