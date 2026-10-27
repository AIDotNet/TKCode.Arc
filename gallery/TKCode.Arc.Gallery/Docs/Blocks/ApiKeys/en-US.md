---
title: API keys
summary: API key management where a Create key pill unfolds into a form, the new secret is shown once, rows open into usage details and a hold revokes a key.
---

<Demo name="Preview" />

## When to use

`ApiKeys` is the keys page of an API product's developer settings. A header explains what the keys are for, a toolbar
counts the active keys, and each key is a row with its name, test or live badge, the last four characters, its scopes and
when it was last used. Creating and revoking happen in place, so people never leave the list:

- **Creating a key.** The "Create key" pill unfolds into a form: a name, Test or Live, and the scopes. Your server
  creates the key, and the same panel then shows the full secret once, with a copy button and a warning, while the new
  row slides in at the top of the list, highlighted until the panel closes.
- **Checking a key.** A row opens into its details: who created it, the last request (when and where from) and how many
  requests it signed in the last 30 days. Keys that have not been used for a while carry a warning.
- **Revoking a key.** The details end with a hold-to-revoke button. Holding it for 1.2 seconds strikes the row through;
  your server revokes the key and the row folds away.

## When not to use

- **Secrets people read again.** The block shows a secret exactly once. For values that stay readable (configuration,
  environment variables), use a masked field with a reveal button instead.
- **Personal access tokens with expiry rules or many permissions.** The form offers a name, two environments and a flat
  list of scopes. A token with expiry dates, resource pickers or per-project permissions needs a full form page.
- **Hundreds of keys.** Rows are not virtualized; the block is designed for a few to a few dozen keys. Search and paging
  belong in a data table.
- **Read-only lists.** If nobody can create or revoke keys here, a plain list or a data table is simpler.

## Usage

The block derives from Avalonia's `TemplatedControl`. Each key is an `ApiKeyItem` (an `AvaloniaObject`): a name, the
environment, the prefix and last four characters that identify it (never the secret), its scopes, the creation date and
creator, the last use and the request count. The scopes the form offers are `ApiKeyScope`s with a name, a description and
whether new keys start with them ticked.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ApiKeys x:Name="Keys"
                 Description="Keys let your backend call the Northwind Relay API. Store them in a vault, never in client code." />
</UserControl>
```

```csharp
Keys.Scopes.Add(new ApiKeyScope { Name = "orders:write", Description = "Create, update and cancel orders" });
Keys.Scopes.Add(new ApiKeyScope { Name = "accounts:read", Description = "Read account profiles and contacts", IsDefault = true });

