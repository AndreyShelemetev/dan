export function PhotoCompare({ before, after, angle, style }) {
  const ph = (label, src, tone) => (
    <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 6 }}>
      {src ? (
        <img src={src} alt={`${angle || "Ракурс"} — ${label}`} style={{ width: "100%", aspectRatio: "4/3", objectFit: "cover", borderRadius: "var(--radius-card)" }} />
      ) : (
        <div style={{ aspectRatio: "4/3", background: tone, borderRadius: "var(--radius-card)", display: "grid", placeItems: "center", fontSize: 13, color: "var(--ink-3)" }}>{label}</div>
      )}
      <div style={{ fontSize: 12, color: "var(--ink-3)", textAlign: "center" }}>{label}</div>
    </div>
  );
  return (
    <div style={{ fontFamily: "var(--font-ui)", ...style }}>
      {angle && <div style={{ fontSize: 13, fontWeight: 600, color: "var(--ink-1)", marginBottom: 8 }}>{angle}</div>}
      <div style={{ display: "flex", gap: 12 }}>
        {ph("до", before, "#E9E1D4")}
        {ph("после", after, "var(--accent-soft)")}
      </div>
    </div>
  );
}
