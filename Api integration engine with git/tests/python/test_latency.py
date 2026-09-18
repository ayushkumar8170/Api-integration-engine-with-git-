"""
Latency-budget integration tests.

Uses the stub downstream's X-Inject-Delay-Ms support (see
stub-downstream/app.py) to deterministically trigger the engine's
latency-budget enforcement, rather than relying on flaky real-world
timing. Requires the stack to be running via `docker compose up` so the
injected-delay header actually reaches the stub.
"""

import time

import requests


def test_fast_downstream_response_is_well_under_budget(base_url, valid_payload, auth_headers):
    start = time.monotonic()
    resp = requests.post(f"{base_url}/api/payloads/ingest", json=valid_payload, headers=auth_headers, timeout=2)
    elapsed_ms = (time.monotonic() - start) * 1000

    assert resp.status_code == 200
    assert elapsed_ms < 130, f"Request took {elapsed_ms:.1f}ms, exceeding the 130ms latency budget"


def test_downstream_exceeding_budget_returns_504(base_url, valid_payload, auth_headers):
    """
    Ask the stub downstream to sleep for 300ms — well past the
    engine's configured per-target timeout (100ms) — and confirm the
    engine fails fast with a 504 rather than hanging or returning a
    false success.
    """
    headers = {**auth_headers, "X-Inject-Delay-Ms": "300"}
    resp = requests.post(f"{base_url}/api/payloads/ingest", json=valid_payload, headers=headers, timeout=2)

    assert resp.status_code == 504
    body = resp.json()
    assert body["error"] == "latency_budget_exceeded"


def test_engine_fails_fast_not_after_full_downstream_delay(base_url, valid_payload, auth_headers):
    """The engine should return well before the injected delay elapses, proving it enforces its own timeout."""
    headers = {**auth_headers, "X-Inject-Delay-Ms": "1000"}
    start = time.monotonic()
    requests.post(f"{base_url}/api/payloads/ingest", json=valid_payload, headers=headers, timeout=2)
    elapsed_ms = (time.monotonic() - start) * 1000

    assert elapsed_ms < 500, (
        f"Engine took {elapsed_ms:.1f}ms to respond to a 1000ms-delayed downstream — "
        "it should have timed out its own call well before that."
    )
