export function Dialog({ open, title, children, footer, onClose, width = 480 }) {
  if (!open) return null;
  return (
    <div onClick={onClose} style={{
      position: "fixed", inset: 0, background: "rgba(43,47,43,0.4)", display: "grid",
      placeItems: "center", zIndex: 100, padding: 24,
    }}>
      <div onClick={(e) => e.stopPropagation()} style={{
        background: "var(--surface-raised)", borderRadius: "var(--radius-modal)",
        boxShadow: "var(--shadow-modal)", width: "100%", maxWidth: width,
        padding: 28, fontFamily: "var(--font-ui)",
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 14 }}>
          <div style={{ fontFamily: "var(--font-display)", fontSize: 22, color: "var(--ink-1)" }}>{title}</div>
          <button onClick={onClose} aria-label="Закрыть" style={{ background: "none", border: "none", fontSize: 20, color: "var(--ink-3)", cursor: "pointer", padding: 4 }}>×</button>
        </div>
        <div style={{ fontSize: 14, color: "var(--ink-2)", lineHeight: 1.55 }}>{children}</div>
        {footer && <div style={{ display: "flex", gap: 12, justifyContent: "flex-end", marginTop: 24 }}>{footer}</div>}
      </div>
    </div>
  );
}
