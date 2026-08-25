/**
 * The operator's legal details, as published by the company.
 *
 * One place, because they appear in the policy, the consent, the cookie policy and the footer,
 * and requisites that disagree between documents are worse than requisites in only one of them.
 *
 * Source: nasmenu.ru and gigjobs.nasmenu.ru/privacy_policy (retrieved 25 August 2026).
 * КПП and the registered address are not published there — add them before production, since
 * 152-ФЗ expects a data subject to be able to reach the operator in writing.
 */
export const COMPANY = {
  name: "АО «Гибкие технологии работы»",
  shortName: "АО «Гибкие технологии работы»",
  inn: "1200018705",
  ogrn: "1251200001580",
  /** Not published — see the note above. */
  kpp: null as string | null,
  address: null as string | null,
  email: "support@nasmenu.ru",
  /** Dedicated address for withdrawal-of-consent and deletion requests. */
  privacyEmail: "delete.data@nasmenu.ru",
  phone: "+7 (924) 269-47-35",
} as const;

/** Rendered wherever the operator has to be named in full. */
export function companyLine(): string {
  const parts = [COMPANY.name, `ИНН ${COMPANY.inn}`, `ОГРН ${COMPANY.ogrn}`];
  if (COMPANY.kpp) parts.push(`КПП ${COMPANY.kpp}`);
  return parts.join(", ");
}
