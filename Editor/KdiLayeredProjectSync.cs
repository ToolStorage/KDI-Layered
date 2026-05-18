using Unity.CodeEditor;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Kylin.DI.Layered.Editor
{
    /// <summary>
    /// Forces external IDEs (Rider, VS Code, …) to regenerate .csproj files when this package
    /// is added or upgraded. Unity caches scoped registry packages at
    /// <c>Library/PackageCache/&lt;name&gt;@&lt;contentHash&gt;/</c>, and that hash changes on
    /// every version bump. The generated .csproj embeds the Roslyn analyzer DLL's absolute path,
    /// so after a package update the IDE-side path becomes stale and the analyzer silently stops
    /// loading. Calling <see cref="CodeEditor.SyncAll"/> here rewrites the project files with the
    /// new hash path. Unity's own Editor compiler is unaffected — only IDE integrations.
    /// </summary>
    [InitializeOnLoad]
    internal static class KdiLayeredProjectSync
    {
        private const string PackageName = "com.kylin.di.layered";

        static KdiLayeredProjectSync()
        {
            Events.registeredPackages += OnPackagesChanged;
        }

        private static void OnPackagesChanged(PackageRegistrationEventArgs args)
        {
            if (!AffectsThisPackage(args)) return;
            CodeEditor.CurrentEditor.SyncAll();
        }

        private static bool AffectsThisPackage(PackageRegistrationEventArgs args)
        {
            foreach (var p in args.added)
                if (p.name == PackageName) return true;
            foreach (var p in args.changedTo)
                if (p.name == PackageName) return true;
            return false;
        }
    }
}
