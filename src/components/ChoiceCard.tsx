import { CheckIcon } from './icons';

interface ChoiceCardProps {
  title: string;
  description?: string;
  badge?: string;
  selected: boolean;
  onToggle: () => void;
}

export function ChoiceCard({ title, description, badge, selected, onToggle }: ChoiceCardProps) {
  const className = ['choice-card', selected ? 'choice-card--selected' : ''].filter(Boolean).join(' ');
  return (
    <div className={className} onClick={onToggle} role="checkbox" aria-checked={selected} tabIndex={0}>
      <span className="choice-card__checkbox">{selected && <CheckIcon style={{ width: 12, height: 12 }} />}</span>
      <span>
        <div className="choice-card__title">
          {title}
          {badge && <span className="choice-card__badge">{badge}</span>}
        </div>
        {description && <div className="choice-card__desc">{description}</div>}
      </span>
    </div>
  );
}
