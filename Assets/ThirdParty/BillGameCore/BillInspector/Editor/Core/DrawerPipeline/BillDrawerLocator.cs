using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Finds and instantiates the correct drawer for each attribute type.
    /// Scans all loaded assemblies for [BillCustomDrawer] on startup and caches results.
    /// </summary>
    public static class BillDrawerLocator
    {
        private static Dictionary<Type, Type> s_drawerMap;
        private static Dictionary<Type, IBillDrawer> s_drawerInstances;
        private static bool s_initialized;

        [UnityEditor.InitializeOnLoadMethod]
        private static void OnDomainReload() => ClearCache();

        public static void Initialize()
        {
            if (s_initialized) return;
            s_drawerMap = new Dictionary<Type, Type>();
            s_drawerInstances = new Dictionary<Type, IBillDrawer>();

            // Scan all assemblies for drawer types
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        var attr = type.GetCustomAttribute<BillCustomDrawerAttribute>();
                        if (attr != null && typeof(IBillDrawer).IsAssignableFrom(type))
                        {
                            s_drawerMap[attr.AttributeType] = type;
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Some assemblies may fail to load types — skip them
                }
            }

            s_initialized = true;
        }

        /// <summary>
        /// Gets or creates a cached drawer instance for the given attribute type.
        /// The Attribute property is updated each call to match the current attribute.
        /// </summary>
        public static IBillDrawer CreateDrawer(BillAttribute attribute)
        {
            Initialize();

            var attrType = attribute.GetType();
            if (!s_drawerMap.TryGetValue(attrType, out var drawerType))
                return null;

            if (!s_drawerInstances.TryGetValue(attrType, out var drawer))
            {
                drawer = (IBillDrawer)Activator.CreateInstance(drawerType);
                s_drawerInstances[attrType] = drawer;
            }

            // Update the Attribute property for the current field
            var attrProp = drawerType.GetProperty("Attribute",
                BindingFlags.Public | BindingFlags.Instance);
            attrProp?.SetValue(drawer, attribute);

            return drawer;
        }

        /// <summary>
        /// Checks if a drawer is registered for the given attribute type.
        /// </summary>
        public static bool HasDrawer(Type attributeType)
        {
            Initialize();
            return s_drawerMap.ContainsKey(attributeType);
        }

        public static void ClearCache()
        {
            s_drawerMap?.Clear();
            s_drawerInstances?.Clear();
            s_initialized = false;
        }
    }
}
