"""GET /providers (spec 1.13): the eval-runner is the authority on which providers have
configured credentials; .NET reflects it in the Model Catalog's availability."""

from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_reports_only_providers_with_configured_keys(monkeypatch) -> None:
    monkeypatch.delenv("EVAL_RUNNER_STUB", raising=False)
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-ant-test")
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)

    response = client.get("/providers")

    assert response.status_code == 200
    assert response.json() == {"providers": ["anthropic"]}


def test_reports_openai_when_its_key_is_present(monkeypatch) -> None:
    monkeypatch.delenv("EVAL_RUNNER_STUB", raising=False)
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-ant-test")
    monkeypatch.setenv("OPENAI_API_KEY", "sk-openai-test")

    response = client.get("/providers")

    assert response.status_code == 200
    assert set(response.json()["providers"]) == {"anthropic", "openai"}


def test_reports_no_providers_when_no_keys_configured(monkeypatch) -> None:
    monkeypatch.delenv("EVAL_RUNNER_STUB", raising=False)
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)

    response = client.get("/providers")

    assert response.status_code == 200
    assert response.json() == {"providers": []}


def test_stub_mode_reports_all_providers_available(monkeypatch) -> None:
    # Stub mode makes execution model-free; report every provider so dev/e2e (no keys set) doesn't
    # mark every catalog model unavailable.
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)

    response = client.get("/providers")

    assert response.status_code == 200
    assert set(response.json()["providers"]) == {"anthropic", "openai"}


def test_requires_the_service_token_when_configured(monkeypatch) -> None:
    monkeypatch.setenv("EVAL_RUNNER_SERVICE_TOKEN", "secret-token")

    unauthorized = client.get("/providers")
    assert unauthorized.status_code == 401

    authorized = client.get("/providers", headers={"X-Service-Token": "secret-token"})
    assert authorized.status_code == 200
