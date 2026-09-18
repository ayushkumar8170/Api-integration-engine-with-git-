"""
Payload validation integration tests.

Exercises the real POST /api/payloads/ingest endpoint end-to-end,
verifying that the JSON Schema contract is actually enforced by the
running service (not just unit-tested against the validator in
isolation).
"""

import copy

import requests


def test_valid_payload_is_accepted(base_url, valid_payload, auth_headers):
    resp = requests.post(f"{base_url}/api/payloads/ingest", json=valid_payload, headers=auth_headers, timeout=2)

    assert resp.status_code == 200
    body = resp.json()
    assert body["status"] == "bridged"
    assert body["targetId"] == "orders-service"


def test_missing_required_field_is_rejected(base_url, valid_payload, auth_headers):
    payload = copy.deepcopy(valid_payload)
    del payload["payload"]["amount"]

    resp = requests.post(f"{base_url}/api/payloads/ingest", json=payload, headers=auth_headers, timeout=2)

    assert resp.status_code == 400
    body = resp.json()
    assert body["error"] == "schema_validation_failed"
    assert len(body["details"]) > 0


def test_invalid_currency_format_is_rejected(base_url, valid_payload, auth_headers):
    payload = copy.deepcopy(valid_payload)
    payload["payload"]["currency"] = "usd"  # must be uppercase 3-letter ISO code

    resp = requests.post(f"{base_url}/api/payloads/ingest", json=payload, headers=auth_headers, timeout=2)

    assert resp.status_code == 400
    assert resp.json()["error"] == "schema_validation_failed"


def test_additional_properties_are_rejected(base_url, valid_payload, auth_headers):
    payload = copy.deepcopy(valid_payload)
    payload["payload"]["unexpectedField"] = "should not be allowed"

    resp = requests.post(f"{base_url}/api/payloads/ingest", json=payload, headers=auth_headers, timeout=2)

    assert resp.status_code == 400


def test_unknown_bridge_target_returns_404(base_url, valid_payload, auth_headers):
    payload = copy.deepcopy(valid_payload)
    payload["targetId"] = "does-not-exist"

    resp = requests.post(f"{base_url}/api/payloads/ingest", json=payload, headers=auth_headers, timeout=2)

    assert resp.status_code == 404
    assert resp.json()["error"] == "unknown_bridge_target"
