"""Small HTTP client on the standard library.

It never logs headers. Error texts pass through `redact` so a key echoed by a server or a signed
link does not reach the job log. Transport errors say whether the request may have reached the
server (`sent`), which decides if a paid create may be retried or must be reconciled first.
"""
import email.utils
import http.client
import json
import pathlib
import re
import socket
import ssl
import time
import urllib.error
import urllib.parse
import urllib.request

_SECRET = re.compile(r"(msy_[A-Za-z0-9_\-]+|Bearer\s+[A-Za-z0-9._\-]+|(?:api[_-]?key|token|secret|password)\s*[=:]\s*\S+)", re.IGNORECASE)
_SIGNED = re.compile(r"([?&](?:X-Amz-[A-Za-z]+|Signature|Expires|Key-Pair-Id|Policy|token)=)[^&\s\"']+", re.IGNORECASE)


def redact(text) -> str:
    text = _SECRET.sub("[ukryte]", str(text))
    return _SIGNED.sub(r"\1[ukryte]", text)


def strip_query(url: str) -> str:
    """A link without its query, for logs: signed download links carry tokens there."""
    parts = urllib.parse.urlsplit(url)
    return urllib.parse.urlunsplit((parts.scheme, parts.netloc, parts.path, "", ""))


class TransportError(Exception):
    def __init__(self, message: str, sent: bool):
        super().__init__(redact(message))
        self.sent = sent


class Response:
    def __init__(self, status: int, headers: dict, body: bytes):
        self.status = status
        self.headers = {k.lower(): v for k, v in headers.items()}
        self.body = body

    def json(self):
        if not self.body:
            return None
        return json.loads(self.body.decode("utf-8"))

    def text(self, limit=500) -> str:
        return redact(self.body[:limit].decode("utf-8", "replace"))

    @property
    def ok(self) -> bool:
        return 200 <= self.status < 300


def parse_retry_after(value, now=None):
    """Seconds to wait from a Retry-After header (delta seconds or an HTTP date)."""
    if value is None:
        return None
    value = str(value).strip()
    if value.isdigit():
        return float(value)
    try:
        when = email.utils.parsedate_to_datetime(value)
    except (TypeError, ValueError):
        return None
    if when is None:
        return None
    return max(0.0, when.timestamp() - (now if now is not None else time.time()))


def _not_sent(reason) -> bool:
    return isinstance(reason, (ConnectionRefusedError, socket.gaierror, ssl.SSLCertVerificationError))


def request(method: str, url: str, *, headers=None, json_body=None, data=None, timeout=60) -> Response:
    headers = dict(headers or {})
    if json_body is not None:
        data = json.dumps(json_body).encode("utf-8")
        headers.setdefault("Content-Type", "application/json")
    headers.setdefault("User-Agent", "morza-bogow-asset-pipeline/0.1")
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return Response(resp.status, dict(resp.headers), resp.read())
    except urllib.error.HTTPError as error:
        try:
            body = error.read() if hasattr(error, "read") else b""
        finally:
            error.close()
        return Response(error.code, dict(error.headers or {}), body or b"")
    except urllib.error.URLError as error:
        raise TransportError(f"{method} {strip_query(url)}: {error.reason}", sent=not _not_sent(error.reason)) from None
    except (socket.timeout, TimeoutError, http.client.HTTPException, ConnectionError, OSError) as error:
        raise TransportError(f"{method} {strip_query(url)}: {error.__class__.__name__}: {error}", sent=not _not_sent(error)) from None


def download(url: str, dest, *, timeout=120, max_bytes=200 * 1024 * 1024, headers=None) -> int:
    """Stream a file to dest via a .part file; never leaves a half file under the final name."""
    dest = pathlib.Path(dest)
    dest.parent.mkdir(parents=True, exist_ok=True)
    part = dest.with_name(dest.name + ".part")
    req = urllib.request.Request(url, headers=dict(headers or {"User-Agent": "morza-bogow-asset-pipeline/0.1"}))
    total = 0
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp, open(part, "wb") as out:
            length = resp.headers.get("Content-Length")
            if length and int(length) > max_bytes:
                raise TransportError(f"Plik {strip_query(url)} ma {length} B, limit {max_bytes} B.", sent=True)
            while True:
                chunk = resp.read(1 << 16)
                if not chunk:
                    break
                total += len(chunk)
                if total > max_bytes:
                    raise TransportError(f"Plik {strip_query(url)} przekracza limit {max_bytes} B.", sent=True)
                out.write(chunk)
    except urllib.error.HTTPError as error:
        part.unlink(missing_ok=True)
        raise TransportError(f"GET {strip_query(url)}: HTTP {error.code}", sent=True) from None
    except urllib.error.URLError as error:
        part.unlink(missing_ok=True)
        raise TransportError(f"GET {strip_query(url)}: {error.reason}", sent=not _not_sent(error.reason)) from None
    except BaseException:
        part.unlink(missing_ok=True)
        raise
    part.replace(dest)
    return total
