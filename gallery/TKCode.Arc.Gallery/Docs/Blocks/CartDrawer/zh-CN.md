---
title: Cart drawer
summary: 商品网格中的商品会飞入购物车按钮，计数滚动更新；侧边抽屉提供数量步进器、免运费进度条、滚动的小计以及结账功能。
source-hash: 46bf8925baa1
---

<Demo name="Preview" />

## 何时使用

`CartDrawer` 是一个只占用一个区块的小型商店：一个商品网格、一个带计数的购物车按钮，以及从区块一侧滑入的购物车。添加商品时，该商品图片的副本会沿弧线飞入购物车按钮；落地时计数加一并滚动更新，按钮也会弹跳一下。抽屉让人们在付款前核对所选商品：数量、通往免运费的进度条、优惠码、合计，以及一个会变成确认提示的结账按钮。

- **商品列表**：人们可以一次添加多件商品，并在结账前一起查看。
- **设有免运费门槛的商店**：进度条及其上方的句子会说明还差多少；当小计达到门槛时，运费行会显示 "Free"。
- **结账前的优惠码**：有效的优惠码会变成绿色标签块，折扣行会滑入合计中。
- **显示商品去了哪里**：飞行动画把卡片上的按下操作与角落的计数连接起来，这是 toast 做不到的。

## 何时不用

- **完整的结账流程**，包含地址、配送和支付步骤：在购物车之后使用多步表单（`MultiStepForm`）。
- **只需要订单摘要**及其合计：使用结账摘要区块（Checkout summary 区块）。
- **不是购物车的侧边面板**：使用 `Drawer`，它覆盖整个窗口，可以放置任何内容。
- **数百件商品**：网格不做虚拟化。每个区块展示一页商品，或将商品放入你自己的列表并从中调用 `Add`。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。商品是 `CartDrawerProduct`，即 `AvaloniaObject`，包含名称、可选的详细信息行、价格、图片以及 `MaxQuantity`（默认为 9）。可以在 XAML 中填充 `Products`，也可以绑定 `ProductsSource`；设置了 `InitialQuantity` 的商品会直接进入购物车。金额为 `decimal`，单位由 `Currency`（ISO 4217）指定，并按 `Labels.Culture` 与 `AmountDecimals` 位小数格式化（默认为 0，即整数单位）。

```xml
<arc:CartDrawer Title="Fresh from the workshop"
                Height="760"
                FreeShippingThreshold="240"
                ShippingFee="9">
    <arc:CartDrawerProduct Name="Fluted water jug" Detail="Recycled glass, 0.9 L" Price="58" InitialQuantity="1" />
    <arc:CartDrawerProduct Name="Speckled mugs" Detail="Pair, reactive glaze" Price="42" />
</arc:CartDrawer>
```

`CartDrawerSamples` 保存预览中虚构的商店：`CreateProducts()`（六件带绘制图片的商品）、`CreateLabels()`（默认文字加上优惠码提示）、优惠码检查以及订单台。

### 结构

- **页头**：左侧是 `Title`；右侧是购物车按钮，包含购物袋、"Cart" 以及数量徽标。徽标在商品飞行落地时才计入，而不是在按下按钮时。
- **网格**：三列商品卡片（在 640 px 及以下为两列）。每张卡片都有 4:5 的图片、名称和价格、详细信息行，以及一个通栏的 "Add to cart" 按钮；按下后 1.4 秒内它会显示为 "Added"。
- **抽屉**：在区块内以遮罩覆盖打开，宽 400 px，距区块边缘 8 px。它包含一个带滚动数量的页头和关闭按钮；一张免运费卡片；各行；以及一个页脚，其中有优惠码、合计和结账按钮。
- **行**：缩略图、名称、详细信息、数量步进器（数量为 1 时，减号变为垃圾桶）、行价格和 ×。
- **收据**：结账后，显示一个带对勾的圆盘、感谢语、一句说明，以及"件数"、"已付"和"送达"三行；页脚有 "Continue shopping"。

### 购物车

`Lines` 按添加顺序列出各行，每次变化后 `ItemCount`、`Subtotal`、`Discount`、`Shipping` 和 `Total` 都会随之更新。当**折扣前**的小计达到 `FreeShippingThreshold` 时运费免收，因此优惠码永远不会取消免运费。折扣为小计乘以优惠码的百分比，并按 `AmountDecimals` 位小数四舍五入（半数远离零：$64 打 10% 折扣减去 $6，$186 减去 $19）。将门槛设为 0 即可隐藏免运费卡片，并让所有订单免运费。

`Add`、`SetQuantity`、`Remove` 和 `Clear` 从代码中修改购物车；`Open` 和 `Close`（或双向绑定的 `IsOpen`）控制抽屉的开关。`ItemAdded`、`QuantityChanged` 和 `ItemRemoved` 会报告每次变化，包括商品以及旧数量和新数量。一行的数量不能超过其商品的 `MaxQuantity`：步进器的加号和卡片按钮会停在上限处（即被禁用），此时 `Add` 返回 false，不会有飞行动画，也不会有播报。

