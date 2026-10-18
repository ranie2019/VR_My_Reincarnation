using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GrupoPlayer : MonoBehaviour
{
    [SerializeField] private List<StatusPlayer> membros = new List<StatusPlayer>();
    [SerializeField] private bool dividirExperienciaComGrupo = true;

    public bool DeveDividirExperienciaComGrupo()
    {
        return dividirExperienciaComGrupo;
    }

    public bool TemGrupoValido()
    {
        return dividirExperienciaComGrupo && GetMembrosValidos().Count > 0;
    }

    public List<StatusPlayer> GetMembrosValidos()
    {
        List<StatusPlayer> membrosValidos = new List<StatusPlayer>();

        for (int i = 0; i < membros.Count; i++)
        {
            StatusPlayer membro = membros[i];
            if (membro != null && membro.isActiveAndEnabled && !membrosValidos.Contains(membro))
                membrosValidos.Add(membro);
        }

        return membrosValidos;
    }

    public void AdicionarMembro(StatusPlayer membro)
    {
        if (membro == null || membros.Contains(membro))
            return;

        membros.Add(membro);
    }

    public void RemoverMembro(StatusPlayer membro)
    {
        if (membro == null)
            return;

        membros.Remove(membro);
    }
}
