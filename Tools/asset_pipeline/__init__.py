"""Morza Bogow asset pipeline.

Flow: brief -> concept -> 3D model -> geometry/UV -> PBR textures -> rigging -> animation
-> HDRP materials -> Unity prefab -> validation. Jobs live in a SQLite queue so a worker can
stop at any point and resume without resubmitting paid provider tasks.
"""

__version__ = "0.1.0"
