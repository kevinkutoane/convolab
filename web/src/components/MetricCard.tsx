import type { LucideIcon } from "lucide-react";

interface MetricCardProps {
  label: string;
  value: string;
  detail: string;
  icon: LucideIcon;
  tone?: "default" | "positive" | "accent" | "warning";
}

export function MetricCard({
  label,
  value,
  detail,
  icon: Icon,
  tone = "default",
}: MetricCardProps) {
  return (
    <article className={`metric-card metric-${tone}`}>
      <div className="metric-icon" aria-hidden="true">
        <Icon size={18} strokeWidth={1.8} />
      </div>
      <div className="metric-content">
        <span className="metric-label">{label}</span>
        <strong className="metric-value">{value}</strong>
        <span className="metric-detail">{detail}</span>
      </div>
    </article>
  );
}
