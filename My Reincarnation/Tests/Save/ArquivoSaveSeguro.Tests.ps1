$ErrorActionPreference = 'Stop'
$root = Join-Path ([System.IO.Path]::GetTempPath()) ('MyReincarnationSaveTests-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($root) | Out-Null
$source = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../Assets/Script/Save/ArquivoSaveSeguro.cs')
$tests = @'
public static class SaveFileTests
{
    private static int assertions;
    private static bool Valid(string s) { return s.StartsWith("VALID:") && s.EndsWith(":END"); }
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    private static void ExpectFailure(Action action)
    { bool failed = false; try { action(); } catch (Exception) { failed = true; } Check(failed, "Expected failure"); }
    public static string Run(string root)
    {
        string a = "VALID:A:END", b = "VALID:B:END", c = "VALID:C:END";
        string p = Path.Combine(root, "normal.json");
        Check(!ArquivoSaveSeguro.ExisteAlgumaGeracao(p), "First game must be empty");
        ArquivoSaveSeguro.Gravar(p, a, Valid);
        Check(File.ReadAllText(p) == a, "First write");
        ArquivoSaveSeguro.Gravar(p, b, Valid);
        Check(File.ReadAllText(p) == b && File.ReadAllText(p + ".bak") == a, "Backup generation");
        ArquivoSaveSeguro.Gravar(p, c, Valid);
        Check(File.ReadAllText(p + ".bak") == b && File.ReadAllText(p + ".bak.previous") == a, "Previous generation");
        ExpectFailure(() => ArquivoSaveSeguro.Gravar(p, "BROKEN", Valid));
        Check(File.ReadAllText(p) == c && File.ReadAllText(p + ".bak") == b, "Invalid data changed files");
        string f = Path.Combine(root, "failure.json");
        ArquivoSaveSeguro.Gravar(f, a, Valid);
        ExpectFailure(() => ArquivoSaveSeguro.Gravar(f, b, Valid, (src, dst) => {
            if (src == f + ".tmp" && dst == f) throw new IOException("Simulated interrupted publication");
            File.Move(src, dst);
        }));
        Check(!File.Exists(f) && File.ReadAllText(f + ".bak") == a, "Last valid generation lost");
        Check(File.ReadAllText(f + ".tmp") == b, "Temporary generation lost");
        Check(ArquivoSaveSeguro.ExisteAlgumaGeracao(f), "Interrupted write mistaken for first game");
        ArquivoSaveSeguro.Gravar(f, c, Valid);
        Check(File.ReadAllText(f) == c && File.ReadAllText(f + ".bak") == a, "Retry lost backup");
        string corrupt = Path.Combine(root, "corrupt.json");
        File.WriteAllText(corrupt, "BAD"); File.WriteAllText(corrupt + ".bak", a);
        ArquivoSaveSeguro.Gravar(corrupt, b, Valid);
        Check(File.ReadAllText(corrupt + ".bak") == a, "Corrupt main replaced valid backup");
        string[] preserved = Directory.GetFiles(root, "corrupt.json.corrompido.*");
        Check(preserved.Length == 1 && File.ReadAllText(preserved[0]) == "BAD", "Corrupt original not preserved");
        string pre = Path.Combine(root, "before.json");
        ArquivoSaveSeguro.Gravar(pre, a, Valid);
        ExpectFailure(() => ArquivoSaveSeguro.Gravar(pre, b, Valid, (src, dst) => { throw new IOException("Simulated rename failure"); }));
        Check(File.ReadAllText(pre) == a, "Rename failure damaged main");
        Check(File.ReadAllText(pre + ".tmp") == b, "Rename failure damaged temporary");
        string rejected = Path.Combine(root, "rejected.json");
        File.WriteAllText(rejected, c); File.WriteAllText(rejected + ".bak", a);
        ArquivoSaveSeguro.Gravar(rejected, b, Valid, preservarPrincipal: true);
        Check(File.ReadAllText(rejected + ".bak") == a, "Recovery replaced the loaded backup");
        var rejectedFiles = Directory.GetFiles(root, "rejected.json.corrompido.*");
        Check(rejectedFiles.Length == 1 && File.ReadAllText(rejectedFiles[0]) == c, "Rejected main lost");
        ArquivoSaveSeguro.Gravar(p, a, Valid);
        var archived = Directory.GetFiles(root, "normal.json.bak.previous.preservado.*");
        Check(archived.Length == 1 && File.ReadAllText(archived[0]) == a, "Older backup discarded");
        return assertions + " assertions passed; isolated files: " + root;
    }
}
'@
Add-Type -TypeDefinition ($source + [Environment]::NewLine + $tests)
[SaveFileTests]::Run($root)
# Artefatos descartaveis preservados para inspecao. Nao acessa persistentDataPath.
