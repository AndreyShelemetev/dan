export function Checkbox({ label, checked, onChange, disabled = false, style }) {
  return (
    <label style={{ display: "flex", alignItems: "flex-start", gap: 10, cursor: disabled ? "default" : "pointer", opacity: disabled ? 0.45 : 1, fontFamily: "var(--font-ui)", ...style }}>
      <span style={{
        width: 18, height: 18, flexShrink: 0, marginTop: 1, borderRadius: 4, display: "grid", placeItems: "center",
        border: `1.5px solid ${checked ? "var(--accent)" : "var(--border-strong)"}`,
        background: checked ? "var(--accent)" : "var(--surface-raised)",
        transition: "background var(--duration) var(--ease)",
      }}>
        {checked && <svg width="11" height="11" viewBox="0 0 12 12" fill="none"><path d="M2 6.5L4.8 9L10 3.5" stroke="var(--on-accent)" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"/></svg>}
      </span>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange && onChange(e.target.checked)} style={{ position: "absolute", opacity: 0, width: 0 }} />
      <span style={{ fontSize: 14, color: "var(--ink-1)", lineHeight: 1.45 }}>{label}</span>
    </label>
  );
}
