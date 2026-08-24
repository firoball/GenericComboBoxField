using UnityEngine.UIElements;

namespace UI.Controls
{
    /// <summary>
    /// Convenience string combo box: GenericComboBoxField&lt;string&gt; with UI Builder / UXML
    /// support and the same Add/Delete/Ordering API as before. Detail mode is off by default
    /// (AllowDetailMode = false), so a freshly-constructed ComboBoxField behaves exactly like a
    /// brief-only combo box. Add works out of the box - string is one of the base class's
    /// built-in constructible types, no ItemFactory needed.
    /// </summary>
    [UxmlElement]
    public partial class ComboBoxField : GenericComboBoxField<string>
    {
        /// <summary>Number of visible rows in the popup before it scrolls.</summary>
        [UxmlAttribute("visible-row-count")]
        public new int VisibleRowCount
        {
            get => base.VisibleRowCount;
            set => base.VisibleRowCount = value;
        }

        /// <summary>Hard pixel cap on popup height, regardless of VisibleRowCount. &lt;= 0 disables it.</summary>
        [UxmlAttribute("max-popup-height")]
        public new float MaxPopupHeight
        {
            get => base.MaxPopupHeight;
            set => base.MaxPopupHeight = value;
        }

        /// <summary>Whether the - button (remove current full match from list) is enabled at all.</summary>
        [UxmlAttribute("allow-delete")]
        public new bool AllowDelete
        {
            get => base.AllowDelete;
            set => base.AllowDelete = value;
        }

        /// <summary>Ordering applied to the popup list.</summary>
        [UxmlAttribute("ordering")]
        public new ComboBoxSortMode Ordering
        {
            get => base.Ordering;
            set => base.Ordering = value;
        }

        /// <summary>
        /// Whether the detail-mode checkbox is allowed to show at all (still also requires
        /// DetailViewBuilder to be set in code - delegates aren't UXML-settable).
        /// </summary>
        [UxmlAttribute("allow-detail-mode")]
        public new bool AllowDetailMode
        {
            get => base.AllowDetailMode;
            set => base.AllowDetailMode = value;
        }

        /// <summary>Casing applied to the rendered ToString() label - display only, never affects matching.</summary>
        [UxmlAttribute("display-casing")]
        public new DisplayCasing DisplayCasing
        {
            get => base.DisplayCasing;
            set => base.DisplayCasing = value;
        }

        /// <summary>Whether the + button (add current text to list) is enabled at all.</summary>
        [UxmlAttribute("allow-add")]
        public new bool AllowAdd
        {
            get => base.AllowAdd;
            set => base.AllowAdd = value;
        }
    }
}