### 移除行

× 和步进器的垃圾桶会立即移除一行。使用指针时，还可以将一行左滑：移动时，其后会露出一个红色层，上面写着 "Remove"；超过 110 px 后，它会加深一级。松开时若超过该点（或快速轻扫），该行就会滑出。较短的滑动会弹回。无论哪种方式，该行都会淡出并收拢，使下方的行上移。只有在大致水平的拖动时才会开始滑动，因此垂直拖动仍会滚动列表。将 `IsSwipeToRemoveEnabled` 设为 false，即只保留按钮。

### 优惠码

输入优惠码，然后按 Apply 或 Enter。`ValidatePromoAsync` 负责检查：返回一个 `CartDrawerPromo` 即应用该优惠码，返回 null 则拒绝。被拒绝的优惠码会使输入框抖动、边框变红，并用 "SPRING doesn't match an offer" 替换提示文字；下一次编辑会清除错误。有效的优惠码会变成一个标签块，显示优惠码、百分比和 Remove 按钮，同时合计中会展开一行折扣。`ApplyPromoAsync` 和 `RemovePromo` 可从代码执行相同的操作；`PromoApplied`、`PromoRejected` 和 `PromoRemoved` 负责报告。`IsPromoVisible="False"` 可隐藏输入框。

```csharp
store.ValidatePromoAsync = async (code, cancellation) =>
{
    var offer = await offers.FindAsync(code, cancellation);
    return offer is null ? null : new CartDrawerPromo(offer.Code, offer.PercentOff);
};
```

### 结账

结账会以 `CartDrawerOrder`（包含各行、优惠码和每项合计）调用 `PlaceOrderAsync`，并显示它返回的收据。运行期间 `State` 为 `Placing`：按钮显示加载指示和 "Placing order" 并保持焦点，各行和优惠码被锁定，抽屉无法关闭。之后按钮变为确认圆盘，对勾自行绘制，收据随之出现。如果任务抛出异常，抽屉会回到购物车，且购物车内容保持不变。关闭收据（无论是点击 "Continue shopping" 还是其他方式）都会开始一个新的空购物车。`OrderPlaced` 携带订单和收据。

```csharp
store.PlaceOrderAsync = async (order, cancellation) =>
{
    var confirmation = await checkout.PayAsync(order.Lines, order.Total, cancellation);
    return new CartDrawerReceipt($"Thanks, {confirmation.FirstName}", confirmation.Summary,
        order.ItemCount, order.Total, confirmation.DeliveryWindow);
};
```

### 自定义店铺

所有文字都位于 `CartDrawerLabels` 中；包含数字的句子是函数，接收格式化后的金额或数量。这家茶叶店使用英镑（含便士），拥有自己的优惠码检查和订单台，并将该区块的事件写入日志。

<Demo name="OwnStore" />

### 窄容器

该区块读取的是自身的宽度，而不是窗口的宽度。在 640 px 及以下，网格为两列，内边距更紧凑。在 380 px 及以下，购物车按钮只保留购物袋和计数，每张卡片把价格放在名称下方，抽屉以更小的缩略图铺满区块，距边缘 4 px。

<Demo name="Narrow" />

### 减少动效

开启减少动效后，不会有任何飞行动画，徽标计数会立即完成；抽屉原地淡入，而不是滑动；行不能滑动（× 仍可移除它们）；数字变化时不再滚动。文字、焦点和播报保持不变。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: CartDrawer -->

<!-- api: CartDrawerProduct -->

<!-- api: CartDrawerLine -->

<!-- api: CartDrawerPromo -->

<!-- api: CartDrawerOrder -->

<!-- api: CartDrawerReceipt -->

<!-- api: CartDrawerState -->

<!-- api: CartDrawerItemEventArgs -->

<!-- api: CartDrawerPromoEventArgs -->

<!-- api: CartDrawerOrderPlacedEventArgs -->

## 无障碍

