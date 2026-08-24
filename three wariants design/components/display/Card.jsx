export function Card({ title, meta, children, padding = 24, style }) {
  return (
    <div style={{
      background: "var(--surface-card)", border: "1px solid var(--border)",
      borderRadius: "var(--radius-card)", boxShadow: "var(--shadow-card)",
      padding, fontFamily: "var(--font-ui)", ...style,
    }}>
      {(title || meta) && (
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 12, marginBottom: 14 }}>
          {title && <div style={{ fontFamily: "var(--font-display)", fontSize: 20, color: "var(--ink-1)" }}>{title}</div>}
          {meta && <div style={{ fontSize: 12, color: "var(--ink-3)" }}>{meta}</div>}
        </div>
      )}
      {children}
    </div>
  );
}
