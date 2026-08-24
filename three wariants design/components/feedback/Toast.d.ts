export interface ToastProps {
  tone?: "info" | "success" | "warning" | "danger";
  children?: React.ReactNode;
  visible?: boolean;
  style?: React.CSSProperties;
}
