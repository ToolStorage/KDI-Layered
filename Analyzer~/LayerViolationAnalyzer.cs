using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kylin.DI.Layered.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LayerViolationAnalyzer : DiagnosticAnalyzer
    {
        public const string SameLayerId = "KDI001";
        public const string UpwardLayerId = "KDI002";

        private const string LayerNamespace = "Kylin.DI.Layered";
        private const string InjectAttributeName = "InjectAttribute";
        private const string InjectNamespace = "Kylin.DI";

        private static readonly DiagnosticDescriptor SameLayerRule = new(
            id: SameLayerId,
            title: "Same-layer injection is forbidden",
            messageFormat: "{0} (in the {1} layer) injects {2} which is in the same layer. Same-layer dependencies are forbidden by KDI Layered Architecture.",
            category: "KDI.Layered",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Two classes in the same layer must not inject each other. Move the shared logic into a lower layer (DomainService or ApplicationService).");

        private static readonly DiagnosticDescriptor UpwardLayerRule = new(
            id: UpwardLayerId,
            title: "Upward layer injection is forbidden",
            messageFormat: "{0} (in the {1} layer) injects {2} which is in the upper {3} layer. Only upper layers may inject lower layers.",
            category: "KDI.Layered",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A lower layer cannot depend on an upper layer. Invert the dependency by having the upper layer subscribe to the lower layer.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SameLayerRule, UpwardLayerRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            var field = (IFieldSymbol)context.Symbol;

            if (!HasInjectAttribute(field))
                return;

            var hostLayer = ResolveLayer(field.ContainingType);
            if (hostLayer == 0)
                return;

            var fieldLayer = ResolveLayer(field.Type);
            if (fieldLayer == 0)
                return;

            var location = field.Locations.Length > 0 ? field.Locations[0] : Location.None;

            if (hostLayer == fieldLayer)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SameLayerRule,
                    location,
                    field.ContainingType.Name,
                    LayerName(hostLayer),
                    field.Type.Name));
            }
            else if (hostLayer > fieldLayer)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    UpwardLayerRule,
                    location,
                    field.ContainingType.Name,
                    LayerName(hostLayer),
                    field.Type.Name,
                    LayerName(fieldLayer)));
            }
        }

        private static bool HasInjectAttribute(IFieldSymbol field)
        {
            foreach (var attribute in field.GetAttributes())
            {
                var attrClass = attribute.AttributeClass;
                if (attrClass is null)
                    continue;
                if (attrClass.Name != InjectAttributeName)
                    continue;
                if (attrClass.ContainingNamespace?.ToDisplayString() == InjectNamespace)
                    return true;
            }
            return false;
        }

        private static int ResolveLayer(ITypeSymbol? type)
        {
            if (type is null)
                return 0;

            if (ImplementsLayerInterface(type, "IViewLayer")) return 1;
            if (ImplementsLayerInterface(type, "IViewModelLayer")) return 2;
            if (ImplementsLayerInterface(type, "IApplicationServiceLayer")) return 3;
            if (ImplementsLayerInterface(type, "IDomainServiceLayer")) return 4;
            if (ImplementsLayerInterface(type, "IDataLayer")) return 5;
            return 0;
        }

        private static bool ImplementsLayerInterface(ITypeSymbol type, string interfaceName)
        {
            foreach (var iface in type.AllInterfaces)
            {
                if (iface.Name != interfaceName)
                    continue;
                if (iface.ContainingNamespace?.ToDisplayString() == LayerNamespace)
                    return true;
            }
            return false;
        }

        private static string LayerName(int level) => level switch
        {
            1 => "View",
            2 => "ViewModel",
            3 => "ApplicationService",
            4 => "DomainService",
            5 => "Data",
            _ => "Unknown"
        };
    }
}
