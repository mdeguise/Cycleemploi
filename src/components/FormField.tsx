import { Children, cloneElement, isValidElement, useId } from 'react';
import type { ReactElement, ReactNode } from 'react';
import { CheckIcon } from './icons';

interface FieldProps {
  label: ReactNode;
  required?: boolean;
  children: ReactNode;
  valid?: boolean;
}

const CONTROLS = ['input', 'select', 'textarea'];

/** Binds the visible label to the first native control among the children (htmlFor/id) so screen readers announce
 * it, and marks it aria-required when the field is required. Children that are not a native control (a custom
 * select, a date picker) get a labelled group instead. */
export function Field({ label, required, children, valid }: FieldProps) {
  const generatedId = useId();
  const labelId = `${generatedId}-label`;
  let controlId: string | undefined;
  const content = Children.map(children, (child) => {
    if (controlId === undefined && isValidElement(child) && typeof child.type === 'string' && CONTROLS.includes(child.type)) {
      const el = child as ReactElement<{ id?: string }>;
      controlId = el.props.id ?? generatedId;
      return cloneElement(el, { id: controlId, ...(required ? { 'aria-required': true } : {}) } as Record<string, unknown>);
    }
    return child;
  });
  return (
    <div className="field">
      <label className="field__label" id={labelId} htmlFor={controlId}>
        {label}
        {required && (
          <>
            <span className="required" aria-hidden="true">*</span>
            <span className="sr-only"> (obligatoire)</span>
          </>
        )}
      </label>
      <div className="field__input-wrap" {...(controlId === undefined ? { role: 'group', 'aria-labelledby': labelId } : {})}>
        {content}
        {valid && <CheckIcon className="field__check-icon" style={{ width: 16, height: 16 }} />}
      </div>
    </div>
  );
}

interface SectionTitleProps {
  icon: ReactNode;
  children: ReactNode;
}

export function SectionTitle({ icon, children }: SectionTitleProps) {
  return (
    <div className="field-section-title">
      <span className="field-section-title__icon">{icon}</span>
      {children}
    </div>
  );
}
