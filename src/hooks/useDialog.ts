import { useEffect, useRef } from 'react';

const FOCUSABLE = 'a[href],button:not([disabled]),input:not([disabled]):not([type="hidden"]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

/** Keyboard behaviour every modal dialog needs: focus moves inside when it opens, Tab stays inside while it is
 * open, Escape closes it (when `onEscape` allows) and focus goes back to what opened it. Attach the returned ref to
 * the element that has role="dialog". */
export function useDialog<T extends HTMLElement>(onEscape?: () => void) {
  const ref = useRef<T>(null);
  const escapeRef = useRef(onEscape);
  escapeRef.current = onEscape;
  // Read during the first render, i.e. before any autoFocus inside the dialog has moved focus away from the opener.
  const openerRef = useRef<HTMLElement | null | undefined>(undefined);
  if (openerRef.current === undefined) openerRef.current = document.activeElement as HTMLElement | null;

  useEffect(() => {
    const previous = openerRef.current;
    const node = ref.current;
    if (node && !node.contains(document.activeElement)) {
      (node.querySelector<HTMLElement>(FOCUSABLE) ?? node).focus();
    }

    const onKeyDown = (ev: KeyboardEvent) => {
      const dialog = ref.current;
      if (!dialog) return;
      if (ev.key === 'Escape' && escapeRef.current) {
        ev.stopPropagation();
        escapeRef.current();
        return;
      }
      if (ev.key !== 'Tab') return;
      const items = [...dialog.querySelectorAll<HTMLElement>(FOCUSABLE)].filter((e) => e.offsetParent !== null);
      if (items.length === 0) {
        ev.preventDefault();
        return;
      }
      const first = items[0];
      const last = items[items.length - 1];
      if (ev.shiftKey && (document.activeElement === first || !dialog.contains(document.activeElement))) {
        ev.preventDefault();
        last.focus();
      } else if (!ev.shiftKey && document.activeElement === last) {
        ev.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previous?.focus?.();
    };
  }, []);

  return ref;
}
