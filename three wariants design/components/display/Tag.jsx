export function Tag({ children, onRemove, style }) {
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 6,
      background: "var(--surface-raised)", border: "1px solid var(--border)",
      color: "var(--ink-2)", fontFamily: "var(--font-ui)", fontSize: 12, fontWeight: 500,
      borderRadius: "var(--radius-pill)", padding: "5px 12px", ...style,
    }}>
      {children}
      {onRemove && <span onClick={onRemove} style={{ cursor: "pointer", color: "var(--ink-3)", fontSize: 14, lineHeight: 1 }}>×</span>}
    </span>
  );
}
