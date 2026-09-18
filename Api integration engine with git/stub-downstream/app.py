"""
Minimal stub downstream service.

Used only for local `docker compose up` and CI integration testing — it
stands in for a "real" downstream REST endpoint the engine bridges
payloads to. Supports an optional injected delay (via the
`X-Inject-Delay-Ms` header or `?delay_ms=` query param) so tests can
exercise the engine's latency-budget enforcement without needing a real
slow service.
"""

import time

from flask import Flask, jsonify, request

app = Flask(__name__)


@app.route("/ingest", methods=["POST"])
def ingest():
    delay_ms = request.headers.get("X-Inject-Delay-Ms") or request.args.get("delay_ms")
    if delay_ms:
        try:
            time.sleep(int(delay_ms) / 1000.0)
        except ValueError:
            pass

    body = request.get_json(silent=True) or {}
    return jsonify({"received": True, "echo": body}), 200


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "healthy"}), 200


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=9090)
