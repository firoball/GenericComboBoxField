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

Both implement **`IGenericComboBoxField`**, a non-generic interface covering
everything that doesn't require knowing `T` (config, not `Choices`/`value`) -
useful for holding a single reference to combos of different `T`, e.g.
`IGenericComboBoxField[] allCombos`.

## Files

- `GenericComboBoxField.cs`
- `ComboBoxField.cs`
- `IGenericComboBoxField.cs` (interface + the `DisplayCasing` enum)
- `GenericComboBoxFieldStyle.cs` (stylesheet auto-loader)
- `GenericComboBoxField.uss` (shared by both classes)

Put the four `.cs` files in a UI Toolkit-capable folder (e.g.
`Assets/UI/Controls/`). **The `.uss` must go under a `Resources` folder** -
e.g. `Assets/Resources/UI/Controls/GenericComboBoxField.uss` - so the control
can load it itself via `Resources.Load<StyleSheet>` at construction time. No
manual `StyleSheets` wiring in UXML or `styleSheets.Add(...)` in code is
needed (and doing it anyway is harmless - the control checks first and won't
add its stylesheet twice). See "Automatic stylesheet loading" below if your
project's `Resources` layout differs or you'd rather supply the `StyleSheet`
yourself.

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

## IGenericComboBoxField

The non-generic config surface, implemented by both classes:

```csharp
public interface IGenericComboBoxField
{
    int VisibleRowCount { get; set; }
    float MaxPopupHeight { get; set; }
    bool AllowAdd { get; set; }
    bool AllowDelete { get; set; }
    ComboBoxSortMode Ordering { get; set; }
    bool AllowDetailMode { get; set; }
    DisplayCasing DisplayCasing { get; set; }
    Func<string, string> Sanitizer { get; set; }
    void Refresh();
}
```

`Choices`, `value`, `ItemFactory`, and `DetailViewBuilder` aren't here - they're
generic-typed and can't be represented without knowing `T`. `Sanitizer` *is*
included despite being a delegate, since `Func<string, string>` doesn't
reference `T` at all. This interface is
for code that only needs to configure or inspect a combo box's *behavior*
(e.g. a settings panel that lists every combo box on screen and lets you
toggle `AllowDetailMode` on each), not read or write its selected value.

## Automatic stylesheet loading

Every `GenericComboBoxField<T>` adds its own stylesheet to itself on
construction - it doesn't rely on whatever panel/UXML happens to create it.
This matters when the combo box is nested inside another reusable component
whose author didn't (or couldn't) expose USS wiring - without this, those
instances would silently render unstyled.

The loader (`GenericComboBoxFieldStyle`, a small static class - not
`UnityEditor`-dependent, works identically in play mode, builds, and editor
windows) resolves the stylesheet as:

1. `GenericComboBoxFieldStyle.Override`, if you've assigned a `StyleSheet`
   directly (e.g. loaded via Addressables, an AssetBundle, or built at
   runtime) - checked fresh every time, always wins when set.
2. Otherwise `Resources.Load<StyleSheet>(GenericComboBoxFieldStyle.ResourcePath)`,
   default path `"UI/Controls/GenericComboBoxField"` - meaning the shipped
   `.uss` needs to sit at `Assets/Resources/UI/Controls/GenericComboBoxField.uss`
   (or wherever `ResourcePath` points, if you change it) for this to resolve.
   Loaded and cached once for the whole app on first access, regardless of
   how many different `T` you use `GenericComboBoxField<T>` with.

If neither produces a `StyleSheet`, a `Debug.LogWarning` fires once (not once
per instance) explaining where to place the file or how to override the path/
supply the sheet directly - the control keeps working either way, just
unstyled. If you change `ResourcePath` after the first control has already
been constructed, call `GenericComboBoxFieldStyle.ResetCache()` to force a
re-attempt; `Override` needs no such reset since it's re-checked every time.

## Sanitizer

`Sanitizer` (`Func<string, string>`, default `null`) cleans the text field's
content right after each user edit, before it's treated as real input:

