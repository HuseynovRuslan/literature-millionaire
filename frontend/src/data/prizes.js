// Hədiyyə pillələri. Adları istədiyiniz kimi dəyişə bilərsiniz —
// oyun yalnız `min`/`max` xal aralığına baxır. Maksimum xal: 10.
export const PRIZES = [
  {
    min: 10,
    max: 10,
    name: 'Ağac tingi + "Yaşıl Bakı" sertifikatı',
    note: 'Ən dəyərli hədiyyə',
    top: true,
  },
  {
    min: 9,
    max: 9,
    name: 'Dibçək gülü',
    note: 'Əla nəticə',
  },
  {
    min: 7,
    max: 8,
    name: 'Bakı Abadlıq eko-çantası',
    note: 'Yaxşı nəticə',
  },
  {
    min: 5,
    max: 6,
    name: 'Gül toxumu dəsti',
    note: 'Təşviqat hədiyyəsi',
  },
]

export function prizeFor(score) {
  return PRIZES.find((p) => score >= p.min && score <= p.max) || null
}
