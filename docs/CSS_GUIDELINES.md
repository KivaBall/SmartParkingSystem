# CSS Guidelines

## Visual Direction

The Smart Parking System interface should look modern, calm, minimalistic, and user-friendly. The goal is not a heavy dashboard with visual noise, but a clean product-like interface that feels understandable on both desktop and mobile devices.

## Styling Stack

- Tailwind CSS should be used as the primary styling approach.
- Utility-first styling is preferred over scattered custom CSS when possible.
- Additional custom CSS should only be used when Tailwind utilities are not enough.

## Layout And Responsiveness

- Every page must be responsive and adapt correctly to different screen sizes.
- The interface must work well on phones, tablets, laptops, and desktop screens.
- Widths and heights should be controlled intentionally rather than left inconsistent.
- Layouts should rely on responsive containers, grids, flex layouts, and spacing scales.
- Elements should resize and rearrange cleanly across breakpoints.

## Component Shape

- All UI elements must use `rounded-md` only.
- Do not use stronger or more decorative border radius variants unless there is a very specific reason.

## Shadows And Borders

- No shadows should be used on cards, panels, buttons, inputs, or layout containers.
- No visible borders should be used in the default visual system.
- Separation between sections should be achieved using spacing, background contrast, typography hierarchy, and grouping instead of borders or shadows.

## Color System

- Avoid aggressive or overly saturated colors.
- The base palette should stay within soft blue, light green, white, and other calm neutral tones.
- Safety, warnings, or attention states may use soft orange or warm yellow accents.
- Even highlighted states should remain restrained and non-aggressive.
- The interface should feel light, calm, and readable rather than alarm-heavy.
- Primary visual accents should still feel alive rather than faded.
- Bright sky-blue should be treated as the main positive brand accent for headings, highlighted controls, and other important UI emphasis.
- Supporting containers and secondary actions should use clearer blue, mint, and warm tones instead of dull grayish surfaces.

## Typography

- The primary application font should be `Manrope`.
- This font should be used consistently across the entire interface.
- The reason for choosing `Manrope` is that it feels modern, soft, readable, and product-oriented without looking cold or overly corporate.

## Icons

- A dedicated icon library should be used across the interface for consistency.
- The preferred icon set is `Lucide Icons`.
- Icons should be loaded through CDN, the same general way Tailwind is included.
- Icons should support the minimal visual style and avoid heavy or decorative shapes.
- Icon usage should remain functional and restrained rather than overly illustrative.

## General UI Principle

- The design should feel structured, soft, and modern.
- It should not look visually overloaded.
- The system should prioritize clarity of state, operator comfort, and clean business-oriented navigation.

## Motion

- Animations should be used carefully and only where they improve clarity.
- The default animation duration across the interface should be `0.5s`.
- `0.5s` should be treated as the total perceived duration of one animation action.
- If a transition is split into multiple sequential phases, their combined duration should still equal `0.5s`.
- Example: if one UI state fades out and the next fades in, that should typically be `0.25s` for fade-out and `0.25s` for fade-in.
- Page loading and page-to-page transitions should use `1s` total duration.
- For page-to-page navigation, the total `1s` should usually be split as `0.5s` for the current page exit and `0.5s` for the next page entrance.
- For first application load, a single full-page entrance animation may use the full `1s`.
- For page transitions, entrance and exit motion may be longer than local UI interactions as long as they stay controlled and readable.
- Entrance animations should feel soft and controlled rather than flashy.
- State changes such as switching between connection modes should transition smoothly instead of changing abruptly.
