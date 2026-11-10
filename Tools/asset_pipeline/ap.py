#!/usr/bin/env python3
"""Entry point that works from any directory: python3 Tools/asset_pipeline/ap.py <command>."""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from asset_pipeline.cli.main import main  # noqa: E402

raise SystemExit(main())
