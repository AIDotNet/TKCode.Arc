---
title: 新手問卷
summary: 首次使用時的簡短問卷，一次問一個問題：單選、多選、文字回答、進度、略過和結束時的感謝頁。
source-hash: ffa8778ef0ff
---

<Demo name="Preview" />

## 何時使用

`OnboardingSurvey` 在新使用者註冊後立即問幾個問題，一次一個，讓應用程式據此選擇範本、預設設定或起始頁面。計數和進度條顯示進度，「Skip survey」可隨時結束問卷。問題可以用卡片單選、用核取方塊多選（可設上限），或在文字框中寫幾句話。選填的問題提供「Skip question」。使用者返回時答案會保留，感謝頁會在 `Completed` 中回報所有答案。

- **首次設定**：依職位、團隊人數或目標調整工作區。
- **關鍵時刻後的簡短調查**，例如試用結束時。
- **收集偏好**：做成一張長表單會顯得太重。

## 何時不用

- **收集必須有效的帳號資訊**：使用會檢查欄位的 `MultiStepForm` 或 `SignupForm`。
- **帶分支的長問卷**：使用專門的表單；這張卡片只適合幾個問題。
- **介紹功能**：使用 `OnboardingIllustration`，它是展示而不是提問。

## 用法

```xml
<arc:OnboardingSurvey CanSkipSurvey="False">
    <arc:OnboardingSurveyQuestion Id="role" Title="What best describes your work?">
        <arc:OnboardingSurveyOption Label="Engineering" />
        <arc:OnboardingSurveyOption Label="Design" />
    </arc:OnboardingSurveyQuestion>
    <arc:OnboardingSurveyQuestion Id="note" Title="Anything we should know?" Kind="Text" IsOptional="True" />
</arc:OnboardingSurvey>
```

每個答案包含問題的 `Id`、所選選項的值（選項的 `Value`，或其文字）以及文字問題的內容。沒有問題時，問卷會問四個虛構的範例問題。

<Demo name="OwnQuestions" />

## API 參考

<!-- api: OnboardingSurvey -->

## 無障礙

- 焦點會移到每個新問題的回答控制項上，並讀出計數和問題。
- Enter 繼續，Alt+左方向鍵返回，數字鍵 1 到 9 選擇選項。在文字框中 Enter 換行，Ctrl+Enter（Cmd+Enter）繼續。
- 多選題達到上限時，其他選項會被停用，提示文字說明可以選幾個。

## 動效

- 問題依前進方向滑入，卡片高度以彈簧跟隨；完成問題時進度條隨之填滿，感謝頁上的勾號會自己畫出來。
- 減少動效時問題交叉淡化，勾號立即顯示。關閉動效時一切立即改變。

## 響應式

- 卡片最寬 540 px。小於 440 px 時內距收緊，按鈕直排並佔滿整列。
- 選項卡片和核取方塊文字會換行，因此在手機上也易於閱讀。

## 相關元件

- `RadioCards`、`Checkbox`、`Textarea` 和 `Progress`：問卷由它們構成。
- `MultiStepForm` 用於分步表單，`OnboardingIllustration` 用於圖示形式的首次導覽。
