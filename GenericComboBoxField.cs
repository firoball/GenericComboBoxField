using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Controls
{
    /// <summary>
    /// Generic combo box control: text field with live-search dropdown against an IList&lt;T&gt;,
    /// plus optional Clear(x) / Add(+) / Remove(-) buttons to modify that list.
    /// Search, matching, and the brief popup row label are all based on T.ToString().
    /// No UnityEditor dependency - usable in Editor UI Toolkit windows and at runtime (UI Toolkit).
    ///
    /// This is a generic (non-UXML-instantiable) base. For UI Builder support, derive a concrete
    /// subclass for your T and add [UxmlElement] to it - see ComboBoxField (T = string) for the
    /// reference implementation, including forwarded [UxmlAttribute] properties.
    /// </summary>
    public partial class GenericComboBoxField<T> : VisualElement, INotifyValueChanged<T>, IGenericComboBoxField
    {
        private const string UssClassName = "combobox-field";
        private const string FieldWrapperUssClassName = UssClassName + "__field-wrapper";
        private const string TextFieldUssClassName = UssClassName + "__text-field";
        private const string InlineButtonsUssClassName = UssClassName + "__inline-buttons";
        private const string ClearButtonUssClassName = UssClassName + "__clear-button";
        private const string UndoButtonUssClassName = UssClassName + "__undo-button";
        private const string ArrowButtonUssClassName = UssClassName + "__arrow-button";
        private const string AddButtonUssClassName = UssClassName + "__add-button";
        private const string RemoveButtonUssClassName = UssClassName + "__remove-button";
        private const string PopupUssClassName = UssClassName + "__popup";
        private const string PopupListUssClassName = UssClassName + "__popup-list";
        private const string RowUssClassName = UssClassName + "__row";
        private const string RowDetailUssClassName = RowUssClassName + "--detail";
        private const string RowHighlightedUssClassName = RowUssClassName + "--highlighted";
        private const string MatchUssClassName = TextFieldUssClassName + "--match";
        private const string DetailFooterUssClassName = UssClassName + "__detail-footer";
        private const string DetailToggleUssClassName = UssClassName + "__detail-toggle";

        private readonly VisualElement _fieldWrapper;
        private readonly TextField _textField;
        private readonly Button _clearButton;
        private readonly Button _undoButton;
        private readonly Button _arrowButton;
        private readonly Button _addButton;
        private readonly Button _removeButton;
        private readonly VisualElement _popupContainer;
        private readonly ScrollView _scrollView;
        private readonly VisualElement _detailFooter;
        private readonly Toggle _detailToggle;

        private IList<T> _choices = new List<T>();
        private List<T> _displayList = new List<T>();
        private T _value;
        private int _highlightedIndex = -1;
        private bool _popupOpen;
        private bool _detailMode;
        private float _measuredBriefRowHeight = -1f;
        private float _measuredDetailRowHeight = -1f;

        /// <summary>Number of visible rows in the popup before it scrolls.</summary>
        public int VisibleRowCount { get; set; } = 6;

        /// <summary>
        /// Hard pixel cap on popup height, regardless of VisibleRowCount or row mode.
        /// &lt;= 0 disables the cap.
        /// </summary>
        public float MaxPopupHeight { get; set; } = 320f;

        /// <summary>Whether the - button (remove current full match from list) is enabled at all.</summary>
        public bool AllowDelete { get; set; } = true;

        /// <summary>Ordering applied to the popup list.</summary>
        public ComboBoxSortMode Ordering { get; set; } = ComboBoxSortMode.Default;

        /// <summary>
        /// Whether the detail-mode checkbox is shown at all. When false, the popup always renders
        /// in brief (ToString label) mode and the checkbox is not visible - this is the default,
        /// so a freshly-constructed control behaves exactly like a brief-only combo box.
        /// </summary>
        public bool AllowDetailMode { get; set; } = false;

        /// <summary>
        /// Casing applied to ToString() results when rendered: the closed text field's own
        /// display text and brief-mode popup row labels. Purely cosmetic - search, full-match/
        /// remove detection, and Add's trial construction always compare against the raw
        /// ToString() result, so this never changes matching behavior. Default: Keep.
        /// </summary>
        public DisplayCasing DisplayCasing { get; set; } = DisplayCasing.Keep;

        /// <summary>
        /// Builds the row content shown for an entry while detail mode is active. If null (the
        /// default), detail-mode rows fall back to the same ToString() label as brief mode.
        /// </summary>
        public Func<T, VisualElement> DetailViewBuilder { get; set; }

        /// <summary>
        /// Whether the + button (add current text to list) is enabled at all. Combined with
        /// ItemFactory / the built-in string-and-primitive fallback below (see CanConstructFromText).
        /// </summary>
        public bool AllowAdd { get; set; } = true;

        /// <summary>
        /// Constructs a new T instance from typed text for the Add(+) button. Args: the typed
        /// text, and the currently committed value (default(T) if nothing's selected yet) - the
        /// latter is handed in explicitly since there's no reference to the control itself
        /// available at the point this is normally wired up (e.g. an object initializer).
        /// Optional: if left null, string and primitive T (int, float, bool, etc.) are handled
        /// automatically - no callback needed for those. For any other T, the Add button stays
        /// hidden until a factory is supplied here, since constructing an arbitrary class from
        /// text isn't possible in general.
        /// </summary>
        public Func<string, T, T> ItemFactory { get; set; }

        /// <summary>
        /// The data list this control searches/edits. Assign a reference; if the underlying
        /// data can change externally (e.g. behind an interface), call Refresh() after changes.
        /// </summary>
        public IList<T> Choices
        {
            get => _choices;
            set
            {
                _choices = value ?? new List<T>();
                Refresh();
            }
        }

        /// <summary>The currently committed/selected item (set via row pick, Enter, or Add - not while merely typing).</summary>
        public T value
        {
            get => _value;
            set => SetValueInternal(value, notify: true);
        }

        public GenericComboBoxField()
        {
            AddToClassList(UssClassName);
            style.flexDirection = FlexDirection.Row;

            // Wrapper holds the text field plus the X/arrow buttons embedded inside it,
            // and defines the width the popup is constrained to (see PopupUssClassName in USS).
            _fieldWrapper = new VisualElement();
            _fieldWrapper.AddToClassList(FieldWrapperUssClassName);

            _textField = new TextField { isDelayed = false };
            _textField.AddToClassList(TextFieldUssClassName);
            _textField.RegisterValueChangedCallback(OnTextChanged);

            _clearButton = new Button(OnClearClicked) { text = "\u2715" }; // ✕
            _clearButton.AddToClassList(ClearButtonUssClassName);

            _undoButton = new Button(OnUndoClicked) { text = "\u21BA" }; // ↺
            _undoButton.AddToClassList(UndoButtonUssClassName);

            _arrowButton = new Button(OnArrowClicked) { text = "\u25BC" }; // ▼
            _arrowButton.AddToClassList(ArrowButtonUssClassName);

            var inlineButtons = new VisualElement();
            inlineButtons.AddToClassList(InlineButtonsUssClassName);
            inlineButtons.Add(_clearButton);
            inlineButtons.Add(_undoButton);
            inlineButtons.Add(_arrowButton);

            _fieldWrapper.Add(_textField);
            _fieldWrapper.Add(inlineButtons);

            // Popup container: absolutely positioned against the field wrapper (see USS), holds
            // the scrollable row list plus an optional detail-mode footer beneath it.
            _popupContainer = new VisualElement();
            _popupContainer.AddToClassList(PopupUssClassName);
            _popupContainer.style.display = DisplayStyle.None;
            _popupContainer.pickingMode = PickingMode.Position;

            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.AddToClassList(PopupListUssClassName);
            _scrollView.pickingMode = PickingMode.Position;
            _popupContainer.Add(_scrollView);

            _detailToggle = new Toggle("Details");
            _detailToggle.AddToClassList(DetailToggleUssClassName);
            _detailToggle.RegisterValueChangedCallback(OnDetailModeChanged);

            _detailFooter = new VisualElement();
            _detailFooter.AddToClassList(DetailFooterUssClassName);
            _detailFooter.Add(_detailToggle);
            _popupContainer.Add(_detailFooter);

            _fieldWrapper.Add(_popupContainer);

            _addButton = new Button(OnAddClicked) { text = "\u271A" }; // ✚
            _addButton.AddToClassList(AddButtonUssClassName);

            _removeButton = new Button(OnRemoveClicked) { text = "\u2212" }; // −
            _removeButton.AddToClassList(RemoveButtonUssClassName);

            Add(_fieldWrapper);
            Add(_addButton);
            Add(_removeButton);

            // TrickleDown: intercept before TextField's own internal KeyDownEvent handling
            // can consume arrow keys (which would otherwise stop them from ever reaching us).
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RegisterCallback<FocusOutEvent>(OnFocusOut);

            UpdateMatchState();
        }

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        /// <summary>Re-applies filtering/sorting and rebuilds the popup + button states.
        /// Call this after externally mutating the Choices list in place.</summary>
        public void Refresh()
        {
            BuildDisplayList();
            if (_popupOpen)
                BuildScrollViewRows();
            UpdateMatchState();
        }

        public void SetValueWithoutNotify(T newValue)
        {
            SetValueInternal(newValue, notify: false);
        }

        private void SetValueInternal(T newValue, bool notify)
        {
            var previous = _value;
            _value = newValue;
            _textField.SetValueWithoutNotify(DisplayFor(newValue));

            BuildDisplayList();
            UpdateMatchState();
            if (_popupOpen)
                BuildScrollViewRows();

            if (notify && !Equals(previous, newValue))
            {
                using var changeEvent = ChangeEvent<T>.GetPooled(previous, newValue);
                changeEvent.target = this;
                SendEvent(changeEvent);
            }
        }

        // ---------------------------------------------------------------
        // Text / matching
        // ---------------------------------------------------------------

        private static string LabelFor(T item) => item?.ToString() ?? string.Empty;

        /// <summary>
        /// The raw ToString() label, cased per DisplayCasing - used only where text is actually
        /// rendered (text field display, brief-mode row labels). Never used for matching.
        /// </summary>
        private string DisplayFor(T item)
        {
            var raw = LabelFor(item);
            return DisplayCasing switch
            {
                DisplayCasing.Uppercase => raw.ToUpperInvariant(),
                DisplayCasing.Lowercase => raw.ToLowerInvariant(),
                DisplayCasing.CapitalizeFirst => CapitalizeFirstInvariant(raw),
                _ => raw
            };
        }

        private static string CapitalizeFirstInvariant(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>
        /// Tries to produce a T from typed text: a custom ItemFactory always wins; otherwise
        /// string/primitive T are handled automatically (identity for string, Convert.ChangeType
        /// with invariant culture for primitives). Pure/side-effect-free - does not touch
        /// Choices or value, so it's safe to call on every keystroke just to drive button state.
        /// </summary>
        private bool TryCreateFromText(string text, out T result)
        {
            if (ItemFactory != null)
            {
                result = ItemFactory(text, _value);
                return (object)result != null;
            }

            if (typeof(T) == typeof(string))
            {
                result = (T)(object)text;
                return true;
            }

            if (typeof(T).IsPrimitive)
            {
                try
                {
                    result = (T)Convert.ChangeType(text, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception)
                {
                    result = default;
                    return false;
                }
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Typing only filters the popup - it does not change value/fire ChangeEvent&lt;T&gt;.
        /// A selection only commits via a row pick, Enter, or Add.
        /// </summary>
        private void OnTextChanged(ChangeEvent<string> evt)
        {
            BuildDisplayList();
            UpdateMatchState();

            if (!_popupOpen)
            {
                OpenPopup();
            }
            else
            {
                BuildScrollViewRows();
                ScrollToFirstMatch();
            }
        }

        private bool TryFindFullMatch(out T matchedEntry)
        {
            var text = _textField.value ?? string.Empty;
            foreach (var entry in _choices)
            {
                if (string.Equals(LabelFor(entry), text, StringComparison.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }
            }

            matchedEntry = default;
            return false;
        }

        private bool HasFactoryCapability =>
            ItemFactory != null || typeof(T) == typeof(string) || typeof(T).IsPrimitive;

        /// <summary>
        /// True if Choices can't be mutated (e.g. a List&lt;T&gt;.AsReadOnly() or a plain T[] -
        /// both implement IList&lt;T&gt; but throw NotSupportedException on Add/Remove). Add/
        /// Remove are hidden entirely in that case rather than throwing on click.
        /// </summary>
        private bool IsChoicesReadOnly => _choices.IsReadOnly;

        /// <summary>Whether the typed text currently differs from the committed value's label - i.e. whether there's anything to revert to.</summary>
        private bool CanUndo()
        {
            var text = _textField.value ?? string.Empty;
            return !string.Equals(text, DisplayFor(_value), StringComparison.Ordinal);
        }

        private void UpdateMatchState()
        {
            var text = _textField.value ?? string.Empty;
            var isFullMatch = TryFindFullMatch(out _);
            var hasText = !string.IsNullOrEmpty(text);
            var readOnly = IsChoicesReadOnly;
            var showAdd = AllowAdd && HasFactoryCapability && !readOnly;
            var showRemove = AllowDelete && !readOnly;

            _textField.EnableInClassList(MatchUssClassName, isFullMatch);

            _clearButton.SetEnabled(hasText);
            _undoButton.SetEnabled(CanUndo());

            // Only attempt the (cheap, side-effect-free) trial construction when it could
            // actually matter - avoids parsing on every keystroke for irrelevant states.
            var wantsAdd = showAdd && hasText && !isFullMatch;
            var canAdd = wantsAdd && TryCreateFromText(text, out _);

            _addButton.style.display = showAdd ? DisplayStyle.Flex : DisplayStyle.None;
            _addButton.SetEnabled(canAdd);
            _removeButton.style.display = showRemove ? DisplayStyle.Flex : DisplayStyle.None;
            _removeButton.SetEnabled(showRemove && isFullMatch);
        }

        /// <summary>
        /// Resets the typed text back to the committed value's label. Choices and value are
        /// never touched here - typing/clearing never changed them in the first place (see the
        /// value/commit model), so there's nothing to roll back beyond the text field's display.
        /// </summary>
        private void RevertText()
        {
            _textField.SetValueWithoutNotify(DisplayFor(_value));
            BuildDisplayList();
            UpdateMatchState();
            if (_popupOpen)
            {
                BuildScrollViewRows();
                ScrollToFirstMatch();
            }
        }

        private void OnUndoClicked()
        {
            RevertText();
            ClosePopup();
            FocusTextFieldDeferred();
        }

        private void OnClearClicked()
        {
            _textField.SetValueWithoutNotify(string.Empty);
            BuildDisplayList();
            UpdateMatchState();
            ClosePopup();
            FocusTextFieldDeferred();
        }

        // ---------------------------------------------------------------
        // Popup
        // ---------------------------------------------------------------

        private void OnArrowClicked()
        {
            if (_popupOpen)
                ClosePopup();
            else
                OpenPopup();
        }

        /// <summary>
        /// Whether the detail-mode checkbox is actually shown: requires both AllowDetailMode
        /// and a DetailViewBuilder. Without a builder, detail rows render identically to brief
        /// rows (same ToString() label), so a toggle with no visible effect would be confusing -
        /// this is always true for string/primitive T unless the caller supplies a builder anyway.
        /// </summary>
        private bool CanShowDetailToggle => AllowDetailMode && DetailViewBuilder != null;

        private void OpenPopup()
        {
            _popupOpen = true;

            // AllowDetailMode/DetailViewBuilder may have been (re)set any time after
            // construction - re-sync the footer's visibility and force brief mode if detail
            // mode isn't currently meaningful.
            var canShowDetailToggle = CanShowDetailToggle;
            _detailFooter.style.display = canShowDetailToggle ? DisplayStyle.Flex : DisplayStyle.None;
            if (!canShowDetailToggle)
                _detailMode = false;
            _detailToggle.SetValueWithoutNotify(_detailMode);

            BuildDisplayList();
            BuildScrollViewRows();
            _popupContainer.style.display = DisplayStyle.Flex;
            ScrollToFirstMatch();
        }

        private void ClosePopup()
        {
            _popupOpen = false;
            _popupContainer.style.display = DisplayStyle.None;
            _highlightedIndex = -1;
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            // Delay so a click on a row/button/toggle inside this control isn't treated as "outside".
            schedule.Execute(() =>
            {
                if (_popupOpen && !ContainsFocusedElement())
                    ClosePopup();
            });
        }

        private bool ContainsFocusedElement()
        {
            var focused = panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == this || Contains(focused));
        }

        private bool IsToggleFocused(VisualElement focused)
        {
            return focused != null && (focused == _detailToggle || _detailToggle.Contains(focused));
        }

        // ---------------------------------------------------------------
        // Detail mode
        // ---------------------------------------------------------------

        private void OnDetailModeChanged(ChangeEvent<bool> evt)
        {
            _detailMode = CanShowDetailToggle && evt.newValue;

            if (!_popupOpen)
                return;

            // Re-anchor to whatever was highlighted (falling back to the committed value) so the
            // same conceptual row stays in view across the instant re-layout.
            T anchor = (_highlightedIndex >= 0 && _highlightedIndex < _displayList.Count)
                ? _displayList[_highlightedIndex]
                : _value;

            BuildScrollViewRows();
            ReanchorTo(anchor);
        }

        private void ReanchorTo(T item)
        {
            if ((object)item == null)
                return;

            var idx = _displayList.FindIndex(e => EqualityComparer<T>.Default.Equals(e, item));
            if (idx < 0)
                return;

            SetHighlighted(idx);
            var rows = _scrollView.Children().ToList();
            if (idx < rows.Count)
                _scrollView.ScrollTo(rows[idx]);
        }

        // ---------------------------------------------------------------
        // List building
        // ---------------------------------------------------------------

        private void BuildDisplayList()
        {
            var text = _textField.value ?? string.Empty;
            IEnumerable<T> filtered = string.IsNullOrEmpty(text)
                ? _choices
                : _choices.Where(e => LabelFor(e).StartsWith(text, StringComparison.OrdinalIgnoreCase));

            _displayList = Ordering switch
            {
                ComboBoxSortMode.Ascending => filtered.OrderBy(LabelFor, StringComparer.OrdinalIgnoreCase).ToList(),
                ComboBoxSortMode.Descending => filtered.OrderByDescending(LabelFor, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => filtered.ToList()
            };
        }

        private void BuildScrollViewRows()
        {
            _scrollView.Clear();
            _highlightedIndex = -1;

            var useDetail = _detailMode && DetailViewBuilder != null;

            for (var i = 0; i < _displayList.Count; i++)
            {
                var entry = _displayList[i];
                VisualElement row;

                if (useDetail)
                {
                    row = new VisualElement { focusable = true, tabIndex = 0 };
                    row.AddToClassList(RowUssClassName);
                    row.AddToClassList(RowDetailUssClassName);
                    var content = DetailViewBuilder(entry);
                    if (content != null)
                        row.Add(content);
                }
                else
                {
                    row = new Label(DisplayFor(entry)) { focusable = true, tabIndex = 0 };
                    row.AddToClassList(RowUssClassName);
                }

                var index = i;
                row.RegisterCallback<MouseDownEvent>(_ => SelectEntry(_displayList[index]));
                row.RegisterCallback<MouseEnterEvent>(_ => SetHighlighted(index));
                _scrollView.Add(row);
            }

            ApplyPopupHeight();
        }

        private void ApplyPopupHeight()
        {
            var cached = _detailMode ? _measuredDetailRowHeight : _measuredBriefRowHeight;
            if (cached > 0f)
            {
                SetScrollViewMaxHeight(cached);
                return;
            }

            if (_scrollView.childCount == 0)
                return;

            // Measure the actual laid-out height of the first row via GeometryChangedEvent,
            // instead of guessing a pixel value - this keeps the control independent of any
            // particular project's USS row-height convention, and independent per mode since
            // detail rows are typically taller/variable compared to brief rows.
            var measuringDetailMode = _detailMode;
            var firstRow = _scrollView.Children().First();

            EventCallback<GeometryChangedEvent> handler = null;
            handler = evt =>
            {
                firstRow.UnregisterCallback(handler);

                if (evt.newRect.height <= 0f)
                    return;

                if (measuringDetailMode)
                    _measuredDetailRowHeight = evt.newRect.height;
                else
                    _measuredBriefRowHeight = evt.newRect.height;

                // Only apply if we're still showing the mode that was just measured - the user
                // may have already switched again while this callback was pending.
                if (measuringDetailMode == _detailMode)
                    SetScrollViewMaxHeight(evt.newRect.height);
            };
            firstRow.RegisterCallback(handler);
        }

        private void SetScrollViewMaxHeight(float rowHeight)
        {
            var target = VisibleRowCount * rowHeight;
            if (MaxPopupHeight > 0f)
                target = Mathf.Min(target, MaxPopupHeight);
            _scrollView.style.maxHeight = target;
        }

        private void ScrollToFirstMatch()
        {
            var text = _textField.value ?? string.Empty;
            if (string.IsNullOrEmpty(text) || _displayList.Count == 0)
                return;

            var idx = _displayList.FindIndex(e => LabelFor(e).StartsWith(text, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;
            SetHighlighted(idx);

            var rows = _scrollView.Children().ToList();
            if (idx >= 0 && idx < rows.Count)
                _scrollView.ScrollTo(rows[idx]);
        }

        private void SetHighlighted(int index)
        {
            var rows = _scrollView.Children().ToList();
            if (_highlightedIndex >= 0 && _highlightedIndex < rows.Count)
                rows[_highlightedIndex].RemoveFromClassList(RowHighlightedUssClassName);

            _highlightedIndex = index;

            if (_highlightedIndex >= 0 && _highlightedIndex < rows.Count)
                rows[_highlightedIndex].AddToClassList(RowHighlightedUssClassName);
        }

        private void SelectEntry(T entry)
        {
            // Close first: this avoids rebuilding (and thereby destroying) the scrollview rows
            // - one of which may still hold keyboard focus - while SetValueInternal runs below.
            ClosePopup();
            SetValueInternal(entry, notify: true);
            FocusTextFieldDeferred();
        }

        /// <summary>
        /// Focuses the text field on the next frame instead of synchronously. Calling Focus()
        /// immediately inside the same key event that a popup row is still resolving focus for
        /// does not reliably move the caret - it can leave the row as the focused element, which
        /// then causes the next arrow key press to be misread as "a row has focus".
        /// </summary>
        private void FocusTextFieldDeferred()
        {
            schedule.Execute(() => _textField.Focus());
        }

        // ---------------------------------------------------------------
        // Keyboard navigation
        // ---------------------------------------------------------------

        private void OnKeyDown(KeyDownEvent evt)
        {
            var focused = panel?.focusController?.focusedElement as VisualElement;

            // Let the detail-mode checkbox handle its own keys (Space/Enter) untouched.
            if (IsToggleFocused(focused))
                return;

            var rowFocused = focused != null && focused.ClassListContains(RowUssClassName);

            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    if (!_popupOpen) OpenPopup();
                    MoveHighlight(1, focusRow: true);
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    if (!_popupOpen) OpenPopup();
                    MoveHighlight(-1, focusRow: true);
                    evt.StopPropagation();
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Backspace:
                    // While a popup row has focus, these hand focus back to the text field
                    // instead of doing nothing (rows have no caret/content to act on).
                    if (rowFocused)
                    {
                        FocusTextFieldDeferred();
                        evt.StopPropagation();
                    }
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_popupOpen && _highlightedIndex >= 0 && _highlightedIndex < _displayList.Count)
                    {
                        SelectEntry(_displayList[_highlightedIndex]);
                        evt.StopPropagation();
                    }
                    break;
                case KeyCode.Escape:
                    if (CanUndo())
                    {
                        // First press (text diverged from the committed value): revert the
                        // display only, leave the popup state as-is. A second press (now
                        // nothing to undo) falls through to the close-popup branch below.
                        RevertText();
                        evt.StopPropagation();
                    }
                    else if (_popupOpen)
                    {
                        ClosePopup();
                        FocusTextFieldDeferred();
                        evt.StopPropagation();
                    }
                    break;
            }
        }

        private void MoveHighlight(int delta, bool focusRow = false)
        {
            if (_displayList.Count == 0) return;
            var next = _highlightedIndex < 0 ? 0 : _highlightedIndex + delta;
            next = Mathf.Clamp(next, 0, _displayList.Count - 1);
            SetHighlighted(next);

            var rows = _scrollView.Children().ToList();
            if (next >= 0 && next < rows.Count)
            {
                _scrollView.ScrollTo(rows[next]);
                if (focusRow)
                    rows[next].Focus();
            }
        }

        // ---------------------------------------------------------------
        // Add / Remove
        // ---------------------------------------------------------------

        private void OnAddClicked()
        {
            if (!AllowAdd || !HasFactoryCapability || IsChoicesReadOnly) return;

            var text = _textField.value;
            if (string.IsNullOrEmpty(text)) return;
            if (TryFindFullMatch(out _)) return;
            if (!TryCreateFromText(text, out var newItem)) return;

            _choices.Add(newItem);
            Refresh();
            SelectEntry(newItem);
        }

        private void OnRemoveClicked()
        {
            if (!AllowDelete || IsChoicesReadOnly) return;
            if (!TryFindFullMatch(out var matched)) return;

            _choices.Remove(matched);
            Refresh();
        }
    }
}
