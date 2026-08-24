export function Toast({ tone = "info", children, visible = true, style }) {
  const bar = { info: "var(--info)", success: "var(--success)", warning: "var(--warning)", danger: "var(--danger)" }[tone];
  if (!visible) return null;
  return (
    <div style={{
      display: "inline-flex", alignItems: "center", gap: 12,
      background: "var(--ink-1)", color: "var(--ink-inverse)",
      borderRadius: "var(--radius-card)", padding: "12px 18px 12px 14px",
      fontFamily: "var(--font-ui)", fontSize: 14, boxShadow: "var(--shadow-raised)", ...style,
    }}>
      <span style={{ width: 4, alignSelf: "stretch", borderRadius: 2, background: bar }} />
      <span>{children}</span>
    </div>
  );
}
