import type { HTMLAttributes, ReactNode } from 'react';
import { useDialog } from '../hooks/useDialog';

interface DialogProps {
  /** Id of the element that titles the dialog (its accessible name). */
  labelledBy: string;
  /** Class of the dialog card itself (the dimmed overlay around it is always .modal-overlay). */
  cardClassName: string;
  /** Closes on a click outside the card (mouse convenience only — Escape is the keyboard path). */
  onBackdrop?: () => void;
  /** Handlers for the overlay when the default click-to-close is not wanted (see ReportIssueModal). */
  overlayProps?: HTMLAttributes<HTMLDivElement>;
  /** Escape closes through this; leave undefined to ignore Escape (e.g. unsaved text). */
  onEscape?: () => void;
  children: ReactNode;
}

/** A modal dialog with the semantics and keyboard behaviour screen-reader and keyboard users need: role="dialog",
 * aria-modal, a name, focus moved in on open, Tab kept inside, Escape to close, focus restored on close. */
export function Dialog({ labelledBy, cardClassName, onBackdrop, overlayProps, onEscape, children }: DialogProps) {
  const ref = useDialog<HTMLDivElement>(onEscape);
  return (
    <div className="modal-overlay" role="presentation" onClick={onBackdrop} {...overlayProps}>
      <div
        ref={ref}
        className={cardClassName}
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
        tabIndex={-1}
        onClick={(ev) => ev.stopPropagation()}
      >
        {children}
      </div>
    </div>
  );
}
