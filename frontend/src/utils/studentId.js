/** The backend accepts exactly 17 digits (birth registration number). Mirror that check here. */
export const STUDENT_ID_PATTERN = /^\d{17}$/;

export function isValidStudentId(value) {
  return STUDENT_ID_PATTERN.test((value ?? '').trim());
}
