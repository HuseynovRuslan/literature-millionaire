/** Mirrors PlantImageCreditDto: the attribution one catalogue photograph must carry. */
export interface PlantImageCredit {
  plantName: string
  scientificName: string
  fileName: string
  /** Public asset path, e.g. /images/plants/plant-001.webp */
  imageUrl: string
  isPrimary: boolean
  source: string
  sourceUrl: string | null
  author: string | null
  license: string
  licenseUrl: string | null
  /** Species actually pictured, when the catalogue answer is a genus or a wider name. */
  specimenSpecies: string | null
}

/** Mirrors PlantCreditsDto from GET /api/plants/credits. */
export interface PlantCredits {
  plantCount: number
  imageCount: number
  images: PlantImageCredit[]
}
