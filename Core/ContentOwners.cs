using System;
using System.Collections.Generic;
using System.Reflection;

namespace POGLib.Core
{
    /// <summary>Which assembly registered which content (for <c>Content.ContentAssemblies</c>).</summary>
    internal static class ContentOwners
    {
        private static readonly Dictionary<string, string> _modToAssembly = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly SortedSet<string> _assemblies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string Self = typeof(ContentOwners).Assembly.GetName().Name;

        public static IReadOnlyCollection<string> Assemblies => _assemblies;

        public static void Note(string modId, Assembly caller)
        {
            string name = caller?.GetName().Name;
            if (string.IsNullOrEmpty(name)) return;
            _assemblies.Add(name);
            _assemblies.Add(Self);
            if (!string.IsNullOrEmpty(modId)) _modToAssembly[modId] = name;
        }
    }
}
