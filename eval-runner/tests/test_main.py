from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_health_returns_ok() -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_echo_returns_the_prompt() -> None:
    response = client.post("/echo", json={"prompt": "round trip"})
    assert response.status_code == 200
    assert response.json() == {"output": "round trip"}


def test_echo_requires_prompt() -> None:
    response = client.post("/echo", json={})
    assert response.status_code == 422


def test_version_reports_service_and_commit() -> None:
    response = client.get("/version")
    assert response.status_code == 200
    body = response.json()
    assert body["service"] == "eval-runner"
    assert body["version"] == "0.3.0"
    assert body["commit"]  # "dev" locally, real SHA in a built image
