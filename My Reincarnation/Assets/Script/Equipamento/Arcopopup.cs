using UnityEngine;

/// <summary>Popup e dados individuais do arco, com o nucleo compartilhado das armas.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("My Reincarnation/Equipamento/Arcopopup")]
public class Arcopopup : Espada
{
    [Min(1)] public int desgastePorDisparo = 1;

    // O arco dispara pela logica existente. O popup nao adiciona golpes de espada.
    protected override void OnCollisionEnter(Collision collision) { }
    protected override void OnTriggerEnter(Collider other) { }
    protected override int ObterDanoCalculado() => 0;
}
