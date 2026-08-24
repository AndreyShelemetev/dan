export function Select({ label, hint, error, options = [], style, ...rest }) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 6, fontFamily: "var(--font-ui)", ...style }}>
      {label && <span style={{ fontSize: 13, fontWeight: 600, color: "var(--ink-1)" }}>{label}</span>}
      <select
        {...rest}
        style={{
          fontFamily: "var(--font-ui)", fontSize: 15, color: "var(--ink-1)", appearance: "auto",
          background: "var(--surface-raised)", border: `1px solid ${error ? "var(--danger)" : "var(--border-strong)"}`,
          borderRadius: "var(--radius-card)", padding: "12px 14px", outline: "none",
        }}
        onFocus={(e) => (e.currentTarget.style.boxShadow = "var(--focus-ring)")}
        onBlur={(e) => (e.currentTarget.style.boxShadow = "none")}
      >
        {options.map((o) => (
          <option key={typeof o === "string" ? o : o.value} value={typeof o === "string" ? o : o.value}>
            {typeof o === "string" ? o : o.label}
          </option>
        ))}
      </select>
      {error ? <span style={{ fontSize: 12, color: "var(--danger)" }}>{error}</span>
        : hint ? <span style={{ fontSize: 12, color: "var(--ink-3)" }}>{hint}</span> : null}
    </label>
  );
}
