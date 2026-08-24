export function Switch({ label, checked, onChange, disabled = false, style }) {
  return (
    <label style={{ display: "flex", alignItems: "center", gap: 12, cursor: disabled ? "default" : "pointer", opacity: disabled ? 0.45 : 1, fontFamily: "var(--font-ui)", ...style }}>
      <span
        onClick={() => !disabled && onChange && onChange(!checked)}
        style={{
          width: 40, height: 24, borderRadius: 999, position: "relative", flexShrink: 0,
          background: checked ? "var(--accent)" : "var(--border-strong)",
          transition: "background var(--duration) var(--ease)",
        }}>
        <span style={{
          position: "absolute", top: 3, left: checked ? 19 : 3, width: 18, height: 18, borderRadius: "50%",
          background: "#fff", boxShadow: "0 1px 2px rgba(43,47,43,0.2)", transition: "left var(--duration) var(--ease)",
        }} />
      </span>
      <span style={{ fontSize: 14, color: "var(--ink-1)" }}>{label}</span>
    </label>
  );
}
