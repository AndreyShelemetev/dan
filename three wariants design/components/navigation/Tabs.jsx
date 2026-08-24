export function Tabs({ items = [], active, onChange, style }) {
  return (
    <div style={{ display: "flex", gap: 24, borderBottom: "1px solid var(--border)", fontFamily: "var(--font-ui)", ...style }}>
      {items.map((it) => {
        const id = typeof it === "string" ? it : it.id;
        const label = typeof it === "string" ? it : it.label;
        const isActive = id === active;
        return (
          <button key={id} onClick={() => onChange && onChange(id)} style={{
            background: "none", border: "none", cursor: "pointer",
            fontFamily: "var(--font-ui)", fontSize: 14, fontWeight: isActive ? 700 : 500,
            color: isActive ? "var(--ink-1)" : "var(--ink-3)",
            padding: "10px 2px 12px", marginBottom: -1,
            borderBottom: `2px solid ${isActive ? "var(--accent)" : "transparent"}`,
            transition: "color var(--duration) var(--ease)",
          }}>{label}</button>
        );
      })}
    </div>
  );
}
