/**
 * What colour each quiz category wears.
 *
 * Keyed on the quiz mode's slug, not on where the card happens to sit in the grid. That was the whole
 * problem: the accent used to come from the card's index, so adding a category, disabling one or
 * changing DisplayOrder repainted the others - "Ayın Kitabı" would arrive one morning in somebody
 * else's colour. A category's identity has to belong to the category.
 *
 * One value per theme on purpose. The card derives the header gradient, the icon tile, the divider,
 * the edge line and the glow from this single accent with `color-mix`, so there is one place that
 * decides what a category looks like and one place that decides how that look is built - rather than
 * five near-identical colour strings per category, and three near-identical card components.
 *
 * Colour is never the only signal: every category also carries its own glyph (QuizMode.iconKey, drawn
 * by QuizModeIcon), so the three read apart for a colour-blind player exactly as they do for anyone
 * else. Never remove one of the two.
 *
 * The accents are darker than the hue they name because white text sits on top of them in the header;
 * each is checked against that, not picked for how it looks on its own.
 */
export interface CategoryTheme {
  /** The category's colour. Everything else on the card is mixed from it. */
  accent: string
  /** Plain-language note on the intent, so a future edit knows what it is preserving. */
  note: string
}

export const categoryThemes: Record<string, CategoryTheme> = {
  // Bilik Dünyası - general knowledge. Indigo/violet, the product's own brand hue. Globe.
  'bilik-dunyasi': { accent: '#6d54f0', note: 'indiqo / bənövşəyi' },
  // Ayın Kitabı - the month's book. Bordo, the deep red of a hardback and of the kiosk's older skin. Open book.
  'ayin-kitabi': { accent: '#a81e52', note: 'bordo / tünd qırmızı' },
  // Ədəbiyyat Dünyası - literature. Teal, so it is not a second red next to Ayın Kitabı. Feather.
  'edebiyyat-dunyasi': { accent: '#0d7b8c', note: 'firuzəyi' },
  // Yaşıl Bakı - the city's plants. Emerald, the obvious one, and the only green on the board. Leaf.
  'yasil-baki': { accent: '#1b9560', note: 'zümrüd / yaşıl' },
  // Green Garden - a nursery catalogue. Terracotta, for the pots the whole catalogue is photographed in,
  // and because a second green beside Yaşıl Bakı would make the two plant categories one blur. Sprout.
  'green-garden': { accent: '#b4551f', note: 'terrakota / saxsı' },
}

/**
 * Categories whose bank is still being worked on, marked on the card as a test version.
 *
 * A player who meets a half-finished bank with no warning reads it as a broken product rather than an
 * unfinished one, and says so to everyone else. Saying it first costs a line on the card and buys the
 * freedom to publish early. Remove the slug when the bank is finished - that is the whole mechanism.
 */
export const previewCategories: ReadonlySet<string> = new Set([
  // Green Garden: the catalogue's own names are trade names, and half of them carry no sourced
  // Azerbaijani name yet. Playable, not finished.
  'green-garden',
])

/**
 * For a category this build has never heard of. The backend can add a quiz mode without a frontend
 * release, and an unthemed card must still look deliberate - so it takes a colour from the answer
 * palette by position, which is what every card did before themes existed.
 */
const FALLBACK_ACCENTS = ['var(--color-brand)', 'var(--color-opt-a)', 'var(--color-opt-d)', 'var(--color-opt-b)', 'var(--color-opt-c)']

export function accentFor(slug: string, index: number): string {
  return categoryThemes[slug]?.accent ?? FALLBACK_ACCENTS[index % FALLBACK_ACCENTS.length]
}
