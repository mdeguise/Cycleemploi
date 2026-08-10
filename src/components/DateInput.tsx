import { useEffect, useRef, useState } from 'react';
import { CalendarIcon, ChevronRightIcon } from './icons';

interface DateInputProps {
  value: string; // ISO AAAA-MM-JJ, or ''
  onChange: (value: string) => void;
}

const MOIS_FR = [
  'janvier', 'février', 'mars', 'avril', 'mai', 'juin',
  'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre',
];
const JOURS_FR = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];

function parseIso(value: string): Date | null {
  if (!value) return null;
  const [year, month, day] = value.split('-').map(Number);
  if (!year || !month || !day) return null;
  return new Date(year, month - 1, day);
}

function toIso(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function formatJjMmAaaa(value: string): string {
  const date = parseIso(value);
  if (!date) return '';
  return `${String(date.getDate()).padStart(2, '0')}-${String(date.getMonth() + 1).padStart(2, '0')}-${date.getFullYear()}`;
}

function daysInMonth(year: number, month: number): number {
  return new Date(year, month + 1, 0).getDate();
}

// Monday-first weekday index (0 = Monday ... 6 = Sunday).
function mondayFirstDay(year: number, month: number): number {
  const jsDay = new Date(year, month, 1).getDay(); // 0 = Sunday
  return (jsDay + 6) % 7;
}

export function DateInput({ value, onChange }: DateInputProps) {
  const [isOpen, setIsOpen] = useState(false);
  const selected = parseIso(value);
  const [viewYear, setViewYear] = useState(selected?.getFullYear() ?? new Date().getFullYear());
  const [viewMonth, setViewMonth] = useState(selected?.getMonth() ?? new Date().getMonth());
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

  const openPicker = () => {
    const current = parseIso(value) ?? new Date();
    setViewYear(current.getFullYear());
    setViewMonth(current.getMonth());
    setIsOpen(true);
  };

  const changeMonth = (delta: number) => {
    let month = viewMonth + delta;
    let year = viewYear;
    if (month < 0) {
      month = 11;
      year -= 1;
    } else if (month > 11) {
      month = 0;
      year += 1;
    }
    setViewMonth(month);
    setViewYear(year);
  };

  const selectDay = (day: number) => {
    onChange(toIso(new Date(viewYear, viewMonth, day)));
    setIsOpen(false);
  };

  const totalDays = daysInMonth(viewYear, viewMonth);
  const leadingBlanks = mondayFirstDay(viewYear, viewMonth);
  const cells: (number | null)[] = [...Array(leadingBlanks).fill(null), ...Array.from({ length: totalDays }, (_, i) => i + 1)];

  return (
    <div className="date-input" ref={rootRef}>
      <button type="button" className="date-input__trigger" onClick={openPicker}>
        <CalendarIcon style={{ width: 15, height: 15 }} />
        <span className={value ? undefined : 'date-input__placeholder'}>{value ? formatJjMmAaaa(value) : 'jj-mm-aaaa'}</span>
      </button>
      {isOpen && (
        <div className="date-input__popup">
          <div className="date-input__header">
            <button type="button" className="date-input__nav" onClick={() => changeMonth(-1)} aria-label="Mois précédent">
              <ChevronRightIcon style={{ width: 14, height: 14, transform: 'rotate(180deg)' }} />
            </button>
            <span className="date-input__month-label">
              {MOIS_FR[viewMonth]} {viewYear}
            </span>
            <button type="button" className="date-input__nav" onClick={() => changeMonth(1)} aria-label="Mois suivant">
              <ChevronRightIcon style={{ width: 14, height: 14 }} />
            </button>
          </div>
          <div className="date-input__weekdays">
            {JOURS_FR.map((j, i) => (
              <span key={`${j}-${i}`}>{j}</span>
            ))}
          </div>
          <div className="date-input__days">
            {cells.map((day, i) => {
              if (day === null) return <span key={`blank-${i}`} />;
              const isSelected =
                selected && selected.getFullYear() === viewYear && selected.getMonth() === viewMonth && selected.getDate() === day;
              return (
                <button
                  key={day}
                  type="button"
                  className={`date-input__day${isSelected ? ' date-input__day--selected' : ''}`}
                  onClick={() => selectDay(day)}
                >
                  {day}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
