"""Providers by name, and the capability matrix the CLI, MCP server and docs report."""


def registry(config) -> dict:
    from ..blender import Blender
    from ..unity import Unity
    from .blockout import Blockout
    from .comfyui import ComfyUI
    from .manual import Manual
    from .meshy import Meshy
    return {
        "meshy": Meshy(config),
        "comfyui": ComfyUI(config),
        "blockout": Blockout(config),
        "blender": Blender(config),
        "unity": Unity(config),
        "substance": Manual(config, "substance", "Substance 3D Painter", ["texturing"],
                            "Painter ma API Pythona tylko wewnątrz aplikacji; brak zweryfikowanej automatyzacji."),
        "deepmotion": Manual(config, "deepmotion", "DeepMotion / SayMotion", ["animating"],
                             "dokumentacja API niedostępna (DNS); klipy z panelu webowego."),
        "cascadeur": Manual(config, "cascadeur", "Cascadeur", ["rigging", "animating"],
                            "skrypty działają w aplikacji; brak publicznego API do zdalnego sterowania."),
    }


def capabilities(config) -> list:
    return [provider.capability() for provider in registry(config).values()]
