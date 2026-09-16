import { Navigate, Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import { GameProvider } from './game/GameContext'
import HomePage from './pages/HomePage'
import RegisterPage from './pages/RegisterPage'
import GamePage from './pages/GamePage'
import LeaderboardPage from './pages/LeaderboardPage'
import PlantCreditsPage from './pages/PlantCreditsPage'
import AdminQuestionsPage from './pages/AdminQuestionsPage'

export default function App() {
  return (
    <GameProvider>
      <Routes>
        {/* Kiosk screens: full viewport, no chrome. */}
        <Route index element={<HomePage />} />
        {/* No campaignId: never silently pick a default campaign - back to category selection. */}
        <Route path="register" element={<Navigate to="/" replace />} />
        <Route path="register/:campaignId" element={<RegisterPage />} />
        <Route path="game" element={<GamePage />} />
        <Route path="leaderboard/:campaignId" element={<LeaderboardPage />} />
        {/* Photograph credits for the plant catalogue; the picture licences require them to stay public. */}
        <Route path="sekil-menbeleri" element={<PlantCreditsPage />} />

        {/* Admin keeps the plain layout with navigation. */}
        <Route element={<Layout />}>
          <Route path="admin/questions" element={<AdminQuestionsPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </GameProvider>
  )
}
