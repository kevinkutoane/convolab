import { Search, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { navigationItems, quickActions } from "../data/platform";

interface CommandPaletteProps {
  open: boolean;
  onClose: () => void;
}

export function CommandPalette({ open, onClose }: CommandPaletteProps) {
  const [query, setQuery] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (!open) return;
    window.setTimeout(() => inputRef.current?.focus(), 20);
  }, [open]);

  const results = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    const items = [
      ...navigationItems.map(item => ({
        label: item.label,
        description: item.description,
        path: item.path,
        icon: item.icon,
        group: "Navigate",
      })),
      ...quickActions.map(item => ({
        label: item.label,
        description: "Quick action",
        path: item.path,
        icon: item.icon,
        group: "Actions",
      })),
    ];

    if (!normalized) return items.slice(0, 10);
    return items.filter(
      item =>
        item.label.toLowerCase().includes(normalized) ||
        item.description.toLowerCase().includes(normalized)
    );
  }, [query]);

  useEffect(() => {
    if (!open) return;
    const handler = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setQuery("");
        onClose();
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [open, onClose]);

  if (!open) return null;

  const close = () => {
    setQuery("");
    onClose();
  };

  const go = (path: string) => {
    navigate(path);
    close();
  };

  return (
    <div className="palette-backdrop" role="presentation" onMouseDown={close}>
      <section
        className="command-palette"
        role="dialog"
        aria-modal="true"
        aria-label="Command palette"
        onMouseDown={event => event.stopPropagation()}
      >
        <div className="palette-input-row">
          <Search size={19} aria-hidden="true" />
          <input
            ref={inputRef}
            value={query}
            onChange={event => setQuery(event.target.value)}
            placeholder="Search pages, capabilities, and actions..."
          />
          <button className="icon-button" onClick={close} aria-label="Close command palette">
            <X size={17} />
          </button>
        </div>
        <div className="palette-results">
          {results.length === 0 ? (
            <div className="palette-empty">No Studio commands match “{query}”.</div>
          ) : (
            results.map((item, index) => {
              const Icon = item.icon;
              return (
                <button
                  key={`${item.group}-${item.label}-${index}`}
                  className="palette-result"
                  onClick={() => go(item.path)}
                >
                  <span className="palette-result-icon">
                    <Icon size={17} />
                  </span>
                  <span>
                    <strong>{item.label}</strong>
                    <small>{item.description}</small>
                  </span>
                  <em>{item.group}</em>
                </button>
              );
            })
          )}
        </div>
        <div className="palette-footer">
          <span><kbd>↵</kbd> open</span>
          <span><kbd>esc</kbd> close</span>
        </div>
      </section>
    </div>
  );
}
