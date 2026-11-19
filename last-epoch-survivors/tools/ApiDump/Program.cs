// ApiDump: writes a greppable outline of interop assemblies (one member per line).
// Usage: ApiDump <out.txt> <dll> [<dll> ...]
// Line format:  T <type> : <base>   |   F/P/M <type>::<member signature>
// Output stays local (derived from game code; never commit it).
using Mono.Cecil;

var outPath = args[0];
using var w = new StreamWriter(outPath);
foreach (var dll in args.Skip(1))
{
    using var m = ModuleDefinition.ReadModule(dll);
    var asm = Path.GetFileNameWithoutExtension(dll);
    foreach (var t in All(m.Types))
    {
        w.WriteLine($"T [{asm}] {t.FullName} : {t.BaseType?.FullName}{(t.IsEnum ? " (enum)" : "")}");
        if (t.IsEnum) { foreach (var f in t.Fields.Where(f => f.IsStatic)) w.WriteLine($"E {t.FullName}::{f.Name} = {f.Constant}"); continue; }
        foreach (var f in t.Fields) w.WriteLine($"F {t.FullName}::{(f.IsStatic ? "static " : "")}{f.FieldType.FullName} {f.Name}");
        foreach (var p in t.Properties) w.WriteLine($"P {t.FullName}::{p.PropertyType.FullName} {p.Name}");
        foreach (var md in t.Methods)
        {
            if (md.IsGetter || md.IsSetter) continue;
            var ps = string.Join(", ", md.Parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            w.WriteLine($"M {t.FullName}::{(md.IsStatic ? "static " : "")}{md.ReturnType.Name} {md.Name}({ps})");
        }
    }
}
Console.WriteLine("wrote " + outPath);

static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> ts)
{ foreach (var t in ts) { yield return t; foreach (var n in All(t.NestedTypes)) yield return n; } }
