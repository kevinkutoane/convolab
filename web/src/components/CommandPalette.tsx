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
  const [activeIndex, setActiveIndex] = useState(0);
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
        setActiveIndex(0);
        onClose();
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [open, onClose]);

  if (!open) return null;

  const close = () => {
    setQuery("");
    setActiveIndex(0);
    onClose();
  };

  const go = (path: string) => {
    navigate(path);
    close();
  };

  const onSearchKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setActiveIndex(index => Math.min(index + 1, Math.max(0, results.length - 1)));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActiveIndex(index => Math.max(0, index - 1));
    } else if (event.key === "Enter" && results[activeIndex]) {
      event.preventDefault();
      go(results[activeIndex].path);
    }
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
            onChange={event => { setQuery(event.target.value); setActiveIndex(0); }}
            onKeyDown={onSearchKeyDown}
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
                  className={`palette-result${index === activeIndex ? " palette-result-active" : ""}`}
                  onClick={() => go(item.path)}
                  onMouseEnter={() => setActiveIndex(index)}
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