```csharp
combo.Sanitizer = raw => new string(raw.Where(char.IsLetterOrDigit).ToArray());
```

It receives the field's full current content (not a single keystroke - that's
how the underlying `ChangeEvent<string>` reports edits) and, if the result
differs, the field is immediately rewritten to the sanitized text before
anything else runs. Everything downstream - live filtering, full-match/remove
detection, Add's trial construction, Undo's divergence check - just keeps
reading the text field as usual; by the time any of that runs, sanitization
has already happened, so none of it needs special-casing.

Only fires on genuine typing. Programmatic changes (a row pick, Undo,
`SetValueWithoutNotify`, clicking Clear) never go through it - those are all
already-known-good values, not raw keyboard input.

## Display casing

`DisplayCasing` (default `Keep`) cases the rendered `ToString()` text - the
closed text field's own display and brief-mode popup row labels:

```csharp
public enum DisplayCasing { Keep, Uppercase, Lowercase, CapitalizeFirst }
```

Purely cosmetic - conversion is invariant-culture (`ToUpperInvariant`/
`ToLowerInvariant`), and it never touches:

- search/typing (still `StartsWith` on the raw `ToString()`, case-insensitive),
- full-match / remove detection (still raw `ToString()`, case-insensitive),
- Add's trial construction (still raw text as typed),
- the underlying `T`/`value` itself.

So a case-only variant is still recognized as the same entry (no accidental
duplicate `+`) and still filters/selects correctly, regardless of what casing
it's displayed in. Detail-mode rows are untouched - they're a user-supplied
`VisualElement` built from the raw `T` instance, so apply (or ignore) casing
yourself there if you want it.

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

## Read-only Choices

If `Choices` is a read-only `IList<T>` (e.g. `list.AsReadOnly()` or a plain
`T[]` - both implement `IList<T>` but throw `NotSupportedException` on
`Add`/`Remove`), `+` and `-` are hidden automatically (checked via
`IList<T>.IsReadOnly`) instead of throwing when clicked. Useful when
something else owns list housekeeping and the combo box should be
selection/search-only.

## Layout

- The text field, with the clear (x), undo (↺), and dropdown-toggle (▼) buttons
  embedded inside it, sits in a fixed-height row on the left; the + and -
  buttons sit outside that row, in that order.
- The popup sits below that row, inside the same wrapper (so it's constrained
  to the wrapper's width, never extending under the outer +/- buttons) - as a
  normal in-flow block, not an absolutely positioned overlay. It's a column:
  the scrollable row list on top, the optional detail-mode footer beneath it.

## Popup layout: in-flow, not an overlay

UI Toolkit has no `z-index` - paint order is strictly visual-tree traversal
order, and `position: absolute` only affects layout, not paint order. Rather
than fight that (reparenting to a top-level overlay, tracking the field's
on-screen position, etc.), the popup is a **normal in-flow element**: opening
it grows the control's own layout space and pushes whatever comes after it
(in the same parent) down, instead of floating over other content. Closing it
shrinks back down. Since nothing ever overlaps anything, there's no paint
order to solve, and no position-tracking needed at all - the layout engine
handles it like any other resizing element.

Trade-off worth knowing: if this control sits inside a **scrollable list of
many fields**, opening it will visibly shift every later field down (and back
up on close) - more noticeable than a floating dropdown would be. If the
control sits inside a **size-constrained ancestor** (fixed height, `overflow:
hidden`), the popup can get clipped instead of properly showing all
`VisibleRowCount` rows - it doesn't create its own space to grow into the way
a floating overlay would. Neither applies to a single field in an otherwise
normal-height layout.

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
  - Home/End: jumps to the first/last entry. PageUp/PageDown: moves by
    `VisibleRowCount` rows. Same open/scroll/focus behavior as Up/Down.
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
requires a non-generic type. UXML attributes: `visible-row-count`,
`max-popup-height`, `allow-delete`, `ordering`
(`Default`/`Ascending`/`Descending`), `allow-detail-mode`, `display-casing`
(`Keep`/`Uppercase`/`Lowercase`/`CapitalizeFirst`), `allow-add`.

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
