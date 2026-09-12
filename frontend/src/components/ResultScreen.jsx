import { useEffect, useMemo, useRef, useState } from 'react'
import { Buta, Divider, Medallion } from './Ornaments'
import { buildLeaderboard } from '../lib/random'
import { prizeFor } from '../data/prizes'
import { saveResult } from '../lib/storage'

const IDLE_RETURN_MS = 90000
// sıra şəkilçisi ahəng qanununa görə: 1-ci, 3-cü, 6-cı, 9-cu...
const ORDINAL = { 1: 'ci', 2: 'ci', 3: 'cü', 4: 'cü', 5: 'ci', 6: 'cı', 7: 'ci', 8: 'ci', 9: 'cu', 10: 'cu' }

function verdict(correct, total) {
  const r = correct / total
  if (r >= 0.9) return 'Əla! Əsl ustad oxucusan'
  if (r >= 0.6) return 'Çox yaxşı nəticə!'
  if (r >= 0.3) return 'Pis deyil, davam et!'
  return 'Kitabı bir daha vərəqlə'
}

export default function ResultScreen({ results, player, quiz, onRestart, onLibrary, onExit }) {
  const score = results.reduce((s, r) => s + r.points, 0)
  const total = results.length
  const prize = prizeFor(score)
  const playerName = player ? `${player.ad} ${player.soyad?.[0] || ''}.`.toUpperCase() : 'SƏN'
  const board = useMemo(() => buildLeaderboard(score, playerName), [score, playerName])
  const myRank = board.findIndex((r) => r.me) + 1
  const stars = score >= 9 ? 3 : score >= 7 ? 2 : score >= 5 ? 1 : 0
  const saved = useRef(false)

  // nəticəni cihazın yaddaşına yaz (bir dəfə)
  useEffect(() => {
    if (saved.current) return
    saved.current = true
    saveResult({
      ad: player?.ad,
      soyad: player?.soyad,
      ataAdi: player?.ataAdi,
      fin: player?.fin,
      telefon: player?.telefonTam,
      kitab: quiz?.title,
      xal: score,
      hediyye: prize ? prize.name : null,
    })
  }, [player, quiz, score, prize])

  // Toxunulmasa, ekran yeni iştirakçı üçün başa qayıtsın (kiosk rejimi)
  useEffect(() => {
    let t = setTimeout(onExit, IDLE_RETURN_MS)
    const reset = () => {
      clearTimeout(t)
      t = setTimeout(onExit, IDLE_RETURN_MS)
    }
    window.addEventListener('pointerdown', reset)
    return () => {
      clearTimeout(t)
      window.removeEventListener('pointerdown', reset)
    }
  }, [onExit])

  return (
    <section className="screen result-screen">
      <div className="result-summary enter">
        <div className="eyebrow">Oyun bitdi</div>
        <h1 className="result-verdict">{verdict(score, total)}</h1>

        <div className="result-stars">
          {[0, 1, 2].map((i) => (
            <Buta
              key={i}
              className={`result-star ${i < stars ? 'on' : ''}`}
              style={{ animationDelay: `${500 + i * 220}ms` }}
            />
          ))}
        </div>

        <div className="score-medallion">
          <Medallion className="score-medallion-bg" />
          <span className="result-score-label">Ümumi xal</span>
          <span className="result-score-value">
            {score}
            <i>/{total}</i>
          </span>
        </div>

        {prize ? (
          <div className={`prize ${prize.top ? 'prize-top' : ''}`}>
            <div className="prize-head">Siz bu hədiyyəni qazandınız!</div>
            <div className="prize-name">{prize.name}</div>
            {prize.note && <div className="prize-note">{prize.note}</div>}
          </div>
        ) : (
          <div className="prize prize-none">
            <div className="prize-head">Hədiyyə üçün ən azı 5 xal lazımdır</div>
            <div className="prize-note">Bir daha cəhd edin — bacararsınız!</div>
          </div>
        )}

        <div className="result-meta">
          <span>
            <b>{myRank}</b>-{ORDINAL[myRank]} yer
          </span>
        </div>

        <Divider />

        <div className="result-actions">
          <button className="btn btn-primary" onClick={onRestart}>
            <span>YENİDƏN OYNA</span>
          </button>
          <button className="btn btn-ghost" onClick={onLibrary}>
            BAŞQA KİTAB
          </button>
          <button className="btn btn-ghost" onClick={onExit}>
            BİTİR
          </button>
        </div>
      </div>

      <div className="leaderboard">
        <div className="leaderboard-head">
          <Buta className="lb-head-buta" flip />
          <span>LİDER TABLOSU</span>
          <Buta className="lb-head-buta" />
        </div>
        <ol className="leaderboard-list">
          {board.map((row, i) => (
            <li
              key={i}
              className={`lb-row ${row.me ? 'lb-me' : ''} ${i < 3 ? `lb-top lb-top-${i + 1}` : ''}`}
              style={{ animationDelay: `${250 + i * 80}ms` }}
            >
              <span className="lb-rank">
                <span>{i + 1}</span>
              </span>
              <span className="lb-name">{row.name}</span>
              <span className="lb-score">{row.score}</span>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
