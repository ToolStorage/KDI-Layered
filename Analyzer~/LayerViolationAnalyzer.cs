using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Kylin.DI.Layered.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LayerViolationAnalyzer : DiagnosticAnalyzer
    {
        public const string SameLayerId = "KDI001";
        public const string UpwardLayerId = "KDI002";
        public const string OwnerOnlyId = "KDI003";

        private const string LayerNamespace = "Kylin.DI.Layered";
        private const string InjectAttributeName = "InjectAttribute";
        private const string InjectNamespace = "Kylin.DI";
        private const string OwnerOnlyAttributeName = "OwnerOnlyAttribute";
        private const string DomainServiceLayerName = "IDomainServiceLayer";
        private const string DataLayerName = "IDataLayer";

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

        private static readonly DiagnosticDescriptor OwnerOnlyRule = new(
            id: OwnerOnlyId,
            title: "Owner-only member called from a non-owner",
            messageFormat: "{0}.{1} is [OwnerOnly] — {2} does not declare IDomainServiceLayer<{0}> ownership of this Data type. Call this through a DomainService that owns {0}.",
            category: "KDI.Layered",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Members marked [OwnerOnly] on a Data layer type may only be invoked by a DomainService that declares ownership via IDomainServiceLayer<TData>. Self-calls inside the Data class (and subclasses) are allowed.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SameLayerRule, UpwardLayerRule, OwnerOnlyRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
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

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var op = (IInvocationOperation)context.Operation;
            var method = op.TargetMethod;
            if (method is null)
                return;

            if (!HasOwnerOnlyAttribute(method))
                return;

            ReportOwnerOnlyIfViolated(context, method.ContainingType, method.Name, op.Syntax.GetLocation());
        }

        private static void AnalyzePropertyReference(OperationAnalysisContext context)
        {
            var op = (IPropertyReferenceOperation)context.Operation;
            var prop = op.Property;
            if (prop is null)
                return;

            var propertyHasOwnerOnly = HasOwnerOnlyAttribute(prop);
            var setter = prop.SetMethod;
            var setterHasOwnerOnly = setter is not null && HasOwnerOnlyAttribute(setter);

            if (!propertyHasOwnerOnly && !setterHasOwnerOnly)
                return;

            // If only the setter is marked, allow read access; restrict only writes.
            if (!propertyHasOwnerOnly && setterHasOwnerOnly && !IsAssignmentTarget(op))
                return;

            ReportOwnerOnlyIfViolated(context, prop.ContainingType, prop.Name, op.Syntax.GetLocation());
        }

        private static void ReportOwnerOnlyIfViolated(
            OperationAnalysisContext context,
            INamedTypeSymbol? dataType,
            string memberName,
            Location location)
        {
            if (dataType is null)
                return;
            if (!ImplementsLayerInterface(dataType, DataLayerName))
                return;

            var enclosingType = GetEnclosingType(context.ContainingSymbol);
            if (enclosingType is null)
                return;

            // Self-calls inside the Data class (and subclasses) are always allowed.
            if (IsSameOrDerivedFrom(enclosingType, dataType))
                return;

            if (DeclaresOwnershipOf(enclosingType, dataType))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                OwnerOnlyRule,
                location,
                dataType.Name,
                memberName,
                enclosingType.Name));
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

        private static bool HasOwnerOnlyAttribute(ISymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                var attrClass = attribute.AttributeClass;
                if (attrClass is null)
                    continue;
                if (attrClass.Name != OwnerOnlyAttributeName)
                    continue;
                if (attrClass.ContainingNamespace?.ToDisplayString() == LayerNamespace)
                    return true;
            }
            return false;
        }

        private static INamedTypeSymbol? GetEnclosingType(ISymbol? symbol)
        {
            while (symbol is not null)
            {
                if (symbol is INamedTypeSymbol named)
                    return named;
                if (symbol.ContainingType is not null)
                    return symbol.ContainingType;
                symbol = symbol.ContainingSymbol;
            }
            return null;
        }

        private static bool IsSameOrDerivedFrom(ITypeSymbol candidate, ITypeSymbol target)
        {
            for (ITypeSymbol? t = candidate; t is not null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, target))
                    return true;
            }
            return false;
        }

        private static bool DeclaresOwnershipOf(ITypeSymbol candidate, ITypeSymbol dataType)
        {
            foreach (var iface in candidate.AllInterfaces)
            {
                if (iface.Name != DomainServiceLayerName)
                    continue;
                if (iface.ContainingNamespace?.ToDisplayString() != LayerNamespace)
                    continue;
                if (!iface.IsGenericType || iface.TypeArguments.Length != 1)
                    continue;

                var owned = iface.TypeArguments[0];
                if (SymbolEqualityComparer.Default.Equals(owned, dataType))
                    return true;
                if (IsAssignableTo(dataType, owned))
                    return true;
            }
            return false;
        }

        private static bool IsAssignableTo(ITypeSymbol from, ITypeSymbol to)
        {
            if (SymbolEqualityComparer.Default.Equals(from, to))
                return true;
            for (ITypeSymbol? t = from.BaseType; t is not null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, to))
                    return true;
            }
            foreach (var iface in from.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, to))
                    return true;
            }
            return false;
        }

        private static bool IsAssignmentTarget(IOperation op)
        {
            var current = op;
            var parent = op.Parent;
            while (parent is not null)
            {
                switch (parent)
                {
                    case IAssignmentOperation assign:
                        return ReferenceEquals(assign.Target, current);
                    case IIncrementOrDecrementOperation incdec:
                        return ReferenceEquals(incdec.Target, current);
                    case IArgumentOperation arg
                        when arg.Parameter is { RefKind: RefKind.Ref or RefKind.Out }:
                        return true;
                    case IConversionOperation:
                    case IParenthesizedOperation:
                        current = parent;
                        parent = parent.Parent;
                        continue;
                    default:
                        return false;
                }
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