- 购物车按钮以数量命名（"Cart, 2 items"）；飞行落地时名称会更新。**Enter** 或 **Space** 可打开抽屉。
- 抽屉打开时，焦点会移到其关闭按钮。**Tab** 和 **Shift+Tab** 在抽屉内循环，永远不会到达其后方的商店，商店也不接收指针输入。
- **Escape**、关闭按钮以及按下遮罩都会关闭抽屉，焦点回到购物车按钮。下单期间，这些操作不起作用。
- 每张卡片的按钮命名为 "Add Fluted water jug to cart"。步进器是一个名为 "Fluted water jug quantity" 的分组；其按钮命名为 "Decrease …"，数量为 1 时为 "Remove …"，另有 "Increase …"；× 为 "Remove …"。滑动操作与这些按钮等效。
- 一行被移除后，焦点移到下一行的相同按钮上（若没有下一行，则移到上一行；购物车为空时移到 "Browse products"）。优惠码应用后，焦点移到标签块的 Remove；移除优惠码后，焦点回到输入框。在收据上，焦点移到 "Continue shopping"。
- 滚动数字的无障碍名称即其数值。配送进度条命名为 "Progress to free shipping"，其帮助文字为已达到的金额。
- 礼貌级实时区域会播报已添加的商品、已移除的行、已应用或已移除的优惠码，以及下单成功的消息（"Order placed. 2 items, $134."）。数量变化和被拒绝的优惠码不会播报：输入框会被标记为无效，错误文字取代提示文字。
- 下单期间，结账按钮保持焦点，并以 "Placing order" 作为其名称。购物车为空时，它被禁用并变暗。

### 键盘

| 按键 | 位置 | 操作 |
|---|---|---|
| Enter, Space | 购物车按钮 | 打开抽屉 |
| Tab, Shift+Tab | 抽屉 | 在抽屉的控件之间移动（在抽屉内循环） |
| Escape | 抽屉 | 关闭抽屉（下单期间除外） |
| Enter | 优惠码输入框 | 应用优惠码 |
| Enter, Space | 任意按钮 | 按下该按钮 |

## 动效

- **飞向购物车**：一个进度值在 `cubic-bezier(.3, 0, .2, 1)` 上运行 780 ms。商品图片的副本沿二次贝塞尔曲线从图片中心飞向购物袋中心，控制点位于 55% 的横向位置，并高于较高端点 30 px；到 80% 时缩小为 26 × 26，途中在一半时倾斜至 −10°，最后 14% 淡出，并按 sin(πp) 抬升其阴影。卡片上的图片保持原位。多个飞行动画可以同时进行。
- **落地**：徽标的数字在平滑弹簧上向上滚动（数量减少时向下），随距离淡出；按钮在约 430 ms 内完成 1 → 1.09 → 0.98 → 1 的弹跳，同时 1 px 的强调色环扩大到 1.45，并从 0.45 淡出。
- **添加按钮**：文字切换为 "Added"（先在 100 ms 内退出，再在 240 ms 内上升 2 px 进入），其宽度跟随平滑弹簧变化，1.4 秒后恢复。
- **抽屉**：从其宽度的 104% 处在平滑弹簧上滑入，在收尾弹簧上滑出；遮罩在 280 ms 内淡入。
- **数字**：计数、行价格、合计和结账金额在平滑弹簧上逐位滚动。免运费进度条在平滑弹簧上增长，并在 480 ms 内变为绿色；卡车在 240 ms 内变为绿色。免运费句子和运费数值的切换方式为：旧文字上移 5.6 px 并淡出（160 ms），同时新文字上升就位（240 ms）。
- **行**：被移除的行在 160 ms 内淡出，其高度在 77 ms 后于平滑弹簧上收拢。被滑动的行以释放时的速度同时滑出；较短的滑动则在收尾弹簧上弹回。
- **优惠码**：被拒绝的优惠码会使输入框抖动（0、−6、5、−3、2、0 px，共 380 ms）。有效的优惠码会使输入框交叉淡入为标签块，标签块从 0.96 放大；页脚的高度和折扣行随平滑弹簧变化。
- **结账**：文字切换为加载指示和 "Placing order"。成功后，按钮的边界和圆角在弹簧上过渡为收据上方 64 px 的圆盘（视觉时长 0.6 秒，无回弹），对勾在 400 ms 后用 250 ms 自行绘制，购物车在 150 ms 内淡出，收据在 130 ms 后淡入。
- **减少动效**：不飞行，徽标计数立即完成；没有弹跳和涟漪；抽屉在 180 ms 内淡入而不是滑动；行不能滑动；数字和进度条变化时不使用弹簧；切换保持 120 ms 的交叉淡入。**关闭**会立即应用所有变化。

## 响应式

- 断点读取区块的宽度：640 px 及以下（`:compact`）和 380 px 及以下（`:narrow`）。
- 为区块指定高度（或将其放在限制高度的容器中）。商店在区块内滚动，抽屉的高度为区块高度减去其内边距，行在固定的页头和页脚之间滚动。
- 抽屉宽 400 px；若区块窄于此，抽屉则填满区块宽度减去内边距后的宽度。
- 悬停只改变颜色。每个操作都可以通过点击完成；滑动可用于触控、触控笔或鼠标。

## 相关组件

- `Drawer`：窗口级的侧边面板。
- 结账摘要区块：带优惠码和付款按钮的订单摘要。
- `AnimatedCounter`：单个滚动数字。
- `Button`、`Input` 和 `Progress`：抽屉各部分所遵循的控件。
