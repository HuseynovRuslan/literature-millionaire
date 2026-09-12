import { Navigate, Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import { GameProvider } from './game/GameContext'
import HomePage from './pages/HomePage'
import GamePage from './pages/GamePage'
import AdminQuestionsPage from './pages/AdminQuestionsPage'

export default function App() {
  return (
    <GameProvider>
      <Routes>
        {/* Kiosk screens: full viewport, no chrome. */}
        <Route index element={<HomePage />} />
        <Route path="game" element={<GamePage />} />

        {/* Admin keeps the plain layout with navigation. */}
        <Route element={<Layout />}>
          <Route path="admin/questions" element={<AdminQuestionsPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </GameProvider>
  )
}
