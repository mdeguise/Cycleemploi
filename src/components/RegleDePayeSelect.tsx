import { useEffect, useRef, useState } from 'react';
import { REGLES_DE_PAYE, REGLE_DE_PAYE_DISPLAY } from '../data/catalogs';
import { ClockIcon, UtensilsIcon, ChevronDownIcon } from './icons';

interface RegleDePayeSelectProps {
  value: string;
  onChange: (value: string) => void;
}

export function RegleDePayeSelect({ value, onChange }: RegleDePayeSelectProps) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

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
    <div className="regle-paye-select" ref={rootRef}>
      <button type="button" className="regle-paye-select__trigger" onClick={() => setIsOpen((v) => !v)}>
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
        <div className="regle-paye-select__list">
          {REGLES_DE_PAYE.map((regle) => {
            const display = REGLE_DE_PAYE_DISPLAY[regle];
            return (
              <div
                key={regle}
                className="regle-paye-select__option"
                onClick={() => {
                  onChange(regle);
                  setIsOpen(false);
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
