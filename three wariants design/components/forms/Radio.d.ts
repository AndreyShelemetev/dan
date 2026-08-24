export interface RadioProps {
  label?: React.ReactNode;
  checked?: boolean;
  onChange?: () => void;
  name?: string;
  disabled?: boolean;
  style?: React.CSSProperties;
}
