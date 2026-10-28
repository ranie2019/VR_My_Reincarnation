using System;
using System.IO;
using System.Text;

// Nao depende de Unity: permite testar interrupcoes em diretorios descartaveis.
internal static class ArquivoSaveSeguro
{
    internal static string[] Candidatos(string principal)
    {
        return new[] { principal, principal + ".bak", principal + ".bak.previous", principal + ".tmp" };
    }

    internal static bool ExisteAlgumaGeracao(string principal)
    {
        foreach (string caminho in Candidatos(principal))
            if (File.Exists(caminho)) return true;
        return false;
    }

    internal static void Gravar(string principal, string conteudo, Func<string, bool> validar,
        Action<string, string> mover = null, bool preservarPrincipal = false)
    {
        if (!validar(conteudo)) throw new InvalidDataException("Snapshot de save invalido.");
        mover = mover ?? File.Move;
        string temporario = principal + ".tmp";
        string backup = principal + ".bak";
        string anterior = principal + ".bak.previous";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(principal)));

        // Preserva um temporario deixado por uma tentativa anterior antes de reutilizar o nome.
        if (File.Exists(temporario))
            mover(temporario, temporario + ".preservado." + Guid.NewGuid().ToString("N"));
        byte[] bytes = new UTF8Encoding(false).GetBytes(conteudo);
        using (var stream = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        if (!string.Equals(File.ReadAllText(temporario, Encoding.UTF8), conteudo, StringComparison.Ordinal) ||
            !validar(File.ReadAllText(temporario, Encoding.UTF8)))
            throw new InvalidDataException("Verificacao do arquivo temporario falhou.");

        if (File.Exists(principal))
        {
            if (!preservarPrincipal && validar(File.ReadAllText(principal, Encoding.UTF8)))
            {
                // O principal permanece intacto enquanto as geracoes mais antigas sao rotacionadas.
                if (File.Exists(anterior))
                    mover(anterior, anterior + ".preservado." + Guid.NewGuid().ToString("N"));
                if (File.Exists(backup)) mover(backup, anterior);
                mover(principal, backup);
            }
            else
            {
                // Preserva tambem um principal estruturalmente valido rejeitado pelo preflight.
                mover(principal, principal + ".corrompido." + Guid.NewGuid().ToString("N"));
            }
        }

        // Nao sobrescreve o destino. Se falhar, backup/previous/tmp continuam recuperaveis.
        mover(temporario, principal);
        if (!string.Equals(File.ReadAllText(principal, Encoding.UTF8), conteudo, StringComparison.Ordinal) ||
            !validar(File.ReadAllText(principal, Encoding.UTF8)))
            throw new InvalidDataException("Verificacao do save publicado falhou.");
        // Mantem .bak e .bak.previous. Arquivos de falhas sao preservados para diagnostico.
    }
}
