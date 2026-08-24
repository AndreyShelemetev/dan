export interface TabsProps {
  items?: Array<string | { id: string; label: string }>;
  active?: string;
  onChange?: (id: string) => void;
  style?: React.CSSProperties;
}
