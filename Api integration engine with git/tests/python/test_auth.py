"""
Authorization integration tests.

Confirms ApiKeyAuthMiddleware actually enforces the X-Api-Key header on
/api/* routes before any validation or bridging work happens, and that
the health check remains reachable without a key (needed for container
orchestrator liveness/readiness probes).
"""

import requests


def test_request_without_api_key_is_rejected(base_url, valid_payload):
    resp = requests.post(f"{base_url}/api/payloads/ingest", json=valid_payload, timeout=2)

    assert resp.status_code == 401
    assert resp.json()["error"] == "unauthorized"


def test_request_with_wrong_api_key_is_rejected(base_url, valid_payload):
    resp = requests.post(
        f"{base_url}/api/payloads/ingest",
        json=valid_payload,
        headers={"X-Api-Key": "definitely-not-the-right-key"},
        timeout=2,
    )

    assert resp.status_code == 401
    assert resp.json()["error"] == "unauthorized"


def test_request_with_valid_api_key_is_accepted(base_url, valid_payload, auth_headers):
    resp = requests.post(f"{base_url}/api/payloads/ingest", json=valid_payload, headers=auth_headers, timeout=2)

    assert resp.status_code == 200


def test_health_check_requires_no_api_key(base_url):
    resp = requests.get(f"{base_url}/api/payloads/health", timeout=2)

    assert resp.status_code == 200
    assert resp.json()["status"] == "healthy"


def test_root_health_check_requires_no_api_key(base_url):
    """The infra-level /health endpoint (ASP.NET health checks) is also exempt."""
    resp = requests.get(f"{base_url}/health", timeout=2)

    assert resp.status_code == 200
