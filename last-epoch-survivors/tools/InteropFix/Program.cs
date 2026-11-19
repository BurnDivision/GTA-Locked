// InteropFix: repairs Il2CppInterop output on Unity 6000.4+ where the unstripper
// emits duplicate nested type definitions (e.g. '<>O'), which makes the CLR throw
// BadImageFormatException("Duplicate type with name '<>O'") and MelonLoader reports
// "No Support Module Loaded!".
//
// Strategy: RENAME duplicates (<>O -> <>O_dup1 ...) instead of deleting them, so every
// field/method reference that points at a duplicate stays valid.
//
// Usage: InteropFix <Il2CppAssemblies dir> [--dry-run]
// Writes <file>.bak before changing any file. Idempotent.
using Mono.Cecil;

if (args.Length < 1) { Console.WriteLine("usage: InteropFix <dir> [--dry-run] | --inspect <dll> <typename>"); return 2; }
if (args[0] == "--inspect")
{
    using var m = ModuleDefinition.ReadModule(args[1]);
    foreach (var t in AllTypes(m.Types))
        if (t.Name == args[2])
            Console.WriteLine($"{t.MetadataToken.ToUInt32():X8} ns='{t.Namespace}' decl='{t.DeclaringType?.FullName}' fields={t.Fields.Count} methods={t.Methods.Count}");
    return 0;
}
var dir = args[0];
var dry = args.Contains("--dry-run");
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(dir);
int totalFixed = 0;

foreach (var path in Directory.GetFiles(dir, "*.dll"))
{
    var bytes = File.ReadAllBytes(path); // read fully so we can overwrite in place
    using var ms = new MemoryStream(bytes);
    ModuleDefinition module;
    try { module = ModuleDefinition.ReadModule(ms, new ReaderParameters { AssemblyResolver = resolver }); }
    catch (Exception e) { Console.WriteLine($"skip {Path.GetFileName(path)}: {e.Message}"); continue; }

    int fixedHere = 0;
    void Scan(IEnumerable<TypeDefinition> types, string scope)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in types.ToList())
        {
            var key = t.Namespace + "." + t.Name;
            if (!seen.Add(key))
            {
                int n = 1; string newName;
                do { newName = $"{t.Name}_dup{n++}"; } while (!seen.Add(t.Namespace + "." + newName));
                Console.WriteLine($"  {Path.GetFileName(path)}: {scope} :: {t.Name} -> {newName}");
                t.Name = newName;
                fixedHere++;
            }
            if (t.HasNestedTypes) Scan(t.NestedTypes, t.FullName);
        }
    }
    Scan(module.Types, "<module>");

    // Unity 6000.4: the CLR rejects multiple '<>O' (compiler delegate-cache) types even when they
    // are nested in different parents. Il2CppInterop emits them as empty stubs, so give each a
    // unique name. Safe: they have no members, and nothing can reference an empty stub by member.
    foreach (var t in AllTypes(module.Types).Where(t => t.Name == "<>O").ToList())
    {
        var newName = $"<>O_{t.MetadataToken.ToUInt32():X8}";
        Console.WriteLine($"  {Path.GetFileName(path)}: {t.DeclaringType?.FullName} :: <>O -> {newName} (fields={t.Fields.Count}, methods={t.Methods.Count})");
        t.Name = newName;
        fixedHere++;
    }

    if (fixedHere > 0)
    {
        totalFixed += fixedHere;
        if (!dry)
        {
            var bak = path + ".bak";
            if (!File.Exists(bak)) File.WriteAllBytes(bak, bytes);
            module.Write(path);
            Console.WriteLine($"fixed {fixedHere} in {Path.GetFileName(path)} (backup: {Path.GetFileName(bak)})");
        }
    }
    module.Dispose();
}
Console.WriteLine(dry ? $"dry run: {totalFixed} duplicates found" : $"done: {totalFixed} duplicates renamed");
return 0;

static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> ts)
{
    foreach (var t in ts) { yield return t; foreach (var n in AllTypes(t.NestedTypes)) yield return n; }
}
