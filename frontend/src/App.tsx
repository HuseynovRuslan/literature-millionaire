import { Navigate, Route, Routes } from 'react-router-dom'
import { GameProvider } from './game/GameContext'
import HomePage from './pages/HomePage'
import RegisterPage from './pages/RegisterPage'
import GamePage from './pages/GamePage'
import LeaderboardPage from './pages/LeaderboardPage'
import QrLoginHelpPage from './pages/QrLoginHelpPage'
import AdminQuestionsPage from './pages/AdminQuestionsPage'
import AdminApp from './pages/admin/AdminApp'
import AdminAuditPage from './pages/admin/AdminAuditPage'
import AdminCampaignEditorPage from './pages/admin/AdminCampaignEditorPage'
import AdminCampaignsPage from './pages/admin/AdminCampaignsPage'
import AdminHomePage from './pages/admin/AdminHomePage'
import AdminLinkPage from './pages/admin/AdminLinkPage'

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

        {/* Admin panel. Every page under it renders only for a signed-in admin: AdminApp asks the server who is
            signed in before showing anything. A break-glass link lands outside it, because it is how one gets in. */}
        <Route path="admin/link" element={<AdminLinkPage />} />
        <Route path="admin" element={<AdminApp />}>
          <Route index element={<AdminHomePage />} />
          <Route path="campaigns" element={<AdminCampaignsPage />} />
          <Route path="campaigns/new" element={<AdminCampaignEditorPage />} />
          <Route path="campaigns/:id" element={<AdminCampaignEditorPage />} />
          <Route path="audit" element={<AdminAuditPage />} />
          {/* The old question form, now behind the sign-in; replaced by the question editor in a later phase. */}
          <Route path="questions" element={<AdminQuestionsPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </GameProvider>
  )
}
