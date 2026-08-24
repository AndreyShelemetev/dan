export interface TagProps {
  children?: React.ReactNode;
  /** Крестик удаления (фильтры) */
  onRemove?: () => void;
  style?: React.CSSProperties;
}
