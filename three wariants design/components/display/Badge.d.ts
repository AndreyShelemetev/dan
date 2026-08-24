export interface BadgeProps {
  /** Тон по смыслу статуса; статус всегда дублируется текстом, не только цветом */
  tone?: "neutral" | "accent" | "success" | "info" | "warning" | "danger";
  /** solid — только для финальных состояний («Завершено») */
  solid?: boolean;
  children?: React.ReactNode;
  style?: React.CSSProperties;
}
