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
