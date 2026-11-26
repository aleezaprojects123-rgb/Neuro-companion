"""
Dataset Loader for Social Robot Cognitive Training Collection (data&prompts.zip).
Extracts game run telemetry (touch vs vocal, errors, reaction times) and prompt templates.
"""

import os
import io
import zipfile
from typing import Dict, Any, Tuple
import pandas as pd
from .base_loader import BaseDatasetLoader


class RobotCognitiveLoader(BaseDatasetLoader):
    """Encapsulates extraction of robot cognitive game telemetry from data&prompts.zip."""

    def __init__(self, zip_path: str):
        super().__init__(zip_path)
        self.session_played_df: pd.DataFrame = pd.DataFrame()
        self.game_session_df: pd.DataFrame = pd.DataFrame()
        self.user_data_df: pd.DataFrame = pd.DataFrame()
        self.prompts_dict: Dict[str, str] = {}

    def load(self) -> Dict[str, Any]:
        if not os.path.exists(self.dataset_path):
            raise FileNotFoundError(f"Robot archive not found: {self.dataset_path}")

        with zipfile.ZipFile(self.dataset_path, "r") as z:
            # Load session_played.xlsx
            if "data/database/session_played.xlsx" in z.namelist():
                sp_bytes = z.read("data/database/session_played.xlsx")
                self.session_played_df = pd.read_excel(io.BytesIO(sp_bytes))

            # Load game_session.xlsx
            if "data/database/game_session.xlsx" in z.namelist():
                gs_bytes = z.read("data/database/game_session.xlsx")
                self.game_session_df = pd.read_excel(io.BytesIO(gs_bytes))

            # Load User_data.xlsx
            if "data/User_data.xlsx" in z.namelist():
                ud_bytes = z.read("data/User_data.xlsx")
                raw_u = pd.read_excel(io.BytesIO(ud_bytes))
                self.user_data_df = raw_u[raw_u["ID"].notna() & raw_u["ID"].apply(lambda x: isinstance(x, (int, float)))].copy()

            # Read prompts
            for name in z.namelist():
                if name.startswith("prompt/") and name.endswith(".txt"):
                    basename = os.path.basename(name)
                    self.prompts_dict[basename] = z.read(name).decode("utf-8", errors="ignore")

        self._is_validated, msg = self.validate()
        if not self._is_validated:
            raise ValueError(f"Robot loader validation failed: {msg}")

        return {
            "session_played": self.session_played_df,
            "game_session": self.game_session_df,
            "user_data": self.user_data_df,
            "prompts": self.prompts_dict
        }

    def validate(self) -> Tuple[bool, str]:
        if self.session_played_df.empty:
            return False, "session_played dataframe is empty."

        expected_sp = ["ID_USER", "type", "numErrors", "gameTime", "gameReactTime", "touch_interaction", "vocal_interaction"]
        for c in expected_sp:
            if c not in self.session_played_df.columns:
                return False, f"Missing expected column '{c}' in session_played.xlsx"

        if len(self.session_played_df) != 437:
            return False, f"Expected 437 game runs, got {len(self.session_played_df)}"

        return True, "Robot cognitive training telemetry verified."

    def get_summary(self) -> Dict[str, Any]:
        return {
            "total_game_runs": len(self.session_played_df),
            "game_types": self.session_played_df["type"].value_counts().to_dict() if not self.session_played_df.empty else {},
            "total_prompts": len(self.prompts_dict),
            "mean_react_time_s": float(self.session_played_df["gameReactTime"].mean()) if not self.session_played_df.empty else None,
            "total_touch_interactions": int(self.session_played_df["touch_interaction"].sum()) if not self.session_played_df.empty else 0,
            "total_vocal_interactions": int(self.session_played_df["vocal_interaction"].sum()) if not self.session_played_df.empty else 0
        }
