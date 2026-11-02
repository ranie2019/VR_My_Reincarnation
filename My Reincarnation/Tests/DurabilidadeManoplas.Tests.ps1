$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Get-Content -Raw (Join-Path $project 'Assets/Script/Equipamento/AtributosManoplas.cs')
$a=$source.IndexOf('    private void Update()')
$b=$source.IndexOf('    public void AtualizarDurabilidadeVisual()', $a)
$methods=$source.Substring($a,$b-$a)
$code=@'
using System;
public class EquiparManoplas {public bool JaEquipado;public int breaks;public void PrepararQuebraPorDurabilidade(){JaEquipado=false;breaks++;}}
public class EstadoItemInventario {public bool estaNoInventario;}
public class ItemPersistente {public int consumed;public void RegistrarConsumoPermanente(){consumed++;}}
public class GameObject {public bool active=true;public void SetActive(bool value){active=value;}}
public static class Time {public static float deltaTime;}
public class DurabilityHost {
 private float vidaAtual=100,vidaMaxima=100,desgasteNaturalPorMinuto=.5f,ultimaVidaPublicada=float.NaN;
 private double vidaPrecisa;private bool destruindoPorDurabilidade;
 private EquiparManoplas equip=new EquiparManoplas();private EstadoItemInventario inventory=new EstadoItemInventario();
 private ItemPersistente persist=new ItemPersistente();private GameObject gameObject=new GameObject();private int destroyed;
 private T GetComponent<T>() where T:class=>typeof(T)==typeof(EquiparManoplas)?equip as T:typeof(T)==typeof(EstadoItemInventario)?inventory as T:persist as T;
 private void Destroy(GameObject go){destroyed++;}
 private void AtualizarDurabilidadeVisual(){}
'@+$methods+@'
 public static int Run(){int n=0;Action<bool,string> c=(ok,m)=>{if(!ok)throw new Exception(m);n++;};
  var h=new DurabilityHost();h.equip.JaEquipado=true;Time.deltaTime=60;h.Update();c(h.vidaAtual==99.5f,"0.5 por minuto");
  h=new DurabilityHost();h.equip.JaEquipado=true;Time.deltaTime=1f/60;for(int i=0;i<3600;i++)h.Update();c(Math.Abs(h.vidaAtual-99.5)<.0001,"Mesmo desgaste em 3600 frames");
  h.equip.JaEquipado=false;float before=h.vidaAtual;Time.deltaTime=600;h.Update();c(h.vidaAtual==before,"Nao desgastar solta");
  h.equip.JaEquipado=true;h.inventory.estaNoInventario=true;h.Update();c(h.vidaAtual==before,"Nao desgastar guardada");h.inventory.estaNoInventario=false;
  h.RepararCompleto();c(h.vidaAtual==100,"Reparar completamente");Time.deltaTime=60;h.Update();c(h.vidaAtual==99.5,"Reparo sincroniza acumulador preciso");
  h.AplicarDesgasteCombate(-1);h.AplicarDesgasteCombate(float.NaN);c(h.vidaAtual==99.5,"Ignorar desgaste invalido");
  h.AplicarDesgasteCombate(200);c(h.vidaAtual==0&&h.destroyed==1&&!h.gameObject.active,"Zero desativa e destroi uma vez");
  c(h.equip.breaks==1&&!h.equip.JaEquipado&&h.persist.consumed==1,"Quebra libera equipamento e registra consumo permanente");
  h.Update();h.AplicarDesgaste(10);c(h.destroyed==1,"Nao repetir destruicao");h.RepararCompleto();c(h.vidaAtual==0,"Nao ressuscitar item ja destruindo");
  return n;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[DurabilityHost]::Run()+" cenarios reais de desgaste em host simulado.")
