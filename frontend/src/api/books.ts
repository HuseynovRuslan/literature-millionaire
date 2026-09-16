import { api } from './client'
import type { BookListItem } from '../types/book'

export async function getBooks(): Promise<BookListItem[]> {
  const { data } = await api.get<BookListItem[]>('/api/books')
  return data
}
