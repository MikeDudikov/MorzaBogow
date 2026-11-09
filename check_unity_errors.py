#!/usr/bin/env python3
"""Sprawdza ostatnią kompilację skryptów w logu edytora Unity.

Użycie:
    python check_unity_errors.py            # domyślny Editor.log dla systemu
    python check_unity_errors.py <log>      # inny log, np. z uruchomienia batch (-logFile)

Ścieżkę można też podać w zmiennej środowiskowej UNITY_EDITOR_LOG.
Kod wyjścia: 1 - znaleziono błędy, 0 - kompilacja czysta, 2 - brak pliku logu.
"""
import os
import re
import sys
from collections import deque
from pathlib import Path

TAIL_LINES = 1500
START_MARKERS = ("CompilationPipeline", "Scripts have compiler errors", "Reloading assemblies")
ERROR_PATTERN = re.compile(r"(error CS\d+:|Exception:|AssertionException|NullReferenceException)")


def default_log_path() -> Path:
    if sys.platform.startswith("win"):
        return Path(os.environ.get("LOCALAPPDATA", "")) / "Unity" / "Editor" / "Editor.log"
    if sys.platform == "darwin":
        return Path.home() / "Library" / "Logs" / "Unity" / "Editor.log"
    return Path.home() / ".config" / "unity3d" / "Editor.log"


def main() -> int:
    if len(sys.argv) > 1:
        log = Path(sys.argv[1])
    elif os.environ.get("UNITY_EDITOR_LOG"):
        log = Path(os.environ["UNITY_EDITOR_LOG"])
    else:
        log = default_log_path()

    if not log.is_file():
        print(f"Nie znaleziono logu Unity: {log}")
        return 2

    with open(log, encoding="utf-8", errors="ignore") as handle:
        lines = list(deque(handle, maxlen=TAIL_LINES))

    # Tylko od ostatniego rozpoczęcia kompilacji skryptów w dół.
    # "Scripts have compiler errors" Unity pisze PO liście błędów, więc gdy to ostatni znacznik,
    # początkiem tej kompilacji jest poprzedni znacznik innego rodzaju (albo początek analizowanego fragmentu).
    start = 0
    for index in range(len(lines) - 1, -1, -1):
        found = [marker for marker in START_MARKERS if marker in lines[index]]
        if not found:
            continue
        if found == ["Scripts have compiler errors"]:
            start = 0
            for earlier in range(index - 1, -1, -1):
                if any(marker in lines[earlier] for marker in START_MARKERS if marker != "Scripts have compiler errors"):
                    start = earlier
                    break
        else:
            start = index
        break

    errors = []
    seen = set()
    for line in lines[start:]:
        text = line.rstrip()
        if ERROR_PATTERN.search(text) and text not in seen:
            seen.add(text)
            errors.append(text)

    print(f"Log: {log} (ostatnie {len(lines)} linii, analiza od linii {start + 1})")
    if errors:
        print(f"Znaleziono błędy: {len(errors)}")
        for text in errors:
            print(text)
        return 1
    print("Kompilacja czysta: brak błędów.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
