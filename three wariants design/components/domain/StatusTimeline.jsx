export function StatusTimeline({ steps = [], orientation = "vertical", style }) {
  const dot = (s) => ({
    width: 12, height: 12, borderRadius: "50%", flexShrink: 0,
    background: s.state === "upcoming" ? "var(--surface)" : "var(--accent)",
    border: `1px solid ${s.state === "upcoming" ? "var(--border-strong)" : "var(--accent)"}`,
    boxShadow: s.state === "current" ? "var(--focus-ring)" : "none",
  });
  if (orientation === "horizontal") {
    return (
      <div style={{ display: "flex", alignItems: "flex-start", fontFamily: "var(--font-ui)", ...style }}>
        {steps.map((s, i) => (
          <div key={i} style={{ display: "flex", alignItems: "flex-start", flex: i < steps.length - 1 ? 1 : "none" }}>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <div style={dot(s)} />
              <div style={{ fontSize: 13, whiteSpace: "nowrap", color: s.state === "upcoming" ? "var(--ink-3)" : "var(--ink-1)", fontWeight: s.state === "current" ? 700 : 400 }}>{s.label}</div>
            </div>
            {i < steps.length - 1 && <div style={{ flex: 1, height: 1, background: "var(--border-strong)", margin: "6px 14px 0" }} />}
          </div>
        ))}
      </div>
    );
  }
  return (
    <div style={{ display: "flex", flexDirection: "column", fontFamily: "var(--font-ui)", ...style }}>
      {steps.map((s, i) => (
        <div key={i} style={{ display: "flex", gap: 14 }}>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
            <div style={dot(s)} />
            {i < steps.length - 1 && <div style={{ width: 1, flex: 1, background: "var(--border-strong)", minHeight: 22 }} />}
          </div>
          <div style={{ paddingBottom: i < steps.length - 1 ? 16 : 0, marginTop: -2 }}>
            <div style={{ fontSize: 14, fontWeight: s.state === "current" ? 700 : 600, color: s.state === "upcoming" ? "var(--ink-3)" : "var(--ink-1)" }}>{s.label}</div>
            {s.note && <div style={{ fontSize: 12, color: "var(--ink-3)", marginTop: 2 }}>{s.note}</div>}
          </div>
        </div>
      ))}
    </div>
  );
}
