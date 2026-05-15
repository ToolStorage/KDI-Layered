using System;
using System.Reflection;

namespace Kylin.DI.Layered
{
    public static class LayerValidator
    {
        public static LayerLevel LayerOf(Type type)
        {
            if (type == null) return LayerLevel.None;
            if (typeof(IViewLayer).IsAssignableFrom(type)) return LayerLevel.View;
            if (typeof(IViewModelLayer).IsAssignableFrom(type)) return LayerLevel.ViewModel;
            if (typeof(IApplicationServiceLayer).IsAssignableFrom(type)) return LayerLevel.ApplicationService;
            if (typeof(IDomainServiceLayer).IsAssignableFrom(type)) return LayerLevel.DomainService;
            if (typeof(IDataLayer).IsAssignableFrom(type)) return LayerLevel.Data;
            return LayerLevel.None;
        }

        public static void Validate(Type implementorType)
        {
            if (implementorType == null) throw new ArgumentNullException(nameof(implementorType));

            var hostLayer = LayerOf(implementorType);
            if (hostLayer == LayerLevel.None) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var t = implementorType; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (field.GetCustomAttribute<InjectAttribute>() == null) continue;

                    var fieldLayer = LayerOf(field.FieldType);
                    if (fieldLayer == LayerLevel.None) continue;

                    if ((int)fieldLayer <= (int)hostLayer)
                    {
                        throw new LayerViolationException(implementorType, field, hostLayer, fieldLayer);
                    }
                }
            }
        }

        public static void ValidateAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsInterface || type.IsAbstract) continue;
                Validate(type);
            }
        }
    }
}
