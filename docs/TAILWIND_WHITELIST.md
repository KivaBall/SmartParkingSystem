# Tailwind Whitelist

## Purpose

This file defines the intentionally limited Tailwind utility set for the project.

The goal is to avoid open-ended Tailwind usage and keep the interface within a strict visual system.

If a class is not listed here, it should be treated as disallowed until reviewed and added deliberately.

## Core Principle

- Keep the whitelist small.
- Prefer consistency over flexibility.
- Do not add extra shades, variants, or arbitrary utility values unless they are genuinely needed.

## Allowed Color Classes

### Blue / Brand

- `bg-brand-100/70`
- `bg-brand-100/80`
- `bg-brand-100/60`
- `bg-brand-200`
- `bg-brand-300`
- `bg-brand-400`
- `text-sky-500`

### Calm Text / Surface

- `bg-calm-50`
- `bg-calm-100`
- `text-calm-500`
- `text-calm-700`
- `text-calm-900`

### Mint

- `bg-mint-100`
- `bg-mint-300`
- `text-mint-700`

### Warm

- `bg-warm-100`
- `bg-warm-300`
- `text-warm-700`

### White

- `bg-white`
- `bg-white/80`
- `bg-white/85`

## Allowed Typography Classes

- `font-sans`
- `font-semibold`
- `font-extrabold`
- `text-sm`
- `text-base`
- `text-lg`
- `text-5xl`
- `text-6xl`
- `leading-none`
- `leading-6`
- `leading-7`

## Allowed Border Radius

- `rounded-md`

## Allowed Spacing

- `p-5`
- `p-6`
- `p-8`
- `p-10`
- `px-4`
- `py-2`
- `py-3`
- `py-4`
- `py-6`
- `mt-8`
- `mb-2`
- `mb-8`
- `gap-2`
- `gap-3`
- `gap-4`
- `gap-6`
- `space-y-4`

## Allowed Layout Classes

- `flex`
- `grid`
- `inline-flex`
- `flex-col`
- `flex-1`
- `items-center`
- `items-end`
- `justify-center`
- `justify-between`
- `justify-end`
- `w-full`
- `h-full`
- `min-h-screen`
- `min-h-12`
- `min-h-16`
- `min-h-[12.5rem]`
- `min-h-[calc(100vh-3rem)]`
- `h-10`
- `h-12`
- `h-24`
- `h-28`
- `w-10`
- `w-12`
- `w-24`
- `w-28`
- `max-w-md`
- `max-w-6xl`
- `mx-auto`
- `box-border`

## Allowed Responsive Layout Classes

- `sm:flex-row`
- `sm:grid-cols-[1fr_auto_auto]`
- `sm:items-end`
- `sm:h-28`
- `sm:h-14`
- `sm:w-28`
- `sm:w-14`
- `sm:p-8`
- `sm:p-10`
- `sm:px-6`
- `sm:text-6xl`
- `sm:text-lg`
- `lg:px-8`
- `lg:grid-cols-[0.95fr_1.05fr]`
- `lg:items-stretch`
- `lg:p-10`
- `md:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]`

## Allowed Visual Helpers

- `bg-gradient-to-b`
- `from-calm-50`
- `to-mint-100`
- `opacity-0`
- `opacity-100`

## Allowed Interaction Classes

- `transition-all`
- `transition-opacity`
- `duration-[250ms]`
- `duration-500`
- `ease-in`
- `ease-out`
- `hover:bg-brand-400`
- `hover:bg-brand-200`
- `hover:bg-mint-200`
- `hover:bg-warm-200`
- `hover:bg-calm-100`
- `disabled:cursor-default`
- `disabled:opacity-50`

## Allowed Animation Classes

- `animate-enter-left`
- `animate-enter-right`
- `animate-enter-bottom`
- `animate-exit-left`
- `animate-exit-right`
- `animate-exit-bottom`

## Allowed Form Classes

- `appearance-none`
- `outline-none`

## Allowed Utility Classes

- `antialiased`

## Rule For New Classes

If a new Tailwind class is needed:

1. First try to solve the problem using an already approved class.
2. If that is not possible, the AI should explicitly ask the user before introducing new styling utilities, new CSS-related files, or extending the visual system for new functionality.
3. Only after that approval should the new class or styling rule be added here intentionally.
4. Keep color additions especially strict.
