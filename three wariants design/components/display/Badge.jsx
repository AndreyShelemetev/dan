export function Badge({ tone = "neutral", solid = false, children, style }) {
  const tones = {
    neutral: { soft: ["var(--paper)", "var(--ink-2)", "1px solid var(--border-strong)"], solidC: ["var(--ink-2)", "#fff"] },
    accent: { soft: ["var(--accent-soft)", "var(--accent-deep)", "none"], solidC: ["var(--accent)", "var(--on-accent)"] },
    success: { soft: ["var(--success-soft)", "var(--success)", "none"], solidC: ["var(--success)", "#fff"] },
    info: { soft: ["var(--info-soft)", "var(--info)", "none"], solidC: ["var(--info)", "#fff"] },
    warning: { soft: ["var(--warning-soft)", "var(--warning)", "none"], solidC: ["var(--warning)", "#fff"] },
    danger: { soft: ["var(--danger-soft)", "var(--danger)", "none"], solidC: ["var(--danger)", "#fff"] },
  };
  const t = tones[tone] || tones.neutral;
  const [bg, color, border] = solid ? [...t.solidC, "none"] : t.soft;
  return (
    <span style={{
      display: "inline-block", background: bg, color, border,
      fontFamily: "var(--font-ui)", fontSize: 12, fontWeight: 600,
      borderRadius: "var(--radius-pill)", padding: "6px 14px", whiteSpace: "nowrap", ...style,
    }}>{children}</span>
  );
}
