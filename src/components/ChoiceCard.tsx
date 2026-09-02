import { CheckIcon } from './icons';

interface ChoiceCardProps {
  title: string;
  description?: string;
  badge?: string;
  selected: boolean;
  onToggle: () => void;
  /** Locks the card checked (or unchecked) and ignores clicks — used when another selection
   * implies this one, e.g. Dynaway implying Accès D365. */
  disabled?: boolean;
  /** Shown under the description when disabled, explaining why. */
  disabledHint?: string;
}

export function ChoiceCard({ title, description, badge, selected, onToggle, disabled, disabledHint }: ChoiceCardProps) {
  const className = [
    'choice-card',
    selected ? 'choice-card--selected' : '',
    disabled ? 'choice-card--disabled' : '',
  ].filter(Boolean).join(' ');
  return (
    <div
      className={className}
      onClick={disabled ? undefined : onToggle}
      role="checkbox"
      aria-checked={selected}
      aria-disabled={disabled}
      tabIndex={disabled ? -1 : 0}
    >
      <span className="choice-card__checkbox">{selected && <CheckIcon style={{ width: 12, height: 12 }} />}</span>
      <span>
        <div className="choice-card__title">
          {title}
          {badge && <span className="choice-card__badge">{badge}</span>}
        </div>
        {description && <div className="choice-card__desc">{description}</div>}
        {disabled && disabledHint && <div className="choice-card__desc">{disabledHint}</div>}
      </span>
    </div>
  );
}
