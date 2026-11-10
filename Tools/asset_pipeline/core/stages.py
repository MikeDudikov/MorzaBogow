"""Job states. Active states are stages a worker runs; blocked states wait for a person or `resume`."""

PIPELINE = ("queued", "generating", "preparing_geometry", "texturing", "rigging", "animating", "importing", "validating")
DONE = ("ready",)
BLOCKED = ("needs_configuration", "needs_review", "failed")
STATES = PIPELINE + DONE + BLOCKED
ACTIVE = PIPELINE

LABEL = {
    "queued": "w kolejce",
    "generating": "generowanie (koncept i model 3D)",
    "preparing_geometry": "przygotowanie geometrii, UV i LOD",
    "texturing": "tekstury PBR",
    "rigging": "szkielet",
    "animating": "animacje",
    "importing": "import do Unity, materiały HDRP, prefab",
    "validating": "walidacja",
    "ready": "gotowe",
    "needs_configuration": "brak konfiguracji",
    "needs_review": "do przeglądu",
    "failed": "błąd",
}

# Stages a person may start by hand (CLI prepare/texture/rig/import/validate).
MANUAL = {"prepare": "preparing_geometry", "texture": "texturing", "rig": "rigging", "animate": "animating", "import": "importing", "validate": "validating"}


def next_stage(state: str) -> str:
    index = PIPELINE.index(state)
    return PIPELINE[index + 1] if index + 1 < len(PIPELINE) else "ready"
