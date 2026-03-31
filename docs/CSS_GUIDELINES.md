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
