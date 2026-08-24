export interface PhotoCompareProps {
  /** URL фото «до»; без URL — плейсхолдер */
  before?: string;
  /** URL фото «после» */
  after?: string;
  /** Название обязательного ракурса («Общий вид», «Памятник») */
  angle?: string;
  style?: React.CSSProperties;
}
