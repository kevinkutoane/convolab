import { useHelp } from "../contexts/HelpContext";
import { ArrowLeft, SearchX } from "lucide-react";
import { useNavigate } from "react-router";

export function NotFoundPage() {
  useHelp({
    title: "Page Not Found",
    description: "You have navigated to a URL that does not exist in the Studio.",
    usageSteps: [
        "Click the 'Return Home' button to go back to the Dashboard.",
        "Use the Command Palette (Cmd+K) to search for the page you were looking for."
    ],
    examples: [
        "N/A"
    ],
    expectedOutput: "Navigation back to a valid platform area.",
    aiLayerRole: "N/A"
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
