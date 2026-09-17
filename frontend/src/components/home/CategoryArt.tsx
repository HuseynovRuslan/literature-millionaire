import { useState } from 'react'
import type { CampaignBook } from '../../types/campaign'
import { categoryArt } from './categoryThemes'

/**
 * The decorative picture on the right of a category card.
 *
 * Purely decorative: the card's title already names the category, so the image is hidden from screen
 * readers (empty alt, aria-hidden) and takes no pointer events - it can sit over the header band without
 * ever intercepting a click meant for the card.
 *
 * It is confined to the card's top zone by the card's layout, not by tuned offsets: CategoryCard puts
 * this inside the zone that holds only the header and the description, and pads that text on the right.
 * The chips, the reward and the buttons live in a separate zone below, where the picture cannot reach.
 *
 * A picture that fails to load removes itself rather than leaving a broken-image icon on the card. That
 * matters most for the book cover, which is data: next month's book may name a file nobody uploaded yet.
 */
export default function CategoryArt({ slug, book }: { slug: string; book: CampaignBook | null }) {
  const [failed, setFailed] = useState(false)
  const art = categoryArt[slug]
  if (!art || failed) return null

  const src = art.kind === 'cover' ? book?.coverImageUrl.trim() : art.src
  if (!src) return null

  return (
    <div aria-hidden="true" data-art={slug} data-art-kind={art.kind} className="card-art">
      <span className="card-art-glow" />
      <img
        src={src}
        alt=""
        draggable={false}
        decoding="async"
        onError={() => setFailed(true)}
        className="card-art-img"
      />
    </div>
  )
}
