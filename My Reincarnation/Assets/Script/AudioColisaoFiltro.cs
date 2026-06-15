using UnityEngine;

public static class AudioColisaoFiltro
{
    public static bool PodeTocarSomDeColisao(Collision collision)
    {
        if (collision == null || collision.contactCount == 0)
            return false;

        return PodeTocarSomDeColisao(collision.collider);
    }

    public static bool PodeTocarSomDeColisao(Collider outro)
    {
        if (outro == null || outro.isTrigger)
            return false;

        if (EhTerrain(outro))
            return false;

        return true;
    }

    private static bool EhTerrain(Collider outro)
    {
        return outro is TerrainCollider || outro.GetComponent<Terrain>() != null;
    }
}
