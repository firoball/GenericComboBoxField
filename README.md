# ComboBoxField / GenericComboBoxField

Custom UI Toolkit combo box controls: a text input with live search, a dropdown
popup, and clear(x) / undo(↺) / add(+) / remove(-) buttons to edit the current
text and the underlying list. Supports an optional detailed popup-row view for
non-primitive types. No `UnityEditor` dependency - works in editor windows and
at runtime.

Two classes:

- **`GenericComboBoxField<T>`** - the control, generic over any `T`. Search,
  matching, and the brief popup row label all use `T.ToString()`.
- **`ComboBoxField`** - `GenericComboBoxField<string>` with UI Builder / UXML
  support. Behaves exactly like a plain string combo box out of the box.

## Files

- `GenericComboBoxField.cs`
- `ComboBoxField.cs`
- `GenericComboBoxField.uss` (shared by both classes)

Put all three in a UI Toolkit-capable folder (e.g. `Assets/UI/Controls/`).
The `.uss` needs to be referenced by your panel/UXML (`StyleSheets` in UXML,
or `visualElement.styleSheets.Add(...)` in code).

## `ComboBoxField` usage (string list)

```csharp
var combo = new ComboBoxField
{
    Choices = myStringList,       // IList<string> reference
    VisibleRowCount = 6,          // visible rows in the popup, rest scrolls
    AllowAdd = true,
    AllowDelete = true,
    Ordering = ComboBoxSortMode.Ascending // Default | Ascending | Descending
};

combo.RegisterValueChangedCallback(evt =>
{
    Debug.Log($"New value: {evt.newValue}");
});

root.Add(combo);
```

## `GenericComboBoxField<T>` usage (any class)

```csharp
var combo = new GenericComboBoxField<Unit>
{
    Choices = myUnitList,                 // IList<Unit>
    VisibleRowCount = 6,
    MaxPopupHeight = 320,                 // hard cap in px, regardless of row mode/count
    AllowDetailMode = true,               // shows the "Details" checkbox in the popup footer
    ItemFactory = CreateUnitFromText,     // any Func<string, Unit> - not tied to a string ctor
    DetailViewBuilder = unit =>
    {
        // Build whatever VisualElement you want for a detailed row. Called once per
        // visible row while detail mode is on. Falls back to ToString() if left null.
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        row.Add(new Label(unit.Name));
        row.Add(new Label(unit.Hp.ToString()));
        return row;
    }
};

combo.RegisterValueChangedCallback(evt =>
{
    Debug.Log($"Selected: {evt.newValue}");
});

root.Add(combo);

// ItemFactory just needs to be a Func<string, Unit, Unit> - a lambda, a local function, or
// (as here) a named method. Nothing requires Unit to have a string constructor; build it
// however your class actually needs to be built (lookups, defaults, validation, etc.). The
// second parameter is the currently committed value (default(Unit) if nothing's selected
// yet) - handy for e.g. copying fields from the previous selection into the new entry.
Unit CreateUnitFromText(string text, Unit previous)
{
    return new Unit { Name = text, Hp = previous?.Hp ?? 10 };
}
```

## Value model

`value` (and the `ChangeEvent<T>` it fires) is the **committed selection**, not
the raw typed text. It changes when:

- the user clicks a popup row,
- the user presses Enter with a row highlighted,
- the `+` button creates and selects a new item, or
- you set `combo.value = someItem` / `combo.SetValueWithoutNotify(someItem)` in code.

