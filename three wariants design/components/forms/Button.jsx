export function Button({ variant = "primary", size = "md", disabled = false, children, onClick, style }) {
  const sizes = { sm: "9px 18px", md: "13px 26px", lg: "16px 34px" };
  const fontSizes = { sm: 13, md: 14, lg: 15 };
  const variants = {
    primary: { background: "var(--accent)", color: "var(--on-accent)", border: "1px solid transparent" },
    secondary: { background: "transparent", color: "var(--ink-1)", border: "1px solid var(--border-strong)" },
    ghost: { background: "transparent", color: "var(--accent)", border: "1px solid transparent" },
    danger: { background: "var(--danger)", color: "#fff", border: "1px solid transparent" },
  };
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      style={{
        fontFamily: "var(--font-ui)", fontWeight: 600, fontSize: fontSizes[size],
        padding: sizes[size], borderRadius: "var(--radius-pill)", cursor: disabled ? "default" : "pointer",
        opacity: disabled ? 0.45 : 1, transition: "background var(--duration) var(--ease), color var(--duration) var(--ease)",
        ...variants[variant], ...style,
      }}
      onMouseEnter={(e) => { if (!disabled && variant === "primary") e.currentTarget.style.background = "var(--accent-hover)"; }}
      onMouseLeave={(e) => { if (variant === "primary") e.currentTarget.style.background = "var(--accent)"; }}
    >{children}</button>
  );
}
