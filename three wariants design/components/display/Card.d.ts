export interface CardProps {
  /** Заголовок — Spectral 20px */
  title?: React.ReactNode;
  /** Мета справа от заголовка (дата, номер) */
  meta?: React.ReactNode;
  children?: React.ReactNode;
  padding?: number | string;
  style?: React.CSSProperties;
}
