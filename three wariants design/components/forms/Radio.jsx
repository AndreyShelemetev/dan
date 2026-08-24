export function Radio({ label, checked, onChange, name, disabled = false, style }) {
  return (
    <label style={{ display: "flex", alignItems: "flex-start", gap: 10, cursor: disabled ? "default" : "pointer", opacity: disabled ? 0.45 : 1, fontFamily: "var(--font-ui)", ...style }}>
      <span style={{
        width: 18, height: 18, flexShrink: 0, marginTop: 1, borderRadius: "50%", display: "grid", placeItems: "center",
        border: `1.5px solid ${checked ? "var(--accent)" : "var(--border-strong)"}`, background: "var(--surface-raised)",
      }}>
        {checked && <span style={{ width: 9, height: 9, borderRadius: "50%", background: "var(--accent)" }} />}
      </span>
      <input type="radio" name={name} checked={checked} disabled={disabled} onChange={() => onChange && onChange()} style={{ position: "absolute", opacity: 0, width: 0 }} />
      <span style={{ fontSize: 14, color: "var(--ink-1)", lineHeight: 1.45 }}>{label}</span>
    </label>
  );
}
