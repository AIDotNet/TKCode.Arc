---
title: Onboarding survey
summary: A short first-run survey asked one question at a time, with single and multiple choice, a text answer, progress, skipping and a thank-you.
---

<Demo name="Preview" />

## When to use

`OnboardingSurvey` asks a new user a few questions right after sign-up, one at a time, so the app can pick templates,
defaults or a starting page. A counter and a progress bar show how far along they are, and "Skip survey" ends it at
any point. A question is answered with cards for one choice, checkboxes for several (with an optional limit), or a
box for a few sentences. Optional questions offer "Skip question". The answers are kept when the person goes back,
and the thank-you reports every answer in `Completed`.

- **First-run setup** that tailors the workspace to a role, a team size or goals.
- **Short research questions** after a key moment, such as finishing a trial.
- **Preference capture** that would feel heavy as one long form.

## When not to use

- **Collecting account details that must be valid**: use `MultiStepForm` or `SignupForm`, which check fields.
- **A long questionnaire with branching**: use a dedicated form; this card keeps to a handful of questions.
- **Introducing features**: use `OnboardingIllustration`, which shows rather than asks.

## Usage

```xml
<arc:OnboardingSurvey CanSkipSurvey="False">
    <arc:OnboardingSurveyQuestion Id="role" Title="What best describes your work?">
        <arc:OnboardingSurveyOption Label="Engineering" />
        <arc:OnboardingSurveyOption Label="Design" />
    </arc:OnboardingSurveyQuestion>
    <arc:OnboardingSurveyQuestion Id="note" Title="Anything we should know?" Kind="Text" IsOptional="True" />
</arc:OnboardingSurvey>
```

Each answer has the question's `Id`, the chosen option values (an option's `Value`, or its label) and the text of a
text question. Without questions the survey asks four invented examples.

<Demo name="OwnQuestions" />

## API reference

<!-- api: OnboardingSurvey -->

## Accessibility

- Focus moves to the answer controls of each new question, and the counter and question are read out.
- Enter continues, Alt+Left goes back, and the number keys 1 to 9 pick an option. In the text box Enter adds a line
  and Ctrl+Enter (Cmd+Enter) continues.
- At the limit of a multiple-choice question the other options are disabled, and the hint says how many to choose.

## Motion

- Questions slide in the direction of travel while the card's height follows on a spring; the progress bar fills as
  questions are finished, and the check on the thank-you draws itself.
- Reduced motion cross-fades the questions and shows the check at once. Off changes everything at once.

## Responsive behavior

- The card is at most 540 px wide. Below 440 px its padding tightens and the buttons stack to fill the row.
- Option cards and checkbox texts wrap, so long answers stay readable on a phone.

## Related

- `RadioCards`, `Checkbox`, `Textarea` and `Progress`, which the survey is built from.
- `MultiStepForm` for forms in steps and `OnboardingIllustration` for a visual first-run tour.
