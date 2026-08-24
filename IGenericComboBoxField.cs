namespace UI.Controls
{
    /// <summary>
    /// Casing applied to ToString() results when rendered: the closed text field's own display
    /// text and brief-mode popup row labels. Purely cosmetic - matching, search-while-typing,
    /// Add/Remove full-match detection, and Add's trial construction always compare against the
    /// raw (uncased) ToString() result, so this never changes matching behavior or the
    /// underlying T/value. Detail-mode rows are untouched too - they're a user-supplied
    /// VisualElement built from the raw T instance, so apply (or ignore) casing yourself there
    /// if you want it.
    /// </summary>
    public enum DisplayCasing
    {
        Keep,
        Uppercase,
        Lowercase,
        CapitalizeFirst
    }

    /// <summary>Ordering applied to the popup list.</summary>
    public enum ComboBoxSortMode
    {
        Default,
        Ascending,
        Descending
    }

    /// <summary>
    /// Non-generic surface of GenericComboBoxField&lt;T&gt; - everything that doesn't require
    /// knowing T, so code that only needs to configure or inspect the control (not read/write
    /// its value or Choices) can hold a single reference regardless of which T a given instance
    /// was built with, e.g. `IGenericComboBoxField[] allCombos` mixing GenericComboBoxField&lt;Unit&gt;,
    /// GenericComboBoxField&lt;int&gt;, ComboBoxField, etc.
    /// </summary>
    public interface IGenericComboBoxField
    {
        int VisibleRowCount { get; set; }
        float MaxPopupHeight { get; set; }
        bool AllowAdd { get; set; }
        bool AllowDelete { get; set; }
        ComboBoxSortMode Ordering { get; set; }
        bool AllowDetailMode { get; set; }
        DisplayCasing DisplayCasing { get; set; }

        void Refresh();
    }
}
