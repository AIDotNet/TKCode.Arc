---
title: Mobile navigation stack
summary: A stack of pages: a pushed page slides in over the one below, and a swipe from the left edge or a back button slides it away.
---

<Demo name="Basic" />

## When to use

`NavigationStack` is the way a mobile app moves deeper: from a list to a detail, from settings to a sub-screen, and back again.

- **Drill-down**: each tap on a row pushes a page that explains it.
- **A flow of screens** that has one way back, such as a few steps of a form.
- **Back by swipe**: the person expects to pull the page back from the left edge, and it follows the finger.

The live demo runs inside a phone frame, a desktop simulation; the swipe and the feel of the slide on a real device have not been verified here.

## When not to use

- **Switching between top-level areas**: use `Tabbar`; each area can hold its own stack.
- **A short task over the current screen**: use `Sheet`, which does not leave the page.
- **Two panes next to each other**: a stack shows one page at a time.

## Usage

Put the first page in the stack, then `Push` pages from code. A page is any control, usually a `Page` with a `Navbar`. Bind the navbar's `BackCommand` to the stack's `BackCommand`: it runs `Pop`, and is disabled on the first page.

```xml
<arcm:NavigationStack xmlns="https://github.com/avaloniaui"
                      xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                      Name="Stack">
    <arcm:Page>
        <arcm:Page.Header>
            <arcm:Navbar Title="Inbox" />
        </arcm:Page.Header>
        <!-- the first page -->
    </arcm:Page>
</arcm:NavigationStack>
```

```csharp
stack.Push(new Page
{
    Header = new Navbar { Title = "Ada", ShowBack = true, BackText = "Inbox", BackCommand = stack.BackCommand },
    Content = detail,
});
```

A push slides the new page in from the right while the page below shifts left by a third of the width and dims. `Pop` does the reverse, and `PopToRoot` returns to the first page, sliding only the top one away. A push or pop that arrives while another is sliding finishes the first at once.

Only the top page is in the visual tree while the stack rests. Pages below it keep their state, such as the scroll position, and come back as they were.

### Tabs with a stack each

A tabbar usually sits under a stack of its own for each tab. Keep one `NavigationStack` per tab and swap the page's `Content` when the selection changes: each tab keeps the pages it has pushed, and the tabbar stays visible under every page.

<Demo name="TabsWithStacks" />

### Swipe back

A horizontal drag that starts within 24 px of the left edge pulls the top page away, one to one with the finger, with the page below shifting and clearing as it goes. On release the stack looks ahead 0.2 s at the finger's speed: past the middle the page leaves, and a rightward flick faster than 500 px/s pops it from anywhere. Otherwise the page springs back. Set `IsSwipeBackEnabled` to `False` for a page that must not be left by a swipe.

The `Navigated` event reports each push and pop as soon as the stack changes, while the slide may still be running.

### Tokens

`NavigationStackToken` holds `DimColor`: the dim over the page below when the top page has not moved yet, different in light and dark. The parallax (0.3 of the width) and the edge width (24) are constants of the control, `ParallaxFraction` and `EdgeWidth`.

## API reference

<!-- api: MobileNavigationStack -->

## Accessibility

Keep the back button in the navbar visible with a name that says where it goes, so the swipe is never the only way back. Only the top page is in the tree, so screen readers and the keyboard reach nothing hidden under it.

## Motion

The slide is a code-driven motion value: the swipe writes it on every move, and on release the finger's speed goes into a smooth spring, which a transition declared in the theme cannot do. The dim and the shift of the page below follow the top page's position. With reduced motion the pages change at once.

## Related

- `MobileNavbar`: its `BackCommand` binds to the stack's `BackCommand`.
- `MobilePage`: the usual page.
- `MobileSheet`: for a short task that stays on the current page.
