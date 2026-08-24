export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  /** Подсказка под полем; ошибка её заменяет */
  hint?: string;
  /** Текст ошибки: что произошло и что делать, без кодов */
  error?: string;
  style?: React.CSSProperties;
  inputStyle?: React.CSSProperties;
}