Typing just filters the popup - it does not change `value` or fire
`ChangeEvent<T>` on every keystroke. The internal text field isn't exposed
publicly by design (a generic `T` can't represent "partial text"); if you
need live text as the user types, wire a callback on the specific `T`
construction path instead (e.g. via `ItemFactory`).

## Detail mode

- Off by default (`AllowDetailMode = false`) - no checkbox, brief rows only.
- The checkbox only appears when `AllowDetailMode = true` **and**
  `DetailViewBuilder` is set. Without a builder, detail rows would look
  identical to brief rows (same `ToString()` label), so a toggle with no
  visible effect is hidden rather than shown - this applies to `string` and
  primitive `T` in particular, unless you supply a builder for them anyway.
- When shown, the "Details" checkbox sits in a footer below the popup list.
  Toggling it switches all popup rows between the brief `ToString()` label
  and whatever `DetailViewBuilder` returns.
- Popup height: `VisibleRowCount` rows at the *current* mode's row height,
  capped by `MaxPopupHeight` (hard limit, `<= 0` disables it). Brief and
  detail row heights are measured and cached independently, since detail rows
  are typically taller. Switching modes re-lays out instantly (no animation)
  and re-anchors the scroll position to the row that was highlighted (or the
  current `value`) before the switch.
- The checkbox is excluded from the popup's arrow-key/Enter row navigation and
  from the "click outside closes the popup" check, so toggling it never closes
  the popup or disrupts keyboard navigation. Reaching it requires an explicit
  Tab or a mouse click.

## Add / Remove

- `+` needs a way to construct a `T` from typed text. This is automatic for
  `string` and any primitive `T` (`int`, `float`, `bool`, etc. - detected via
  `Type.IsPrimitive`) - no callback required. For any other class, set
  `ItemFactory` (`Func<string, T, T>` - text and the previously committed
  value); if it's left null and `T` isn't
  string/primitive, the button stays hidden. `AllowAdd` (default `true`) is an
  extra on/off switch on top of that - set it `false` to hide `+` even when
  construction would otherwise be possible.
  The button is enabled only when there's text, no full match, and (for
  primitives) the text actually parses - this check is a pure trial
  construction, it never touches `Choices` or `value`, so it's safe to run on
  every keystroke and never collides with the commit-only `ChangeEvent<T>`
  behavior below.
  On click it constructs the item, adds it to `Choices`, and selects it.
- `-` removes the current full match (`T.ToString()` case-insensitive equals
  the typed text) from `Choices`. `AllowDelete` (default `true`) both hides and
  disables the button when `false`, mirroring how `AllowAdd` gates `+`.

## External list changes

`Choices` only holds a reference. If the list is modified from outside
(e.g. through an interface/provider) without going through the `Choices`
setter, call:

```csharp
combo.Refresh();
```

afterwards so filtering, sorting, and button states get recalculated.

## Layout

- The text field, with the clear (x), undo (↺), and dropdown-toggle (▼) buttons
  embedded inside it, sits in one wrapper on the left; the + and - buttons sit
  outside it, in that order.
- The popup is constrained to the wrapper's width, so it never extends past the
  text field under the outer +/- buttons. It's a column: the scrollable row
  list on top, the optional detail-mode footer beneath it.

## Behavior

- Typing filters live (`StartsWith` on `ToString()`, case-insensitive), auto-opens
  the popup, and scrolls to the first match.
- A full match (case-insensitive) makes the text field's text bold with a slightly
  lighter background - no color coding.
- The x button clears the text field and closes the popup.
- The ↺ (undo) button restores the text field to the currently committed
  `value`'s label and closes the popup. Always visible, enabled only while
  the typed text differs from that label - i.e. while there's actually
  something to revert. Never touches `Choices` or `value` itself: typing
  never changed them, so there's nothing to roll back beyond the display.
- Keyboard:
  - Up/Down (while typing or while a popup row is focused): opens the popup if
    closed, moves the highlighted/selected row, scrolls it into view, and moves
    keyboard focus onto that row.
  - Left/Right (while a popup row has focus): moves focus back to the text field.
  - Enter: selects the highlighted row.
  - Escape: progressive. If the typed text differs from the committed value's
    label, the first press reverts the text (same as the ↺ button, but leaves
    the popup open). A second press (or any press when nothing's diverged)
    closes the popup and returns focus to the text field.
- Popup row height is measured once per mode from the first rendered row of that
  mode (`GeometryChangedEvent`), so the control doesn't assume any particular
  project's USS row-height convention.

## UI Builder / UXML

Only the concrete `ComboBoxField` (T = string) supports UXML - `[UxmlElement]`
requires a non-generic type. UXML attributes: `visible-row-count`, `allow-add`,
`allow-delete`, `ordering` (`Default`/`Ascending`/`Descending`).

For UI Builder support with a specific `T`, derive your own concrete subclass
of `GenericComboBoxField<YourType>` and add `[UxmlElement]` to it, following
the pattern in `ComboBoxField.cs`.

## License

MIT - see [LICENSE.md](LICENSE.md). Free to use for anything; attribution
is appreciated but not required beyond keeping the license notice with the
code, per the MIT terms.

## Disclaimer

Provided as-is, without warranty of any kind (see LICENSE.md). Review and
test it before relying on it in production - in particular the keyboard
navigation and popup-closing logic, which depend on your project's focus
handling and panel setup.

---
*This control (code, styles, and this README) was generated by Claude (Anthropic).*
