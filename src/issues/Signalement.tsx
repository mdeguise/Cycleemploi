import { useEffect, useRef, useState } from 'react'
import { getScreen, snapshot } from './context'

/** The application's name in Le Guichet: its queue in the Signalements space. */
const APPLICATION = 'Cycle Emploi'
/** Le Guichet's screens. Everything about reports lives there; each application only opens this window. */
export const GUICHET = import.meta.env.VITE_GUICHET_URL ?? 'http://localhost:5207'
const ORIGINE = new URL(GUICHET).origin
const URL_FENETRE = `${GUICHET}/signaler.html?app=${encodeURIComponent(APPLICATION)}`

/**
 * « Signaler un problème »: the large window of Le Guichet in a frame. The conversation lives there, in the report.
 * This side only hands over what the user can't say: the screen open and the technical context, frozen at opening.
 * If the frame says nothing after a few seconds (blocked, or the sign-in failed inside it), a link opens the same page in a tab.
 */
export default function Signalement({ onClose }: { onClose: () => void }) {
  const cadre = useRef<HTMLIFrameElement>(null)
  const [contexte] = useState(() => ({ type: 'signalement:contexte', ecran: getScreen(), contexte: JSON.stringify(snapshot(), null, 2) }))
  const [muet, setMuet] = useState(false)

  useEffect(() => {
    const avant = document.activeElement as HTMLElement | null
    const minuterie = setTimeout(() => setMuet(true), 8000)
    const recevoir = (e: MessageEvent) => {
      if (e.origin !== ORIGINE) return
      if (e.data?.type === 'signalement:pret') {
        clearTimeout(minuterie); setMuet(false)
        ;(e.source as Window | null)?.postMessage(contexte, ORIGINE)
      }
      if (e.data?.type === 'signalement:fermer') onClose()
    }
    const clavier = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('message', recevoir)
    window.addEventListener('keydown', clavier)
    return () => {
      clearTimeout(minuterie)
      window.removeEventListener('message', recevoir)
      window.removeEventListener('keydown', clavier)
      avant?.focus()
    }
  }, [contexte, onClose])

  return (
    <div className="signalement-voile" onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="signalement-fenetre" role="dialog" aria-modal="true" aria-label="Signalements">
        <iframe ref={cadre} src={URL_FENETRE} title="Signalements" onLoad={() => cadre.current?.focus()} />
        {muet && (
          <div className="signalement-repli" role="alert">
            <p>La fenêtre des signalements ne s'affiche pas ici.</p>
            <a className="bouton" href={URL_FENETRE} target="_blank" rel="opener" onClick={onClose}>Ouvrir dans un onglet</a>
            <button onClick={onClose}>Fermer</button>
          </div>
        )}
      </div>
    </div>
  )
}
