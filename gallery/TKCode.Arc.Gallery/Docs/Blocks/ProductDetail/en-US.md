---
title: Product detail
summary: A product page: pictures per color with thumbnails, the price, color swatches, sizes with their stock, a quantity, add to cart and save, and the details and reviews behind a switch.
---

<Demo name="Preview" />

## When to use

`ProductDetail` is the page of one product. Its pictures change with the chosen color, with thumbnails to move between
views. Beside them are the maker, the name, the rating, the price (with the price before a reduction), the colors, the sizes
with their stock, a quantity, Add to cart and Save. Below, a switch shows the details or the reviews with a breakdown by
stars.

- **Product pages** with color and size variants.
- **Quick views** opened from a listing.
- **Product previews** in a back office.

## When not to use

- **Browsing many products**: use the Product listing block.
- **Products configured from many options**: build a step-by-step configurator with a form.

## Usage

The block derives from Avalonia's `TemplatedControl`. Set the product (`Brand`, `ProductName`, `Price`, `CompareAtPrice`,
`Rating`, `ReviewCount`, `Description`, `Details`) and fill `Colors` (each a `ProductColorOption` with its pictures),
`Sizes` (`ProductSizeOption`s with their stock) and `Reviews`. `ProductDetailSamples.Apply` fills a block with the preview's
invented tote.

```csharp
detail.Colors.Add(new ProductColorOption { Name = "Sand", Swatch = Color.Parse("#C9B79C"), Images = sandPictures });
detail.Sizes.Add(new ProductSizeOption("M", stock: 2));
```

### Choosing and adding

A size with no stock is struck through and cannot be chosen; three or fewer left shows "Only 2 left". The quantity stays
between 1 and the chosen size's stock (at most 10). Until a size is chosen the button reads "Choose a size"; pressing it
shows "Choose a size first." and moves the focus to the first size. Otherwise `AddedToCart` carries the color, the size and
the quantity, and the button reads "Added to cart" for 1.6 seconds. Save toggles `IsSaved`.

<Demo name="Narrow" />

## API reference

<!-- api: ProductDetail -->

<!-- api: ProductColorOption -->

<!-- api: ProductSizeOption -->

<!-- api: ProductReview -->

## Accessibility

- The main picture is focusable; **Left** and **Right** move between pictures and its name says which ("Picture 2 of 4").
- Thumbnails, swatches and sizes are buttons named with their picture, color or size ("M", "XS, sold out"); arrow keys move
  along each row and skip sold-out sizes.
- The rating is named "Rated 4.7 out of 5".
- A polite live region announces what was added and the size reminder.

## Motion

- Option borders and fills cross-fade in 160 ms when chosen.
- Pictures change in place; the add button's label swaps with the button's own transition.
- **Reduced motion** and **Off**: every change is immediate.

## Responsive behavior

- Below 820 px the pictures sit above the information.
- Options wrap onto more lines in a narrow block.

## Related

- The Product listing block for the catalog page.
- The Cart drawer block for what happens after Add to cart.