var checkout = new ApiKeyItem
{
    Key = "checkout",
    Name = "Checkout service",
    Environment = ApiKeyEnvironment.Live,
    Prefix = "rk_live_",
    LastFour = "7c3e",
    LastUsedAt = DateTimeOffset.Now.AddMinutes(-4),
    CreatedAt = new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero),
    CreatedBy = "Lena Ortiz",
    LastRequestText = "4 min ago from 18.144.62.10, Frankfurt",
    RequestsLast30Days = 942_317,
};
checkout.Scopes.AddRange(["orders:write", "accounts:read"]);
Keys.Keys.Add(checkout);
```

### Anatomy

- **Header**: `Title` (a level 2 heading, "API keys" by default) and `Description`, at most 520 wide.
- **Toolbar**: the count ("5 active keys", the number rolling up or down as it changes) and the "Create key" pill at the
  end. The whole pill is clickable, its text included. While the panel is open the count fades out.
- **Rows**: a whole-row button with four columns (name, badge and mask; scope chips; last use and creation date; a
  chevron). Its details open below it: facts, then a line with a note and the hold-to-revoke button.
- **Empty state**: a title, a line and an optional button (`EmptyActionText`), shown once the last row has folded away.

### Creating keys on your server

Set `GenerateAction` to the call that creates the key. It receives an `ApiKeyRequest` (the trimmed name, the
environment, the ticked scopes and the attempt number) and a cancellation token, and returns an `ApiKeyCreated`: the new
row and the one-time secret. While it runs `PanelState` is `Generating`: Generate shows a spinner and keeps the focus,
the fields are locked, and Escape cancels the request and returns to the form. The block keeps the secret only while the
reveal is open and drops it when the panel closes.

```csharp
Keys.GenerateAction = async (request, cancellation) =>
{
    var created = await api.CreateAsync(request.Name, request.Environment, request.Scopes, cancellation);
    var item = new ApiKeyItem
    {
        Key = created.Id,
        Name = request.Name,
        Environment = request.Environment,
        Prefix = created.Prefix,
        LastFour = created.Secret[^4..],
        CreatedAt = DateTimeOffset.Now,
        CreatedBy = currentUser.Name,
    };
    item.Scopes.AddRange(request.Scopes);
    return new ApiKeyCreated(item, created.Secret);
};
```

The new row is inserted at the top of `Keys` (or of `KeysSource` when it is a list you can add to), `KeyCreated` fires
and `KeyCreatedCommand` runs. Without a `GenerateAction` the block simulates a server: about 730 ms, then a random
40-character secret starting with `SimulatedPrefixFormat`, recorded as created by `CurrentUserName`. Use that only for
prototypes and demos.

The form resets on every open: an empty name, `DefaultEnvironment`, and the scopes marked `IsDefault`. An empty name
shows an error under the field and keeps the focus there; no scope shows an error under the scopes and focuses the first
one. Both clear as soon as the person types or ticks a scope. Enter in the name field submits.

### Failure and retry

When `GenerateAction` throws, the form stays open with everything the person entered, an error line appears above the
buttons (the exception's message, or `ApiKeyLabels.GenerateFailed`), `GenerateError` holds it and `KeyCreateFailed`
fires. Pressing Generate again retries with the next attempt number. When `RevokeAction` throws, the struck-through row
comes back as it was, the hold button resets, the error is announced and `KeyRevokeFailed` fires.

<Demo name="Failing" />

### Revoking

Holding the button for `RevokeHoldDuration` (1.2 s, by pointer or by holding Space or Enter) confirms. `KeyRevoking`
fires first and can cancel. Then the name and the mask are struck through, the rest of the row dims, and
`RevokeAction` runs; the row stays like that for at least `RevokeExitDelay` (0.9 s) and folds away when both are done.
The key is removed from `Keys` (or from a `KeysSource` list), `KeyRevoked` fires with the row's former index and
`KeyRevokedCommand` runs. `RevokeAsync` does the same from code, without the hold.

### Details and idle keys

`ExpandedKey` (two-way) is the row whose details are open; only one row is open at a time. `LastRequestText` fills the
"Last request" fact; without it the block writes how long ago the key was used. A key whose `LastUsedAt` is older than
`StaleAfter` (30 days) shows a warning before its last use, and its revoke note says how many days it has been idle.
`ReferenceTime` fixes "now" for these texts (tests, screenshots).

### Narrow containers

The block follows its own width, not the window's. Below 700 the scopes column and the creation date leave the rows
(the last use moves to the end) and appear in the details instead. Below 540 the side paddings tighten from 20 to 16,
the facts stack in one column and the revoke button moves under its note. When the open panel is 560 wide or less the
form uses one column, with the hint on its own line above the buttons.

<Demo name="Compact" />

### Empty state

With no keys the list shows `ApiKeyLabels.EmptyTitle` and `EmptyDescription`. Set `EmptyActionText` to add a button: it
raises `EmptyActionInvoked` and runs `EmptyActionCommand`; when neither handles it, it opens the form. Added keys grow in
together, and the count rolls up.

<Demo name="Empty" />

### Events and state

`PanelState` (`Closed`, `Form`, `Generating`, `Reveal`) and `PanelStateChanged` with the reason the panel closed (Cancel,
Done, the close toggle, Escape or code) let the page follow the panel; `IsCreateOpen` (two-way), `OpenCreate` and
`CloseCreate` drive it. `ActiveCount` is the number of listed keys. `SecretCopied` fires when the secret was copied.

<Demo name="EventLog" />

### Texts

Every word the block shows or announces is in `Labels`, an `ApiKeyLabels` with English defaults. Texts that include a
value are functions, so word order can change:

```csharp
Keys.Labels = new ApiKeyLabels
{
    CreateKey = "New key",
    CountWords = count => count == 1 ? "key in use" : "keys in use",
    Used = ago => $"Last call {ago}",
};
```

## API reference

<!-- api: ApiKeys -->

<!-- api: ApiKeyItem -->

<!-- api: ApiKeyScope -->

<!-- api: ApiKeyRequest -->

<!-- api: ApiKeyCreated -->

<!-- api: ApiKeyLabels -->

## Accessibility

- The block is named by its title, a level 2 heading. The count is read as one phrase ("5 active keys").
- The toggle is named "Create key", and "Close" while the panel is open. The scopes are a group named "Scopes"; each
  checkbox is named by its scope and described by its line. Validation errors are alerts.
- The secret box is named "Secret key". The secret is never put anywhere else in the visual tree.
- The list is named "Active keys". Each row's button reads the name, environment, "Key ending in 7c3e", the scopes and
  the dates, with "Idle" before the last use of a stale key, and reports whether its details are expanded. The details
  are named "<name> details". The hold button is named "Hold to revoke: <name>".
- Opening a row, copying the secret, creating a key, revoking a key and a failed revoke are announced politely.
  A failed generate shows an assertive error line.
- The keyboard focus ring of a row is drawn inside the row, so its clipping never hides it.

### Keyboard

- **Tab** reaches the "Create key" toggle, then each row. An open row's hold button follows its row.
- **Enter** or **Space** on the toggle opens the form and focuses the name. **Tab** then moves through the environment
  switch (one stop; arrows choose), the scopes, Cancel and Generate. The form does not trap the focus.
- **Enter** in the name field submits. **Escape** anywhere in the panel closes it and returns the focus to the toggle;
  while a key is being generated, Escape cancels the request instead and stays in the form.
- After a key is created the focus moves to the Copy button.
- **Enter** or **Space** on a row opens or closes its details.
- **Space** or **Enter** held on the hold button revokes after 1.2 s; releasing early cancels.
- After a revoke the focus moves to the next row, else to the previous one, else to the "Create key" toggle.

## Motion

- **Pill to panel**: one smooth spring grows the surface leftward to the full width and down to the form; the plus turns
  45° into a close cross; the form fades in after 140 ms; the count fades out. Closing fades the body out in 100 ms, folds
  the surface back and fades the count in after 100 ms.
- **Form**: the hint under the form blur-swaps when the environment changes; the error lines open on the smooth spring.
- **Generate**: the title blur-swaps to "Key created"; the form fades out in 120 ms and the reveal rises 8 px while
  fading in (320 ms after 60 ms); the secret develops from an 8 px blur over 480 ms. The new row grows in from zero
  height on the smooth spring with a 240 ms fade after 80 ms and keeps its accent tint until the panel closes, then the
  tint fades over 1.2 s after 300 ms. The count rolls up.
- **Rows**: the details open on the smooth spring and fade in over 240 ms after 50 ms; closing fades them out in 160 ms.
  The chevron turns 180° on a 580 ms spring curve; the open tint changes in 160 ms. Only one row is open.
- **Revoke**: the hold fills linearly; once confirmed, a line strikes through the name (480 ms) and, 80 ms later, through
  the mask; the rest dims to 40% in 240 ms. After the pause the row folds away on the smooth spring and the rows below
  follow; the count rolls down.
- Reduced motion places sizes and positions at once and keeps only short fades; the hold still fills over 1.2 s. Off
  applies every change at once.

## Related

- `HoldToConfirm`, `CopyButton`, `Input`, `SegmentedControl`, `Checkbox`, `Badge` and `Avatar`, the controls the block
  is made of.
- Webhooks, Integrations and Env variables, the other Developer blocks.
