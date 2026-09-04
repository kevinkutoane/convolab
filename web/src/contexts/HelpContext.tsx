import { createContext, useContext, useState, useEffect, type ReactNode } from "react";

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

export function useHelp(content: HelpContent) {
  const { setHelpContent } = useHelpContext();

  useEffect(() => {
    setHelpContent(content);
    return () => setHelpContent(null);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [setHelpContent, content.title]); 
}
