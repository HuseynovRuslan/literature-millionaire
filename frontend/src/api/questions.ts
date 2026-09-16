import { api } from './client'
import type { Difficulty, Question, QuestionInput } from '../types/question'

export interface QuestionFilter {
  difficulty?: Difficulty
  category?: string
  bookId?: number
}

export async function getQuestions(filter: QuestionFilter = {}): Promise<Question[]> {
  const { data } = await api.get<Question[]>('/api/questions', { params: filter })
  return data
}

export async function getQuestion(id: number): Promise<Question> {
  const { data } = await api.get<Question>(`/api/questions/${id}`)
  return data
}

export async function createQuestion(input: QuestionInput): Promise<Question> {
  const { data } = await api.post<Question>('/api/questions', input)
  return data
}

export async function updateQuestion(id: number, input: QuestionInput): Promise<Question> {
  const { data } = await api.put<Question>(`/api/questions/${id}`, input)
  return data
}

export async function deleteQuestion(id: number): Promise<void> {
  await api.delete(`/api/questions/${id}`)
}
