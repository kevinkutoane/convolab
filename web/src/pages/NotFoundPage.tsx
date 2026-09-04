import { useHelp } from "../contexts/HelpContext";
import { ArrowLeft, SearchX } from "lucide-react";
import { useNavigate } from "react-router";

export function NotFoundPage() {
  useHelp({
    title: "Page Not Found",
    description: "The URL you navigated to does not exist in ConvoLab Studio.",
    usageSteps: [
          "Click 'Return to Dashboard' to go back to the main platform hub.",
          "Use the Command Palette (Ctrl+K / Cmd+K) to search for the feature you were looking for.",
          "Check the URL for typos — Studio paths use lowercase with hyphens (e.g., /conversations, /knowledge)."
    ],
    examples: [
          "If you typed '/prompt-studio', the correct path is '/prompts'.",
          "If you followed a broken link, report it to your workspace Administrator."
    ],
    expectedOutput: "Navigation back to a valid area of the Studio.",
    aiLayerRole: "N/A — this page is shown when a route cannot be matched."
  });

  const navigate = useNavigate();
  return (
    <div className="not-found-page">
      <div className="not-found-icon"><SearchX size={32} /></div>
      <span>404</span>
      <h2>Studio page not found</h2>
      <p>The requested workspace does not exist in the current capability map.</p>
      <button className="primary-button" onClick={() => navigate("/")}>
        <ArrowLeft size={16} /> Return to dashboard
      </button>
    </div>
  );
}
