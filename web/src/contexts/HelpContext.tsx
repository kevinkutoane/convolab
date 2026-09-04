import { createContext, useContext, useState, useEffect, useRef, type ReactNode } from "react";

export interface HelpContent {
  title: string;
  description: ReactNode;
  usageSteps: ReactNode[];
  examples: ReactNode[];
  expectedOutput: ReactNode;
  aiLayerRole: ReactNode;
}

interface HelpContextType {
  helpContent: HelpContent | null;
  setHelpContent: (content: HelpContent | null) => void;
  isHelpOpen: boolean;
  setIsHelpOpen: (isOpen: boolean) => void;
  toggleHelp: () => void;
}

const HelpContext = createContext<HelpContextType | undefined>(undefined);

export function HelpProvider({ children }: { children: ReactNode }) {
  const [helpContent, setHelpContent] = useState<HelpContent | null>(null);
  const [isHelpOpen, setIsHelpOpen] = useState(false);

  const toggleHelp = () => setIsHelpOpen((prev) => !prev);

  return (
    <HelpContext.Provider
      value={{
        helpContent,
        setHelpContent,
        isHelpOpen,
        setIsHelpOpen,
        toggleHelp,
      }}
    >
      {children}
    </HelpContext.Provider>
  );
}

export function useHelpContext() {
  const context = useContext(HelpContext);
  if (context === undefined) {
    throw new Error("useHelpContext must be used within a HelpProvider");
  }
  return context;
}

/**
 * useHelp — registers screen-specific help content into the global drawer.
 * Uses a ref to carry the latest content object into the effect without
 * re-registering on every render, avoiding stale-closure issues when
 * description or steps change while the title stays the same.
 */
export function useHelp(content: HelpContent) {
  const { setHelpContent } = useHelpContext();
  const contentRef = useRef(content);
  contentRef.current = content;

  useEffect(() => {
    setHelpContent(contentRef.current);
    return () => setHelpContent(null);
  // Intentionally runs only on mount/unmount and when the page (title) changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [setHelpContent, content.title]);
}
