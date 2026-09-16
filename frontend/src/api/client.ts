import axios from 'axios'

// When VITE_API_BASE_URL is empty, requests go to the same origin and the
// Vite dev server proxies /api/* to the backend (see vite.config.ts).
const baseURL = import.meta.env.VITE_API_BASE_URL || ''

export const api = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 10_000,
})
