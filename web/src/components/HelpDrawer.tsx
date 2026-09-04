import { X, HelpCircle, BookOpen, Lightbulb, PlaySquare, Bot } from "lucide-react";
import { useHelpContext } from "../contexts/HelpContext";
import "./HelpDrawer.css";

export function HelpDrawer() {
  const { isHelpOpen, setIsHelpOpen, toggleHelp, helpContent } = useHelpContext();

  return (
    <>
      <button 
        className={`help-fab ${isHelpOpen ? 'help-fab-open' : ''}`}
        onClick={toggleHelp}
        aria-label={isHelpOpen ? "Close help" : "Open Help & Documentation"}
        title={isHelpOpen ? "Close" : "Help & Documentation"}
      >
        {isHelpOpen ? <X size={26} /> : <HelpCircle size={28} />}
      </button>

      {isHelpOpen && (
        <>
          <div className="help-drawer-overlay" onClick={() => setIsHelpOpen(false)} aria-hidden="true" />
          <div className="help-drawer" role="dialog" aria-label="Help and Documentation">
            <div className="help-drawer-header">
              <div className="help-drawer-title-group">
                <HelpCircle size={24} className="help-drawer-icon" />
                <h2>{helpContent?.title || "Documentation"}</h2>
              </div>
              <button className="icon-button" onClick={() => setIsHelpOpen(false)} aria-label="Close help">
                <X size={20} />
              </button>
            </div>
            <div className="help-drawer-content">
              {helpContent ? (
                <>
                  <section className="help-section">
                    <h3><BookOpen size={18} /> Purpose & Overview</h3>
                    <div className="help-text">{helpContent.description}</div>
                  </section>

                  {helpContent.usageSteps && helpContent.usageSteps.length > 0 && (
                    <section className="help-section">
                      <h3><PlaySquare size={18} /> Getting Started / Navigation</h3>
                      <ol className="help-steps">
                        {helpContent.usageSteps.map((step, index) => (
                          <li key={index}>{step}</li>
                        ))}
                      </ol>
                    </section>
                  )}

                  {helpContent.examples && helpContent.examples.length > 0 && (
                    <section className="help-section">
                      <h3><Lightbulb size={18} /> What You'll Find Here</h3>
                      <ul className="help-examples">
                        {helpContent.examples.map((example, index) => (
                          <li key={index}>{example}</li>
                        ))}
                      </ul>
                    </section>
                  )}

                  {helpContent.expectedOutput && (
                    <section className="help-section">
                      <h3>Expected Output / Actions</h3>
                      <div className="help-text help-output">{helpContent.expectedOutput}</div>
                    </section>
                  )}

                  {helpContent.aiLayerRole && (
                    <section className="help-section help-ai-section">
                      <h3><Bot size={18} /> AI Layer Context</h3>
                      <div className="help-text">{helpContent.aiLayerRole}</div>
                    </section>
                  )}
                </>
              ) : (
                <div className="help-empty-state">
                  <p>No specific documentation available for this screen.</p>
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </>
  );
}
