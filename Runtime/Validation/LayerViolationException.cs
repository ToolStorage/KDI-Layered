using System;
using System.Reflection;

namespace Kylin.DI.Layered
{
    public class LayerViolationException : Exception
    {
        public Type HostType { get; }
        public FieldInfo Field { get; }
        public LayerLevel HostLayer { get; }
        public LayerLevel FieldLayer { get; }

        public LayerViolationException(Type hostType, FieldInfo field, LayerLevel hostLayer, LayerLevel fieldLayer)
            : base(BuildMessage(hostType, field, hostLayer, fieldLayer))
        {
            HostType = hostType;
            Field = field;
            HostLayer = hostLayer;
            FieldLayer = fieldLayer;
        }

        private static string BuildMessage(Type hostType, FieldInfo field, LayerLevel hostLayer, LayerLevel fieldLayer)
        {
            var direction = hostLayer == fieldLayer ? "same layer" : "upward";
            return $"[KDI.Layered] {hostType.FullName} ({hostLayer}) injects {field.FieldType.FullName} ({fieldLayer}) — {direction} injection is not allowed. Only upper layers may inject lower layers.";
        }
    }
}
