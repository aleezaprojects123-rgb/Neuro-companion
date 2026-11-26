r"""
Central documented entry point for running and checking the Phase 4.3 Unified Data Preparation pipeline.

Usage:
    cd D:\NeuroCompanion\Backend
    venv\Scripts\python.exe run_phase4_3_preparation.py
"""

import sys
from pathlib import Path

# Add Backend root to path
backend_dir = str(Path(__file__).resolve().parent)
if backend_dir not in sys.path:
    sys.path.insert(0, backend_dir)

from ml.data.preparation.pipeline import run_unified_data_preparation

if __name__ == "__main__":
    df_manifest = run_unified_data_preparation()
    print("\nSummary of Prepared Datasets:")
    for idx, row in df_manifest.iterrows():
        print(f"[{row['dataset_id']}] {row['dataset_name']} ({row['row_count']:,} rows, {row['column_count']} cols) - Status: {row['preparation_status']}")
