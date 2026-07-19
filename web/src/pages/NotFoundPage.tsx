import { ArrowLeft, SearchX } from "lucide-react";
import { useNavigate } from "react-router-dom";

export function NotFoundPage() {
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
