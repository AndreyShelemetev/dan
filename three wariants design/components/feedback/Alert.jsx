export function Alert({ tone = "info", title, children, action, style }) {
  const tones = {
    info: ["var(--info-soft)", "var(--info)"],
    success: ["var(--success-soft)", "var(--success)"],
    warning: ["var(--warning-soft)", "var(--warning)"],
    danger: ["var(--danger-soft)", "var(--danger)"],
  };
  const [bg, fg] = tones[tone] || tones.info;
  return (
    <div style={{
      background: bg, borderRadius: "var(--radius-card)", padding: "14px 18px",
      fontFamily: "var(--font-ui)", display: "flex", flexDirection: "column", gap: 4, ...style,
    }}>
      {title && <div style={{ fontSize: 14, fontWeight: 700, color: fg }}>{title}</div>}
      <div style={{ fontSize: 14, color: "var(--ink-1)", lineHeight: 1.5 }}>{children}</div>
      {action && <div style={{ marginTop: 6 }}>{action}</div>}
    </div>
  );
}
