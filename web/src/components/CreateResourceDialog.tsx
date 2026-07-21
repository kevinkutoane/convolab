import { useEffect, type FormEvent } from "react";
import { LoaderCircle, Plus, X } from "lucide-react";

export interface CreateResourceField {
  name: string;
  label: string;
  placeholder?: string;
  type?: "text" | "textarea" | "select";
  options?: string[];
  required?: boolean;
}

interface CreateResourceDialogProps {
  open: boolean;
  title: string;
  description: string;
  submitLabel: string;
  fields: CreateResourceField[];
  values: Record<string, string>;
  busy?: boolean;
  error?: string;
  onChange: (name: string, value: string) => void;
  onClose: () => void;
  onSubmit: () => void | Promise<void>;
}

export function CreateResourceDialog({
  open,
  title,
  description,
  submitLabel,
  fields,
  values,
  busy = false,
  error,
  onChange,
  onClose,
  onSubmit,
}: CreateResourceDialogProps) {
  useEffect(() => {
    if (!open) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy) onClose();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [busy, onClose, open]);

  if (!open) return null;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!busy) void onSubmit();
  };

  return (
    <div className="resource-dialog-backdrop" role="presentation" onMouseDown={() => !busy && onClose()}>
      <section
        className="resource-dialog panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="resource-dialog-title"
        onMouseDown={event => event.stopPropagation()}
      >
        <div className="resource-dialog-heading">
          <div>
            <span className="panel-eyebrow">Create governed resource</span>
            <h2 id="resource-dialog-title">{title}</h2>
            <p>{description}</p>
          </div>
          <button type="button" className="icon-button" onClick={onClose} disabled={busy} aria-label="Close create dialog"><X size={18} /></button>
        </div>
        <form className="resource-dialog-form" onSubmit={submit}>
          {fields.map((field, index) => (
            <label key={field.name} className={field.type === "textarea" ? "resource-dialog-wide" : undefined}>
              <span>{field.label}</span>
              {field.type === "textarea" ? (
                <textarea autoFocus={index === 0} required={field.required !== false} value={values[field.name] ?? ""} placeholder={field.placeholder} onChange={event => onChange(field.name, event.target.value)} />
              ) : field.type === "select" ? (
                <select autoFocus={index === 0} required={field.required !== false} value={values[field.name] ?? ""} onChange={event => onChange(field.name, event.target.value)}>
                  {field.options?.map(option => <option key={option} value={option}>{option}</option>)}
                </select>
              ) : (
                <input autoFocus={index === 0} required={field.required !== false} value={values[field.name] ?? ""} placeholder={field.placeholder} onChange={event => onChange(field.name, event.target.value)} />
              )}
            </label>
          ))}
          {error && <p className="form-error resource-dialog-wide">{error}</p>}
          <div className="resource-dialog-actions resource-dialog-wide">
            <button type="button" className="secondary-button" onClick={onClose} disabled={busy}>Cancel</button>
            <button type="submit" className="primary-button" disabled={busy}>
              {busy ? <LoaderCircle className="spin" size={15} /> : <Plus size={15} />} {submitLabel}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}
