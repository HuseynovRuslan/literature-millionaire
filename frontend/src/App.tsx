import { Navigate, Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import { GameProvider } from './game/GameContext'
import HomePage from './pages/HomePage'
import RegisterPage from './pages/RegisterPage'
import GamePage from './pages/GamePage'
import LeaderboardPage from './pages/LeaderboardPage'
import QrLoginHelpPage from './pages/QrLoginHelpPage'
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
        {/* Where an ordinary phone camera lands when it scans the kiosk's QRLog sign-in QR. */}
        <Route path="qr/:code" element={<QrLoginHelpPage />} />

        {/* Admin keeps the plain layout with navigation. */}
        <Route element={<Layout />}>
          <Route path="admin/questions" element={<AdminQuestionsPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </GameProvider>
  )
}
