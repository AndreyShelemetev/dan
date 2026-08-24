export interface StatusTimelineStep {
  /** Публичный текст статуса («Смета готова») */
  label: string;
  /** Дата, следующий шаг или пояснение */
  note?: string;
  state: "done" | "current" | "upcoming";
}
export interface StatusTimelineProps {
  steps?: StatusTimelineStep[];
  orientation?: "vertical" | "horizontal";
  style?: React.CSSProperties;
}
