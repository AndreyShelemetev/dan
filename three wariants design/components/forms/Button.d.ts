export interface ButtonProps {
  /** primary — одно главное действие на экран; secondary — рядом с primary; ghost — третичное; danger — необратимые действия */
  variant?: "primary" | "secondary" | "ghost" | "danger";
  size?: "sm" | "md" | "lg";
  disabled?: boolean;
  children?: React.ReactNode;
  onClick?: () => void;
  style?: React.CSSProperties;
}
