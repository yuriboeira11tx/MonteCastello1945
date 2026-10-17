"""Client for the installed Blender Lab MCP bridge (localhost only)."""
import argparse
import json
from pathlib import Path
import socket


def execute(code, timeout=300):
    request = {"type": "execute", "code": code, "strict_json": True}
    with socket.create_connection(("127.0.0.1", 9876), timeout=10) as client:
        client.settimeout(timeout)
        client.sendall(json.dumps(request).encode("utf-8") + b"\0")
        chunks = bytearray()
        while b"\0" not in chunks:
            part = client.recv(65536)
            if not part:
                break
            chunks.extend(part)
    response = json.loads(chunks.split(b"\0", 1)[0])
    return response


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--script")
    parser.add_argument("--code")
    parser.add_argument("--timeout", type=int, default=300)
    args = parser.parse_args()
    code = Path(args.script).read_text(encoding="utf-8") if args.script else args.code
    response = execute(code, args.timeout)
    print(json.dumps(response, ensure_ascii=True, indent=2))
    if response.get("status") != "ok":
        raise SystemExit(1)
