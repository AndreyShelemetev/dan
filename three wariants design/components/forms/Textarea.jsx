export function Textarea({ label, hint, error, rows = 4, style, ...rest }) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 6, fontFamily: "var(--font-ui)", ...style }}>
      {label && <span style={{ fontSize: 13, fontWeight: 600, color: "var(--ink-1)" }}>{label}</span>}
      <textarea
        rows={rows}
        {...rest}
        style={{
          fontFamily: "var(--font-ui)", fontSize: 15, color: "var(--ink-1)", resize: "vertical",
          background: "var(--surface-raised)", border: `1px solid ${error ? "var(--danger)" : "var(--border-strong)"}`,
          borderRadius: "var(--radius-card)", padding: "12px 14px", outline: "none",
        }}
        onFocus={(e) => (e.currentTarget.style.boxShadow = "var(--focus-ring)")}
        onBlur={(e) => (e.currentTarget.style.boxShadow = "none")}
      />
      {error ? <span style={{ fontSize: 12, color: "var(--danger)" }}>{error}</span>
        : hint ? <span style={{ fontSize: 12, color: "var(--ink-3)" }}>{hint}</span> : null}
    </label>
  );
}
