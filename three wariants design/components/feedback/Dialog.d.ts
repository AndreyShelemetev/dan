export interface DialogProps {
  open?: boolean;
  title?: React.ReactNode;
  children?: React.ReactNode;
  /** Кнопки действий, выравнены вправо */
  footer?: React.ReactNode;
  onClose?: () => void;
  width?: number;
}
