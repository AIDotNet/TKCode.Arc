---
title: Product listing
summary: A product listing page: category chips, a price range, an in-stock switch and a sort over a responsive grid of product cards with ratings, reduced prices, colors and quick add to cart.
---

<Demo name="Preview" />

## When to use

`ProductListing` is the catalog page of a store. Filters narrow the products by category, price and stock; a sort orders
them; and the grid shows a card per product with its picture, a tag (new, bestseller or the reduction), the name, the
rating, the price with the price before a reduction, and the colors it comes in. Each card has a quick add button, and the
cart button in the header counts what was added.

- **Category and search result pages** of a store.
- **Small catalogs** of up to a few hundred products per page.
- **Merchandising pages** where reductions and ratings matter.

## When not to use

- **A single product**: use the Product detail block.
- **Reviewing the cart**: use the Cart drawer block.
- **Thousands of products at once**: the grid is not virtualized; page the results.

## Usage

The block derives from Avalonia's `TemplatedControl`. Fill `Products`, or bind `ProductsSource`, with `ListingProduct`s.
`ProductListingSamples.CreateProducts` returns twelve invented products whose pictures are drawn by
`ProductListingArtwork`.

```xml
<arc:ProductListing Title="Everyday goods" ProductsSource="{Binding Products}" Sort="TopRated" />
```

### Filters and sort

The chips list every category of the products; none selected shows all. The price range starts at the cheapest and ends at
the most expensive product; `MinPrice` and `MaxPrice` are null while a handle rests at its end. `InStockOnly` hides sold-out
products. `Sort` orders by the given order, price either way, rating (more reviews break ties) or newest. `VisibleProducts`
holds the result and `ClearFilters` resets everything; an empty result offers the same button.

### Adding to the cart

Quick add raises `AddedToCart`, adds one to `CartCount` and reads "Added" for 1.4 seconds. Sold-out products keep their
card with a disabled Sold out button. Pressing a card raises `ProductInvoked`, and the cart button raises `CartInvoked`.

### Narrow blocks

The grid fits as many columns of at least 160 px as the width allows, up to `MaxColumns` (4). Below 760 px the filters fold
behind a Filters button.

<Demo name="Narrow" />

## API reference

<!-- api: ProductListing -->

<!-- api: ListingProduct -->

## Accessibility

- Each card's main area is a button named with the product, its price and its rating; its quick add button is named "Add
  Market tote to cart".
- Arrow keys move between cards in reading order (**Up** and **Down** by a row), **Home** and **End** go to the first and
  last card.
- The cart button is named with the count ("Cart, 2 items").
- A polite live region announces each added product and the new result count after a filter change. **Escape** closes the
  folded filters.

## Motion

- Cards that join the grid after a filter change fade in over 240 ms; cards that stay keep their place.
- A picture grows by 1.5% under the pointer on the snappy spring.
- **Reduced motion** and **Off**: cards appear at once and pictures do not grow.

## Responsive behavior

- Columns follow the block's width, not the window's; the filters fold below 760 px.
- Names end with an ellipsis; prices and ratings keep one line.

## Related

- The Product detail block for one product.
- The Cart drawer block for a cart with checkout.
- `ChipGroup`, `Slider`, `Switch` and `Select`, the filter controls.
