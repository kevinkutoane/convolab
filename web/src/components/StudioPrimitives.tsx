import {
  PanelLeftClose,
  PanelLeftOpen,
  PanelRightClose,
  PanelRightOpen,
  X,
  type LucideIcon,
} from "lucide-react";
import {
  useEffect,
  useId,
  useMemo,
  useState,
  type HTMLAttributes,
  type ReactNode,
} from "react";

export function GlassPanel({
  className = "",
  ...props
}: HTMLAttributes<HTMLElement>) {
  return <section className={`glass-panel ${className}`.trim()} {...props} />;
}

export function StudioPageHeader({
  eyebrow,
  title,
  description,
  icon: Icon,
  actions,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  icon?: LucideIcon;
  actions?: ReactNode;
}) {
  return (
    <header className="studio-page-header">
      <div className="studio-page-heading">
        {Icon && <span className="studio-page-icon"><Icon size={21} aria-hidden="true" /></span>}
        <div>
          {eyebrow && <span className="page-eyebrow">{eyebrow}</span>}
          <h1>{title}</h1>
          {description && <p>{description}</p>}
        </div>
      </div>
      {actions && <div className="studio-page-actions">{actions}</div>}
    </header>
  );
}

export function StudioField({
  label,
  description,
  required,
  children,
  className = "",
}: {
  label: string;
  description?: string;
  required?: boolean;
  children: ReactNode;
  className?: string;
}) {
  return (
    <label className={`studio-field ${className}`.trim()}>
      <span>{label}{required && <sup aria-label="required"> *</sup>}</span>
      {description && <small>{description}</small>}
      {children}
    </label>
  );
}

export function SingletonSelect({
  label,
  value,
  values,
  allLabel,
  onChange,
  className = "",
}: {
  label: string;
  value: string;
  values?: readonly string[];
  allLabel?: string;
  onChange: (value: string) => void;
  className?: string;
}) {
  const options = useMemo(() => values ?? [], [values]);
  useEffect(() => {
    if (values && value && !options.includes(value)) onChange("");
  }, [onChange, options, value, values]);

  if (!options.length) return null;
  if (options.length === 1) {
    return (
      <div className={`singleton-context ${className}`.trim()}>
        <span>{label}</span>
        <strong>{options[0]}</strong>
      </div>
    );
  }

  return (
    <label className={`singleton-select ${className}`.trim()}>
      <span>{label}</span>
      <select value={value} onChange={event => onChange(event.target.value)}>
        <option value="">{allLabel ?? `All ${label.toLowerCase()}`}</option>
        {options.map(option => <option key={option} value={option}>{option}</option>)}
      </select>
    </label>
  );
}

function storedPaneState(key: string, pane: "left" | "right", fallback: boolean) {
  try {
    const stored = localStorage.getItem(`convolab.layout.${key}.${pane}`);
    return stored === null ? fallback : stored === "open";
  } catch {
    return fallback;
  }
}

export function AdaptiveWorkspace({
  storageKey,
  leftLabel = "Library",
  rightLabel = "Inspector",
  hasLeft = true,
  hasRight = true,
  children,
  className = "",
}: {
  storageKey: string;
  leftLabel?: string;
  rightLabel?: string;
  hasLeft?: boolean;
  hasRight?: boolean;
  children: ReactNode;
  className?: string;
}) {
  const regionId = useId();
  const compactAtMount = () => window.matchMedia("(max-width: 1179px)").matches;
  const [leftOpen, setLeftOpen] = useState(() => compactAtMount() ? false : storedPaneState(storageKey, "left", true));
  const [rightOpen, setRightOpen] = useState(() => compactAtMount() ? false : storedPaneState(storageKey, "right", true));

  const setPane = (pane: "left" | "right", open: boolean) => {
    if (pane === "left") setLeftOpen(open);
    else setRightOpen(open);
    try {
      localStorage.setItem(`convolab.layout.${storageKey}.${pane}`, open ? "open" : "closed");
    } catch {
      // Presentation preferences are optional when storage is unavailable.
    }
  };

  return (
    <section
      className={`adaptive-workspace ${leftOpen ? "pane-left-open" : "pane-left-closed"} ${rightOpen ? "pane-right-open" : "pane-right-closed"} ${className}`.trim()}
      aria-label="Adaptive workspace"
    >
      <div className="adaptive-workspace-toolbar">
        <span>Workspace layout</span>
        <div>
          {hasLeft && (
            <button
              type="button"
              className="secondary-button compact-button"
              aria-controls={regionId}
              aria-expanded={leftOpen}
              onClick={() => setPane("left", !leftOpen)}
            >
              {leftOpen ? <PanelLeftClose size={15} /> : <PanelLeftOpen size={15} />}
              {leftOpen ? `Hide ${leftLabel}` : `Show ${leftLabel}`}
            </button>
          )}
          {hasRight && (
            <button
              type="button"
              className="secondary-button compact-button"
              aria-controls={regionId}
              aria-expanded={rightOpen}
              onClick={() => setPane("right", !rightOpen)}
            >
              {rightOpen ? <PanelRightClose size={15} /> : <PanelRightOpen size={15} />}
              {rightOpen ? `Hide ${rightLabel}` : `Show ${rightLabel}`}
            </button>
          )}
        </div>
      </div>
      <div id={regionId} className="adaptive-workspace-region">
        {children}
      </div>
      {(leftOpen || rightOpen) && (
        <>
          <button
            type="button"
            className="adaptive-pane-backdrop"
            aria-label="Close workspace panels"
            onClick={() => { setPane("left", false); setPane("right", false); }}
          />
          <button
            type="button"
            className="adaptive-drawer-close icon-button"
            aria-label="Close workspace panel"
            onClick={() => { setPane("left", false); setPane("right", false); }}
          >
            <X size={18} />
          </button>
        </>
      )}
    </section>
  );
}
