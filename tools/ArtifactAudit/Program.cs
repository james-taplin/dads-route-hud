using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 1) throw new ArgumentException("Pass repository root");
var root = Path.GetFullPath(args[0]);
using var info = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "info.json")));
var id = info.RootElement.GetProperty("Id").GetString()!;
var version = info.RootElement.GetProperty("Version").GetString()!;
var assembly = info.RootElement.GetProperty("AssemblyName").GetString()!;
var dll = Path.Combine(root, "build", assembly);
using var stream = File.OpenRead(dll);
using var pe = new PEReader(stream);
var metadata = pe.GetMetadataReader();
var definition = metadata.GetAssemblyDefinition();
var name = metadata.GetString(definition.Name);
var references = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToArray();
var singleObjectSearchReferences = metadata.MemberReferences.Count(h => metadata.GetString(metadata.GetMemberReference(h).Name) == "FindObjectOfType");
if (singleObjectSearchReferences != 0) throw new Exception("Global single-object search remains in emitted assembly");
if (name != id || definition.Version.ToString(3) != version) throw new Exception("Assembly identity mismatch");
var forbidden = new[] { "Signals", "Signals.MP", "AITraffic", "DoubleTrack", "DerailValleyModToolbar", id == "DadsRouteHUD" ? "DadsDrivingHUD" : "DadsRouteHUD" };
if (references.Intersect(forbidden, StringComparer.OrdinalIgnoreCase).Any()) throw new Exception("Static optional/other-HUD dependency");
if (info.RootElement.TryGetProperty("Requirements", out var required) && required.GetArrayLength() != 0) throw new Exception("Unexpected mandatory dependencies");
var resolved = File.ReadAllLines(Path.Combine(root, "build", "resolved-references.txt"));
if (resolved.Any(p => p.Contains(root + Path.DirectorySeparatorChar + "lib", StringComparison.OrdinalIgnoreCase))) throw new Exception("Obsolete lib reference");
var zip = Path.Combine(root, "artifacts", id + "-" + version + ".zip");
var expectedHash = File.ReadAllText(zip + ".sha256").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)));
if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new Exception("ZIP hash mismatch");
var sources = new Dictionary<string, string>
{
    ["info.json"] = "info.json", [assembly] = "build/" + assembly,
    ["QuantitiesNet.dll"] = "build/QuantitiesNet.dll", ["LICENSE"] = "LICENSE",
    ["QuantitiesNet.LICENSE"] = "QuantitiesNet.LICENSE", ["THIRD_PARTY_NOTICES.txt"] = "THIRD_PARTY_NOTICES.txt",
    ["README.md"] = "README.md", ["docs/VALIDATION.md"] = "docs/VALIDATION.md",
    ["docs/PROJECT_CONTEXT.md"] = "docs/PROJECT_CONTEXT.md", ["docs/HANDOVER.md"] = "docs/HANDOVER.md"
};
if (id == "DadsRouteHUD")
{
    sources["CREDITS.md"] = "CREDITS.md";
    sources["docs/LICENSING.md"] = "docs/LICENSING.md";
    sources["docs/NATIVE_SIGN_ASSOCIATION.md"] = "docs/NATIVE_SIGN_ASSOCIATION.md";
    sources["docs/OPTIONAL_SIGNAL_ADAPTER.md"] = "docs/OPTIONAL_SIGNAL_ADAPTER.md";
}
using var archive = ZipFile.OpenRead(zip);
if (archive.Entries.Count != sources.Count) throw new Exception("Unexpected ZIP count");
foreach (var entry in archive.Entries)
{
    var prefix = id + "/";
    if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal) || !sources.TryGetValue(entry.FullName.Substring(prefix.Length), out var source)) throw new Exception("Non-allowlisted ZIP entry: " + entry.FullName);
    using var input = entry.Open();
    using var memory = new MemoryStream();
    input.CopyTo(memory);
    if (!memory.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(root, source)))) throw new Exception("ZIP/source mismatch: " + source);
}
var report = new { id, version, assembly = name, references, singleObjectSearchReferences, zip, sha256 = hash, verifiedPackageFiles = archive.Entries.Count,
    scope = "PE metadata and ZIP bytes only; no target DLL execution. No Mod Toolbar registration or dependency. Runtime interaction remains pending." };
File.WriteAllText(Path.Combine(root, "build", "unified-artifact-audit.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(id + " " + version + ": identity/references and " + archive.Entries.Count + " package files verified; SHA256 " + hash);
