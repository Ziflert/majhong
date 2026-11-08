#!/usr/bin/env python3
"""Serve the project documents as readable text over HTTP.

The handoff file (docs/session-context.md) and the rest of docs/ are read in a browser or
fetched by another agent. `http.server` sends markdown as `text/markdown`, which Chrome
downloads instead of showing, so this server answers with `text/plain; charset=utf-8`
and strips the markdown it can do without (headings, emphasis, code fences).

    python Tools/context-server.py            # http://127.0.0.1:8517/
    python Tools/context-server.py --port 8601 --no-strip

Port 8517 is registered in ~/.pi/agent/PORTS.md for this project; pass --port for a second
instance instead of sharing it.
"""
import argparse
import os
import re
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DOCS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "docs")


def plain(markdown: str) -> str:
    """Markdown as plain text: headings lose their hashes, emphasis its stars."""
    text = re.sub(r"^```.*$", "", markdown, flags=re.MULTILINE)
    text = re.sub(r"^#{1,6}\s*", "", text, flags=re.MULTILINE)
    text = re.sub(r"\*\*(.+?)\*\*", r"\1", text)
    text = re.sub(r"`([^`]*)`", r"\1", text)
    text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)
    return text


class Handler(BaseHTTPRequestHandler):
    server_version = "MahjongDocs/1.0"
    strip_markdown = True

    def do_GET(self):  # noqa: N802 - http.server names it this way
        name = self.path.split("?")[0].lstrip("/") or "session-context.md"
        if name == "favicon.ico":
            self.send_error(404)
            return
        if "/" in name or "\\" in name or name.startswith("."):
            self.send_error(403, "only files of docs/ are served")
            return

        path = os.path.join(DOCS, name)
        if not os.path.isfile(path):
            self.send_error(404, f"no document called {name}")
            return

        with open(path, encoding="utf-8") as document:
            text = document.read()
        if Handler.strip_markdown:
            text = plain(text)

        body = text.encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def do_HEAD(self):  # noqa: N802
        self.do_GET()

    def log_message(self, fmt, *args):
        print(f"{self.address_string()} {fmt % args}", flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="Serve docs/ as plain text (port 8517 by default).")
    parser.add_argument("--port", type=int, default=8517)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--no-strip", action="store_true", help="serve the markdown as it is")
    args = parser.parse_args()

    Handler.strip_markdown = not args.no_strip
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(f"docs on http://{args.host}:{args.port}/session-context.md (Ctrl+C to stop)", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("stopped", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
