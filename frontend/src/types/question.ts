export type Difficulty = 'Easy' | 'Medium' | 'Hard'

export type CorrectOption = 'A' | 'B' | 'C' | 'D'

export interface Question {
  id: number
  text: string
  optionA: string
  optionB: string
  optionC: string
  optionD: string
  correctOption: CorrectOption
  difficulty: Difficulty
  category: string
  explanation: string | null
  createdAt: string
  /** null only for legacy questions not yet assigned to a book */
  bookId: number | null
  bookTitle: string | null
  /** Optional local illustration (/question-images/...). Both null or both set. */
  imageUrl: string | null
  imageAltText: string | null
}

/** Write shape: every new or updated question must name its book. */
export type QuestionInput = Omit<Question, 'id' | 'createdAt' | 'bookId' | 'bookTitle'> & { bookId: number }
