/** A horizontal bar showing how much of a budget is used. Turns red when nothing is left. */
export function Meter({ used, total }: { used: number; total: number }) {
  const ratio = total > 0 ? Math.min(1, used / total) : 1;
  const state = ratio >= 1 ? "full" : ratio >= 0.8 ? "high" : "ok";
  return (
    <div
      className={`meter meter-${state}`}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={Math.round(total)}
      aria-valuenow={Math.round(Math.min(used, total))}
    >
      <div className="meter-fill" style={{ width: `${ratio * 100}%` }} />
    </div>
  );
}
