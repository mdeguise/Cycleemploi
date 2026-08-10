import type { CSSProperties } from 'react';

type IconProps = { className?: string; style?: CSSProperties };

const base = {
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
  viewBox: '0 0 24 24',
};

export function CalendarIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <rect x="3" y="4" width="18" height="18" rx="2" />
      <path d="M16 2v4M8 2v4M3 10h18" />
    </svg>
  );
}

export function UserIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21c0-4 3.6-7 8-7s8 3 8 7" />
    </svg>
  );
}

export function ClockIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </svg>
  );
}

export function BriefcaseIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <rect x="3" y="7" width="18" height="13" rx="2" />
      <path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M3 12h18" />
    </svg>
  );
}

export function LockIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <rect x="4" y="10" width="16" height="10" rx="2" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" />
    </svg>
  );
}

export function LaptopIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <rect x="4" y="4" width="16" height="10" rx="1" />
      <path d="M2 19h20l-2-3H4Z" />
    </svg>
  );
}

export function GridIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </svg>
  );
}

export function CheckCircleIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <circle cx="12" cy="12" r="9" />
      <path d="m8.5 12.5 2.5 2.5 4.5-5" />
    </svg>
  );
}

export function CheckIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M5 12l5 5L20 7" />
    </svg>
  );
}

export function MapPinIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z" />
      <circle cx="12" cy="10" r="3" />
    </svg>
  );
}

export function InfoIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 8v.01" />
    </svg>
  );
}

export function LightbulbIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M9 18h6M10 22h4M12 2a6 6 0 0 0-4 10.5c.6.6 1 1.4 1 2.5h6c0-1.1.4-1.9 1-2.5A6 6 0 0 0 12 2Z" />
    </svg>
  );
}

export function ShieldIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M12 3l7 3v6c0 4.5-3 8-7 9-4-1-7-4.5-7-9V6Z" />
    </svg>
  );
}

export function LifeBuoyIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <circle cx="12" cy="12" r="9" />
      <circle cx="12" cy="12" r="4" />
      <path d="m5 5 4 4M19 5l-4 4M5 19l4-4M19 19l-4-4" />
    </svg>
  );
}

export function ChevronRightIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="m9 6 6 6-6 6" />
    </svg>
  );
}

export function ChevronDownIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="m6 9 6 6 6-6" />
    </svg>
  );
}

export function SaveIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M5 3h11l3 3v15H5Z" />
      <path d="M8 3v6h8V3M8 21v-7h8v7" />
    </svg>
  );
}

export function SearchIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <circle cx="11" cy="11" r="7" />
      <path d="m21 21-4.3-4.3" />
    </svg>
  );
}

export function PaperclipIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M21 12.5 12.5 21a5 5 0 0 1-7-7l8-8a3.5 3.5 0 0 1 5 5l-7.5 7.5a2 2 0 0 1-3-3L15 8" />
    </svg>
  );
}

export function XIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M18 6 6 18M6 6l12 12" />
    </svg>
  );
}

export function ShirtIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M8 4 4 7v4h3v9h10v-9h3V7l-4-3-3 2h-2Z" />
    </svg>
  );
}

export function AlertTriangleIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="m12 3 10 18H2Z" />
      <path d="M12 10v4M12 17v.01" />
    </svg>
  );
}

export function LogOutIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <path d="m16 17 5-5-5-5" />
      <path d="M21 12H9" />
    </svg>
  );
}

export function FileTextIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M6 2h9l3 3v17H6Z" />
      <path d="M9 12h6M9 16h6M9 8h3" />
    </svg>
  );
}

export function AppsIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <rect x="4" y="4" width="6" height="6" rx="1.5" />
      <rect x="14" y="4" width="6" height="6" rx="1.5" />
      <rect x="4" y="14" width="6" height="6" rx="1.5" />
      <circle cx="17" cy="17" r="3" />
    </svg>
  );
}

export function UtensilsIcon({ className, style }: IconProps) {
  return (
    <svg className={className} style={style} {...base}>
      <path d="M7 2v7a2 2 0 0 0 2 2v11M7 2v7a2 2 0 0 1-2 2v0M7 2v7M4 2v7M17 2c-1.7 0-3 2.2-3 5s1.3 5 3 5v9M17 2v20" />
    </svg>
  );
}
