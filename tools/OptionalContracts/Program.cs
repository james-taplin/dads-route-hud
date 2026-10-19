using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// PE metadata only. None of the inspected assemblies is loaded or executed.
if (args.Length != 2) throw new ArgumentException("Usage: OptionalContracts <game Mods directory> <report.json>");
var contracts = new Contract[]
{
    new("DVSignals/Signals.Game.dll", "Signals.Game.SignalManager", "property", "AllControllers"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.SignalManager", "base", "DV.Utils.SingletonBehaviour`1<Signals.Game.SignalManager>"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Controllers.BasicSignalController", "property", "PlacementInfo"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Controllers.BasicSignalController", "property", "AllSignals"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Controllers.BasicSignalController", "property", "Exists"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Controllers.BasicSignalController", "field", "Id", IsStatic: false),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Controllers.BasicSignalController", "method", "GetCameraDistanceSqr", 0, false),
    new("DVSignals/Signals.Game.dll", "Signals.Game.SignalPlacementInfo", "field", "Track"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.SignalPlacementInfo", "field", "Span"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.SignalPlacementInfo", "field", "Direction"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Signal", "field", "Id", IsStatic: false),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Signal", "property", "Name"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Signal", "property", "CurrentAspect"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Signal", "property", "Block"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Signal", "property", "IsShunting"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Signal", "property", "DistantSignal"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Railway.TrackBlock", "property", "AllTracks"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Aspects.IAspect", "property", "Id"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Aspects.IAspect", "property", "DisallowPassing"),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Aspects.IAspect", "method", "GetDefinition", 0, false),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Railway.TrackChecker", "method", "IsOccupied", 2, true),
    new("DVSignals/Signals.Game.dll", "Signals.Game.Railway.TrackReserver", "method", "IsTrackReserved", 2, true),
    new("DVSignals/Signals.Game.dll", "Signals.Game.TrackDirection", "literal", "Out", Literal: 0),
    new("DVSignals/Signals.Game.dll", "Signals.Game.TrackDirection", "literal", "In", Literal: 1),
    new("DVSignals/Signals.Common.dll", "Signals.Common.CrossingCheckMode", "literal", "Ignore", Literal: 0),
    new("DVSignals/Signals.Common.dll", "Signals.Common.Aspects.AspectBaseDefinition", "field", "OnLights"),
    new("DVSignals/Signals.Common.dll", "Signals.Common.SignalLightDefinition", "field", "Colour"),
    new("AITraffic/AITraffic.dll", "AITraffic.Navigation.SignalRegistry", "method", "GetAspectDisplayName", 1, true),
    new("AITraffic/AITraffic.dll", "AITraffic.Navigation.SignalRegistry", "method", "GetAspectColorHex", 1, true),
    new("AITraffic/AITraffic.dll", "AITraffic.Navigation.SignalRegistry", "method", "GetSignalName", 1, true),
    new("AITraffic/AITraffic.dll", "AITraffic.Navigation.SignalRegistry", "method", "IsSignalReservedByAI", 1, true),
    new("AITraffic/AITraffic.dll", "AITraffic.Core.TrafficManager", "field", "s_instance", IsStatic: true, IsPublic: false),
    new("AITraffic/AITraffic.dll", "AITraffic.Navigation.RailGraph", "field", "s_instance", IsStatic: true, IsPublic: false),
    new("AITraffic/AITraffic.dll", "AITraffic.Navigation.RailGraph", "method", "TryGetTrackReservationHolder", 3, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer", "method", "PlaceSign", 1, false, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer", "method", "GetTrackSigns", 2, false, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer", "method", "GetJunctionSigns", 1, false, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer", "method", "ShouldIncludeTrack", 1, true, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer+SignData", "method", "GetTrack", 0, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer+SignData", "method", "GetPosition", 0, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer+SignData", "method", "GetRotation", 0, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer+SignData", "method", "GetSign", 0, false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignPlacer+SignData", "field", "flip", IsStatic: false, IsPublic: false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.Sign", "field", "signParameters", IsStatic: false, IsPublic: false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignParameters", "field", "type", IsStatic: false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignParameters", "field", "sign", IsStatic: false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignParameters", "field", "signText", IsStatic: false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignParameters", "field", "accessories", IsStatic: false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignDebug", "field", "text", IsStatic: false),
    new("../DerailValley_Data/Managed/DV.SignPlacer.dll", "DV.Signs.SignGeneratorData", "field", "signParameters", IsStatic: false),
    new("../DerailValley_Data/Managed/DV.Utils.dll", "DV.Utils.SingletonBehaviour`1", "field", "_instance", IsStatic: true, IsPublic: false),
};
var results = new List<object>();
var sources = new List<object>();
var failures = 0;
foreach (var group in contracts.GroupBy(c => c.File))
{
    var path = Path.GetFullPath(Path.Combine(args[0], group.Key.Replace('/', Path.DirectorySeparatorChar)));
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    sources.Add(new { path, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) });
    foreach (var contract in group)
    {
        var typeHandle = reader.TypeDefinitions.FirstOrDefault(h =>
        {
            return TypeName(reader, h) == contract.Type;
        });
        var matched = false;
        var shape = "";
        if (!typeHandle.IsNil)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            if (contract.Kind == "base")
            {
                shape = MetadataTypeName(reader, type.BaseType);
                matched = shape == contract.Member;
            }
            else if (contract.Kind == "property")
            {
                foreach (var handle in type.GetProperties())
                {
                    var property = reader.GetPropertyDefinition(handle);
                    if (reader.GetString(property.Name) != contract.Member) continue;
                    var getter = property.GetAccessors().Getter;
                    if (!getter.IsNil)
                    {
                        var attributes = reader.GetMethodDefinition(getter).Attributes;
                        matched = (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
                        shape = "public property getter; signature " + Convert.ToHexString(reader.GetBlobBytes(property.Signature));
                    }
                }
            }
            else if (contract.Kind == "method")
            {
                foreach (var handle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(handle);
                    if (reader.GetString(method.Name) != contract.Member) continue;
                    var signature = reader.GetBlobReader(method.Signature);
                    var header = signature.ReadSignatureHeader();
                    if (header.IsGeneric) signature.ReadCompressedInteger();
                    var count = signature.ReadCompressedInteger();
                    var isStatic = (method.Attributes & MethodAttributes.Static) != 0;
                    var isPublic = (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
                    if (count == contract.Parameters && isStatic == contract.IsStatic && isPublic == contract.IsPublic)
                    {
                        matched = true;
                        shape = (isStatic ? "static" : "instance") + (isPublic ? " public" : " nonpublic") + " method, " + count + " parameters; signature "
                            + Convert.ToHexString(reader.GetBlobBytes(method.Signature));
                    }
                }
            }
            else
            {
                foreach (var handle in type.GetFields())
                {
                    var field = reader.GetFieldDefinition(handle);
                    if (reader.GetString(field.Name) != contract.Member) continue;
                    var isStatic = (field.Attributes & FieldAttributes.Static) != 0;
                    var isPublic = (field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
                    matched = (!contract.IsStatic.HasValue || isStatic == contract.IsStatic)
                        && isPublic == contract.IsPublic;
                    shape = (isStatic ? "static" : "instance") + " field; signature " + Convert.ToHexString(reader.GetBlobBytes(field.Signature));
                    if (contract.Kind == "literal")
                    {
                        var constantHandle = field.GetDefaultValue();
                        if (constantHandle.IsNil) matched = false;
                        else
                        {
                            var constant = reader.GetConstant(constantHandle);
                            var blob = reader.GetBlobReader(constant.Value);
                            var value = constant.TypeCode switch
                            {
                                ConstantTypeCode.Byte => blob.ReadByte(),
                                ConstantTypeCode.SByte => blob.ReadSByte(),
                                ConstantTypeCode.Int16 => blob.ReadInt16(),
                                ConstantTypeCode.UInt16 => blob.ReadUInt16(),
                                ConstantTypeCode.Int32 => blob.ReadInt32(),
                                _ => int.MinValue
                            };
                            matched &= value == contract.Literal;
                            shape += "; literal " + value;
                        }
                    }
                }
            }
        }
        if (!matched) failures++;
        results.Add(new { contract.File, contract.Type, contract.Kind, contract.Member, matched, shape });
    }
}
var report = new
{
    scope = "PE metadata member accessibility, arity/static shape, constructed singleton ancestry and enum constants only; no target assemblies executed; member signatures recorded as raw metadata blobs. No runtime compatibility or freshness claim.",
    sourceDlls = sources,
    matchedContracts = contracts.Length - failures,
    totalContracts = contracts.Length,
    contracts = results,
    freshnessEvidence = "Installed DVSignals BasicSignalController normal camera envelope is 1,500 metres; queued propagation can update farther. Adapter never invokes updates and labels all aspect ages unverified.",
    readOnlyEvidence = "Existing singleton backing fields read only, including SignalManager's native closed generic base instance slot. No Instance getter or scene-search fallback. TrackChecker/TrackReserver/graph reads and AI name/aspect helpers retained; TryFindUpcomingSignals is not called.",
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine((contracts.Length - failures) + "/" + contracts.Length + " optional/native metadata contracts matched; no target code executed.");
return failures == 0 ? 0 : 1;

static string TypeName(MetadataReader reader, TypeDefinitionHandle handle)
{
    var type = reader.GetTypeDefinition(handle);
    var declaring = type.GetDeclaringType();
    return declaring.IsNil ? reader.GetString(type.Namespace) + "." + reader.GetString(type.Name)
        : TypeName(reader, declaring) + "+" + reader.GetString(type.Name);
}

static string MetadataTypeName(MetadataReader reader, EntityHandle handle)
{
    if (handle.Kind == HandleKind.TypeDefinition) return TypeName(reader, (TypeDefinitionHandle)handle);
    if (handle.Kind == HandleKind.TypeReference)
    {
        var reference = reader.GetTypeReference((TypeReferenceHandle)handle);
        return reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);
    }
    if (handle.Kind != HandleKind.TypeSpecification) return "Unsupported base metadata " + handle.Kind;
    var specification = reader.GetTypeSpecification((TypeSpecificationHandle)handle);
    var signature = reader.GetBlobReader(specification.Signature);
    if (signature.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance
        || signature.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle) return "Unsupported base signature";
    var generic = MetadataTypeName(reader, signature.ReadTypeHandle());
    var count = signature.ReadCompressedInteger();
    var arguments = new List<string>();
    for (var index = 0; index < count; index++)
    {
        if (signature.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle) return "Unsupported base argument";
        arguments.Add(MetadataTypeName(reader, signature.ReadTypeHandle()));
    }
    return generic + "<" + string.Join(",", arguments) + ">";
}

record Contract(string File, string Type, string Kind, string Member, int Parameters = -1, bool? IsStatic = null, bool IsPublic = true, int? Literal = null);
