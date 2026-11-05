import type { KeyboardEvent } from 'react';

/** Props that make a non-button element (a result row, a card) behave like a button for the keyboard and for screen
 * readers: reachable with Tab, announced as a button, activated with Enter or Space. Prefer a real <button> when the
 * markup allows it. */
export function pressable(onActivate: () => void) {
  return {
    role: 'button' as const,
    tabIndex: 0,
    onClick: onActivate,
    onKeyDown: (ev: KeyboardEvent) => {
      if (ev.target !== ev.currentTarget) return;
      if (ev.key === 'Enter' || ev.key === ' ') {
        ev.preventDefault();
        onActivate();
      }
    },
  };
}
