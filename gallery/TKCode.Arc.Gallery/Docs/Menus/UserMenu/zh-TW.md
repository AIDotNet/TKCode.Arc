---
title: 使用者選單
summary: 頭像背後的帳號、設定、主題與登出。在手機上以底部面板的形式開啟。
source-hash: b50c9a4ea9f3
---

## 何時使用

- 應用程式頂列中唯一的帳號入口，位於頭像之後。
- 兩到四個與帳號相關的入口，例如個人資料、設定和帳單，並附帶主題選擇與登出。
- 團隊應用程式中顯示在頭像上的線上狀態切換。

## 何時不用

- 與帳號無關的一般命令清單：用 `DropdownMenu`。
- 不屬於帳號選單、單獨使用的主題切換：用 `ThemeSwitcher`。
- 入口超過四個：把多出的入口移到一個頁面中，再從選單連結過去。
- 從清單列或卡片開啟的選單：用 `ContextMenu` 或 `DropdownMenu`。

## 用法

設定身分相關屬性，並在 `UserMenu.Items` 下宣告各個入口。頭像即為觸發器。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:UserMenu DisplayName="Ada Lovelace"
                  Email="ada@analytical.dev"
                  Plan="Pro"
                  IsNameVisible="True"
                  SignOutGestureText="⇧⌘Q">
        <arc:UserMenu.Items>
            <arc:MenuEntry Header="個人資料" />
            <arc:MenuEntry Header="設定" InputGestureText="⌘ ," />
            <arc:MenuEntry Header="帳單" />
        </arc:UserMenu.Items>
    </arc:UserMenu>
</UserControl>
```

### 主題與狀態

`ThemePreference` 可為 `Light`、`Dark` 或 `System`。選單會回報所選的值並保持開啟。主題由應用程式自行套用。`IsThemeSwitchVisible="False"` 可隱藏主題切換。

```xml
<arc:UserMenu DisplayName="Grace Hopper"
              Email="grace@compiler.dev"
              ThemePreference="{Binding ThemePreference, Mode=TwoWay}"
              Status="{Binding Status, Mode=TwoWay}"
              IsStatusVisible="True" />
```

```csharp
userMenu.ThemePreferenceChanged += (_, e) => ApplyTheme(e.ThemePreference);
userMenu.StatusChanged += (_, e) => status.Text = $"狀態：{e.Status}";
```

### 非同步登出

選取登出列時會執行 `SignOutAction`。工作執行期間，該列顯示載入指示器，選單保持開啟。工作完成後選單才會關閉。

```csharp
userMenu.SignOutAction = () => auth.SignOutAsync();
userMenu.SignOutRequested += (_, _) => status.Text = "正在登出…";
```

### 照片與資料驅動的入口

`AvatarSource` 用於顯示照片。未設定照片，或照片仍在載入時，顯示姓名首字母。當 `Items` 不夠用時，`ItemsSource` 可以根據資料產生入口。

```xml
<arc:UserMenu DisplayName="{Binding Name}"
              Email="{Binding Email}"
              AvatarSource="{Binding Avatar}"
              ItemsSource="{Binding AccountLinks}" />
```

## API 參考

<!-- api: UserMenu -->

<!-- api: MenuEntry -->

## 無障礙

- 頭像觸發器會以「帳號選單」加顯示名稱的方式被播報。顯示狀態時，狀態會附加在名稱之後。
- 各列具有選單項目角色。主題與狀態切換分別是有名稱的單選群組。
- 方向鍵可在主題或狀態切換中選擇，Enter 或空白鍵啟動某一列。
- Escape 或 Tab 會關閉選單，並將焦點交回頭像。
- 線上狀態點同時透過形狀和顏色變化表達，不只依賴顏色。

## 動態效果

- 在寬視窗中，面板從頭像處展開，沒有回彈過衝；各列依序淡入。
- 一個高亮條在各列之間滑動，主題與狀態的滑塊滑到所選選項上。
- 寬度低於 640px 時，選單以底部面板的形式開啟，從底邊升起。它會跟隨向下拖曳，拖得夠遠時關閉。
- 減少動態效果時，面板與切換項使用短暫的淡入淡出，高亮直接跳轉，並停用拖曳。關閉動態效果時選單立即顯示。

## 相關元件

- `DropdownMenu`：位於帶標籤觸發器之後的一般命令清單。
- `ThemeSwitcher`：不屬於帳號選單的淺色與深色切換。
- `Avatar`：觸發器所顯示的圖片，也可在身分資訊標頭中重複使用。
- `MenuSurface`：面板與底部面板所依據的共用選單彈出層。
