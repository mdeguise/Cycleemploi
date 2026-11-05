import { useEffect, useId, useRef, useState } from 'react';
import { REGLES_DE_PAYE, REGLE_DE_PAYE_DISPLAY } from '../data/catalogs';
import { ClockIcon, UtensilsIcon, ChevronDownIcon } from './icons';

interface RegleDePayeSelectProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

export function RegleDePayeSelect({ value, onChange, disabled }: RegleDePayeSelectProps) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const listId = useId();

  useEffect(() => {
    const handleClickOutside = (ev: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(ev.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const selectedDisplay = value ? REGLE_DE_PAYE_DISPLAY[value] : undefined;

  return (
    <div className={`regle-paye-select${disabled ? ' regle-paye-select--disabled' : ''}`} ref={rootRef}>
      <button
        ref={triggerRef}
        type="button"
        className="regle-paye-select__trigger"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls={isOpen ? listId : undefined}
        onClick={() => !disabled && setIsOpen((v) => !v)}
      >
        {value ? (
          selectedDisplay ? (
            <span className="regle-paye-select__row">
              <ClockIcon style={{ width: 15, height: 15 }} />
              <span>{selectedDisplay.hours}</span>
              <span className="regle-paye-select__sep">|</span>
              <UtensilsIcon style={{ width: 15, height: 15 }} />
              <span>{selectedDisplay.repas}</span>
            </span>
          ) : (
            <span>{value === 'AUCUNE' ? 'Aucune' : value}</span>
          )
        ) : (
          <span className="regle-paye-select__placeholder">Sélectionner</span>
        )}
        <ChevronDownIcon style={{ width: 14, height: 14, marginLeft: 'auto' }} />
      </button>
      {isOpen && (
        <div
          className="regle-paye-select__list"
          id={listId}
          role="listbox"
          onKeyDown={(ev) => {
            const options = [...ev.currentTarget.querySelectorAll<HTMLElement>('[role="option"]')];
            const i = options.indexOf(document.activeElement as HTMLElement);
            if (ev.key === 'ArrowDown') { ev.preventDefault(); options[Math.min(i + 1, options.length - 1)]?.focus(); }
            else if (ev.key === 'ArrowUp') { ev.preventDefault(); options[Math.max(i - 1, 0)]?.focus(); }
            else if (ev.key === 'Escape') { ev.stopPropagation(); setIsOpen(false); triggerRef.current?.focus(); }
          }}
        >
          {REGLES_DE_PAYE.map((regle) => {
            const display = REGLE_DE_PAYE_DISPLAY[regle];
            return (
              <div
                key={regle}
                className="regle-paye-select__option"
                role="option"
                aria-selected={regle === value}
                tabIndex={0}
                ref={(el) => { if (el && regle === (value || REGLES_DE_PAYE[0]) && !el.dataset.focused) { el.dataset.focused = '1'; el.focus(); } }}
                onClick={() => {
                  onChange(regle);
                  setIsOpen(false);
                  triggerRef.current?.focus();
                }}
                onKeyDown={(ev) => {
                  if (ev.key === 'Enter' || ev.key === ' ') {
                    ev.preventDefault();
                    onChange(regle);
                    setIsOpen(false);
                    triggerRef.current?.focus();
                  }
                }}
              >
                {display ? (
                  <span className="regle-paye-select__row">
                    <ClockIcon style={{ width: 15, height: 15 }} />
                    <span>{display.hours}</span>
                    <span className="regle-paye-select__sep">|</span>
                    <UtensilsIcon style={{ width: 15, height: 15 }} />
                    <span>{display.repas}</span>
                  </span>
                ) : (
                  <span>{regle === 'AUCUNE' ? 'Aucune' : regle}</span>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
