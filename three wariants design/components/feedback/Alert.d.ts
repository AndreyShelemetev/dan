export interface AlertProps {
  tone?: "info" | "success" | "warning" | "danger";
  title?: React.ReactNode;
  /** Текст: что произошло и что делать; без кодов и stack trace */
  children?: React.ReactNode;
  /** Кнопка или ссылка следующего шага */
  action?: React.ReactNode;
  style?: React.CSSProperties;
}
