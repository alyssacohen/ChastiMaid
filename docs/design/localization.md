# Localization

ChastiMaid is localized with **gettext** ([GetText.NET](https://github.com/perpetualKid/GetText.NET)).
English is the source language: the text in the code *is* the message id (msgid). Each other language is a
`.po` file translated with [Poedit](https://poedit.net/).

```
locale/
  ChastiMaid.pot     template: every message of the app (generated, never edited by hand)
  it.po              Italian translation (source of truth, edited in Poedit)
tools/ChastiMaid.L10n/
  extract            regenerates the template and updates every .po
  compile            .po -> .mo, run automatically by every build
```

The `.mo` catalogs are build output, never committed: the build compiles `locale/*.po` into
`obj/locale/` and ships them as `Content/locale/{lang}/LC_MESSAGES/ChastiMaid.mo` (`publish.ps1` moves
`Content/` to the root of the portable layout as before). Untranslated and **fuzzy** entries are left out of
the `.mo`, so they show in English until a translator confirms them.

## In the code

### C# — `ILocalizer`

ViewModels get the localizer from `MainWindowViewModel.Localizer` (usually through a
`private ILocalizer L => _mainVm.Localizer;`); child ViewModels and helpers receive it as a parameter.
`Localizer.Current` (static) is only for code that cannot receive it: the composition root (`App.axaml.cs`),
the `Tr` markup extension and controls without a ViewModel (`DebtWidget`).

| Need | Call |
|---|---|
| Text | `L.T("Cancel")` |
| Text with values | `L.T("Held: {0}", count)` — placeholders are filled with the **invariant** culture |
| Plural | `L.N("{0} line", "{0} lines", n, n)` — `n` picks the form, the arguments fill the placeholders |
| Same English, different meaning | `L.P("safe", "Open")` (msgctxt) |
| Text kept in a table, translated later | `Localizer.Mark("…")` in the table, `L.T(text)` when shown |
| Amounts | `L.T("Debt: {0}", CurrencyConverter.Format(debt))` — never `£{0:F2}` in a msgid |

The extractor only sees **string literals**:

- no interpolation: `L.T($"… {n}")` is rejected by `extract`; use `L.T("… {0}", n)`;
- no ternary inside the call: `L.T(flag ? "a" : "b")` is silently missed; write `flag ? L.T("a") : L.T("b")`;
- no plural by hand (`n == 1 ? … : …`): use `L.N`, gettext knows each language's plural rules.

Never translate identifiers: persistence keys, enum values, JSON ids, file names, setting keys. Store the
identifier and translate when showing it (see `PunishmentItemViewModel.ReasonText`, `WardrobeLayer`).

### XAML — `{l:Tr}`

Purely static labels use the markup extension (namespace `xmlns:l="using:ChastiMaid.Localization"`):

```xml
<Button Content="{l:Tr 'New Game'}" />
<TextBlock Text="{l:Tr 'Empty', Context='save slot'}" />
<TextBlock Text="{l:Tr &quot;Today's Outfit&quot;}" />          <!-- text with an apostrophe -->
<TextBlock Classes="LabelUI" Text="{conv:UpperCase {l:Tr 'Name'}}" />
```

Anything dynamic, conditional or data-dependent (counts, amounts, states) comes from the ViewModel through a
binding. `Tr` resolves when the view is built; a language change rebuilds the screens.

### Notes for translators

A comment on the line right above the text becomes a `#.` note in the template:

```csharp
// translators: shown after a large number, e.g. "3  items selected"
SelectedCountCaption = L.N("item selected", "items selected", n);
```

```xml
<!-- translators: label of a save slot with no game in it -->
<TextBlock Text="{l:Tr 'Empty', Context='save slot'}" />
```

A note over several lines repeats `translators:` on each line. Add one whenever the context is not obvious:
what a placeholder holds, a length limit for a button, the gender of the noun a one-word label refers to.

## Workflow

### Adding or changing a string

1. Write the English text in the code with `L.T`/`L.N`/`L.P` or `{l:Tr '…'}`.
2. Run the extraction from the repository root:

   ```powershell
   dotnet run --project tools/ChastiMaid.L10n -- extract
   ```

   It regenerates `locale/ChastiMaid.pot` (GetText.NET.Extractor for C#, plus the `{l:Tr}` labels and the
   translator notes) and updates every `locale/*.po`: new messages are added untranslated, references and
   notes are refreshed, messages no longer in the code are dropped and listed in the output.
3. Translate the new entries in Poedit (next section) and commit the code, the `.pot` and the `.po` together.

Changing an English msgid makes it a new message: its old translation is dropped. When the change is only a
correction, edit the msgid in each `.po` first (or re-enter the translation afterwards).

### Translating in Poedit

1. Open `locale/it.po` in Poedit. Do **not** use "Update from POT": `extract` already keeps the `.po`
   in sync.
2. Translate the empty entries; review the **fuzzy** ones (Poedit shows them as "needs work") and clear the
   flag to confirm them.
3. Keep every placeholder (`{0}`, `{1:F2}`, …) and fill every plural form.
4. Save. Poedit also writes a `.mo` next to the `.po`; it is ignored, the build makes its own.

Tone: in-game text follows `docs/design/TONE_GUIDE.md` (Madame's voice; Italian uses *voi*). The Settings
screens are technical: neutral, impersonal wording in every language.

### Building

Nothing to do: `dotnet build`, `dotnet run`, `dotnet test` and `publish.ps1` compile the `.po` files. A
malformed `.po` fails the build with `error L10N001` and the file name.

### Checking

`dotnet test` runs `TranslationFileTests` and `XamlTextTests`:

- every `.po` parses, has a valid `Language` and `Plural-Forms` header, keeps the placeholders of each msgid in
  every form, fills as many plural forms as the header declares, and contains every msgid of the template;
- untranslated and fuzzy entries are only reported (detailed output:
  `dotnet test --filter "FullyQualifiedName~Po_ContainsEveryMsgid" --logger "console;verbosity=detailed"`);
- no `.axaml` contains a literal user-visible text; real exceptions (the app name, glyphs) are listed with
  their reason in `XamlTextTests.Allowed`.

### Adding a language

1. Create `locale/{code}.po` (two-letter code) from the template, e.g. in Poedit with
   *File › New from POT/PO file…* on `locale/ChastiMaid.pot`, setting the language. Check that the header
   has `Language: {code}` and the right `Plural-Forms`.
2. Translate it.
3. Add the code to `App.SupportedLanguages` and an entry to `SettingsViewModel.AvailableLanguages` (the
   language's own name, e.g. `new("de", "Deutsch")`).
4. Optional but recommended: a seed contract `Content/{code}/default.contract.json` with the default wardrobe,
   rules and chores in that language (otherwise the English one is used), and speech models under
   `Content/Speech/` for the speech features.

## What is not translated through gettext

- **Seed content** (`Content/{lang}/default.contract.json`): wardrobe, rules and chores are user data, one file
  per language. Only the in-code fallback (`MinimalContentSeeder`) goes through gettext, translated once when
  the profile is created.
- **Contract files** exchanged between players keep the language they were written in.
- **Persisted data formats**: `Outfit.ToString()` (the saved outfit text), punishment reason identifiers,
  layer orders.
- **Language names** in the selector are shown in their own language.
