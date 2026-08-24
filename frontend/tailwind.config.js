/**
 * Design system: direction A — «Тихий сад» (Quiet Garden).
 *
 * Every value below is transcribed from the design source of truth in
 * `three wariants design/tokens/*.css` and `three wariants design/guidelines/*.html`.
 * Do not add colours that are not in that palette; add a token there first.
 *
 * `fontSize`, `borderRadius` and `fontFamily` deliberately REPLACE Tailwind's
 * defaults (rather than extending them) so the scale in the codebase is the
 * design scale — no stray `text-5xl`/`rounded-lg` that the system never defined.
 *
 * @type {import('tailwindcss').Config}
 */
module.exports = {
  // The system defines a single (light) palette — no dark variants exist yet.
  // Left as "media" so nothing regresses if a dark palette is added later.
  darkMode: "media",
  content: ["./app/**/*.{ts,tsx}", "./components/**/*.{ts,tsx}"],
  theme: {
    // --font-display / --font-ui (tokens/typography.css). The CSS variables are
    // supplied by next/font/google in app/layout.tsx.
    fontFamily: {
      display: ["var(--font-spectral)", "Georgia", "Times New Roman", "serif"],
      sans: ["var(--font-manrope)", "Segoe UI", "Arial", "sans-serif"],
    },
    // tokens/typography.css. `4xl` (52px) is the hero display size used by
    // direction-a.dc.html; everything else is a literal --text-* token.
    fontSize: {
      xs: ["12px", { lineHeight: "1.5" }],
      sm: ["13px", { lineHeight: "1.5" }],
      base: ["15px", { lineHeight: "1.5" }],
      md: ["17px", { lineHeight: "1.65" }],
      lg: ["20px", { lineHeight: "1.3" }],
      xl: ["26px", { lineHeight: "1.25" }],
      "2xl": ["34px", { lineHeight: "1.15" }],
      "3xl": ["46px", { lineHeight: "1.15" }],
      "4xl": ["52px", { lineHeight: "1.15" }],
    },
    // tokens/effects.css
    borderRadius: {
      none: "0px",
      DEFAULT: "4px",
      card: "4px",
      input: "4px",
      modal: "6px",
      pill: "999px",
      full: "9999px",
    },
    extend: {
      // tokens/colors.css — semantic names only.
      colors: {
        paper: "#F5F3EE",
        surface: {
          DEFAULT: "#FBFAF6",
          raised: "#FFFFFF",
        },
        border: {
          DEFAULT: "#E3DFD4",
          strong: "#C9C4B6",
        },
        ink: {
          1: "#2B2F2B",
          2: "#5A5F58",
          3: "#8A8F87",
          inverse: "#FBFAF6",
        },
        accent: {
          DEFAULT: "#4A6151",
          hover: "#3C5142",
          deep: "#33473A",
          soft: "#E9EDE7",
        },
        // Deliberately muted semantics — no alarm red anywhere in this brand.
        success: { DEFAULT: "#3C6144", soft: "#E7EEE7" },
        info: { DEFAULT: "#41586E", soft: "#E6EBF0" },
        warning: { DEFAULT: "#8F6B2E", soft: "#F3ECDA" },
        danger: { DEFAULT: "#8C4A3F", soft: "#F2E3DF" },
      },
      // tokens/typography.css
      lineHeight: {
        tight: "1.15",
        normal: "1.5",
        relaxed: "1.65",
      },
      letterSpacing: {
        // --tracking-caps
        caps: "0.18em",
        // wider caps used by the hero eyebrow in direction-a.dc.html
        "caps-wide": "0.22em",
        // wordmark spacing (guidelines/wordmark.html)
        wordmark: "0.04em",
      },
      // tokens/spacing.css — 4/8/12/16/24/32/48/64 already map 1:1 onto
      // Tailwind's default scale (1/2/3/4/6/8/12/16); only 88px is missing.
      spacing: {
        22: "88px", // --space-9, the between-sections rhythm
        hit: "44px", // --hit-target, the WCAG / ТЗ minimum tap target
      },
      maxWidth: {
        content: "1080px", // --container
        hero: "760px", // hero column in direction-a.dc.html
        measure: "560px", // hero paragraph measure
      },
      // tokens/effects.css
      boxShadow: {
        card: "0 1px 2px rgba(43, 47, 43, 0.05)",
        raised: "0 6px 20px rgba(43, 47, 43, 0.08)",
        modal: "0 16px 48px rgba(43, 47, 43, 0.16)",
        focus: "0 0 0 3px rgba(74, 97, 81, 0.28)", // --focus-ring
      },
      transitionDuration: {
        ds: "160ms", // --duration
      },
      transitionTimingFunction: {
        ds: "cubic-bezier(0.25, 0.6, 0.3, 1)", // --ease
      },
    },
  },
  plugins: [],
};
