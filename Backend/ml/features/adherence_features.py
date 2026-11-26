"""
Longitudinal Adherence & Compliance Feature Extractor.
Informed by Brain-IT 12-week longitudinal adherence dynamics.
"""

from typing import List, Dict, Any
import numpy as np


class AdherenceTrajectoryExtractor:
    """Extracts longitudinal compliance slope and adherence metrics."""

    @staticmethod
    def extract_adherence_metrics(weekly_attendance: List[int], planned_sessions_per_week: int = 3) -> Dict[str, Any]:
        """Calculates adherence rate, trend slope, and stability metrics."""
        if not weekly_attendance:
            return {
                "total_weeks": 0,
                "overall_adherence_pct": 0.0,
                "attendance_slope": 0.0,
                "adherence_risk_bracket": "unknown"
            }

        total_weeks = len(weekly_attendance)
        total_attended = sum(weekly_attendance)
        total_planned = total_weeks * planned_sessions_per_week
        overall_adherence_pct = (total_attended / total_planned) * 100.0 if total_planned > 0 else 0.0

        # Calculate slope across weeks (weeks 1 to N)
        if total_weeks >= 2:
            x = np.arange(total_weeks)
            y = np.array(weekly_attendance, dtype=float)
            slope = float(np.polyfit(x, y, 1)[0])
        else:
            slope = 0.0

        # Risk stratification (compliance, not clinical diagnosis)
        if overall_adherence_pct >= 80.0 and slope >= -0.1:
            risk = "high_adherence_stable"
        elif overall_adherence_pct >= 60.0:
            risk = "moderate_adherence_monitoring"
        else:
            risk = "low_adherence_dropout_risk"

        return {
            "total_weeks_recorded": total_weeks,
            "total_sessions_attended": int(total_attended),
            "total_sessions_planned": int(total_planned),
            "overall_adherence_pct": float(round(overall_adherence_pct, 1)),
            "attendance_slope": float(round(slope, 3)),
            "adherence_risk_bracket": risk
        }
