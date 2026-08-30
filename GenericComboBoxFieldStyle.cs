using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Controls
{
    /// <summary>
    /// Shared, non-generic loader for the stylesheet all GenericComboBoxField&lt;T&gt; instances
    /// use, regardless of T (a static field on GenericComboBoxField&lt;T&gt; itself would be
    /// duplicated per closed generic type - this is deliberately outside the generic class so
    /// the asset is only ever loaded once for the whole app).
    /// </summary>
    public static class GenericComboBoxFieldStyle
    {
        /// <summary>
        /// Resource path (no extension) the shared stylesheet is auto-loaded from via
        /// Resources.Load&lt;StyleSheet&gt; - e.g. the shipped GenericComboBoxField.uss placed at
        /// Assets/Resources/UI/Controls/GenericComboBoxField.uss. Override if your project uses
        /// a different Resources layout. Changing this after the stylesheet has already been
        /// loaded has no effect unless you also call ResetCache().
        /// </summary>
        public static string ResourcePath { get; set; } = "GenericComboBoxField";

        /// <summary>
        /// Assign directly to skip Resources.Load entirely - e.g. if you load or build the
        /// StyleSheet yourself (Addressables, AssetBundle, generated at runtime, etc.). Takes
        /// priority over ResourcePath whenever non-null, and is re-checked on every access, so
        /// no caching/reset is needed for this path specifically.
        /// </summary>
        public static StyleSheet Override { get; set; }

        private static StyleSheet s_LoadedFromResources;
        private static bool s_LoadAttempted;
        private static bool s_WarnedOnce;

        /// <summary>
        /// The stylesheet GenericComboBoxField&lt;T&gt; adds to itself on construction. Null if
        /// neither Override nor a Resources.Load against ResourcePath produced anything - the
        /// control still functions in that case, just unstyled (a warning is logged once).
        /// </summary>
        public static StyleSheet Shared
        {
            get
            {
                if (Override != null)
                    return Override;

                if (!s_LoadAttempted)
                {
                    s_LoadAttempted = true;
                    s_LoadedFromResources = Resources.Load<StyleSheet>(ResourcePath);

                    if (s_LoadedFromResources == null && !s_WarnedOnce)
                    {
                        s_WarnedOnce = true;
                        Debug.LogWarning(
                            "GenericComboBoxField: could not auto-load its stylesheet from " +
                            $"Resources/{ResourcePath}.uss - place the shipped " +
                            $"GenericComboBoxField.uss under a 'Resources' folder at that path " +
                            $"(e.g. Assets/Resources/{ResourcePath}.uss), set " +
                            "GenericComboBoxFieldStyle.ResourcePath to match your project's " +
                            "layout, or assign GenericComboBoxFieldStyle.Override directly. " +
                            "The control still works, just unstyled.");
                    }
                }

                return s_LoadedFromResources;
            }
        }

        /// <summary>Forces the next access to retry Resources.Load - e.g. after changing ResourcePath at runtime.</summary>
        public static void ResetCache()
        {
            s_LoadAttempted = false;
            s_LoadedFromResources = null;
            s_WarnedOnce = false;
        }
    }
}
