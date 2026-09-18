"""
Shared fixtures for the Python integration test suite.

Tests run against a *running* instance of the engine (started via
`dotnet run` or `docker compose up`) — they are black-box HTTP tests,
not in-process tests. Point them at a different instance with the
ENGINE_BASE_URL environment variable.
"""

import os

import pytest
import requests

DEFAULT_BASE_URL = "http://localhost:8080"
DEFAULT_API_KEY = "local-dev-key"


@pytest.fixture(scope="session")
def base_url() -> str:
    return os.environ.get("ENGINE_BASE_URL", DEFAULT_BASE_URL)


@pytest.fixture(scope="session")
def api_key() -> str:
    """
    Must match the Auth:ApiKey configured on the running instance —
    appsettings.Development.json's "local-dev-key" for `dotnet run`, or
    ENGINE_API_KEY for docker compose (see docker-compose.yml).
    """
    return os.environ.get("ENGINE_API_KEY", DEFAULT_API_KEY)


@pytest.fixture
def auth_headers(api_key) -> dict:
    return {"X-Api-Key": api_key}


@pytest.fixture(scope="session", autouse=True)
def wait_for_service(base_url):
    """Fail fast with a clear message if the engine isn't reachable yet."""
    import time

    last_error = None
    for _ in range(20):
        try:
            resp = requests.get(f"{base_url}/health", timeout=1)
            if resp.status_code == 200:
                return
        except requests.RequestException as e:
            last_error = e
        time.sleep(0.5)

    pytest.exit(
        f"Integration engine not reachable at {base_url} (last error: {last_error}). "
        "Start it with `dotnet run` or `docker compose up` before running these tests."
    )


@pytest.fixture
def valid_payload() -> dict:
    return {
        "targetId": "orders-service",
        "schemaId": "payload.schema.json",
        "payload": {
            "orderId": "ORD-1029",
            "amount": 42.50,
            "currency": "USD",
        },
    }
