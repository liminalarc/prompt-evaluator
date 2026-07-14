"""Service-token auth tests (4.1, slice 6).

eval-runner is an internal trusted service: when EVAL_RUNNER_SERVICE_TOKEN is set, the work
endpoints require a matching X-Service-Token header; when unset, they stay open (back-compat).
The Anthropic client is stubbed (EVAL_RUNNER_STUB) so no live API calls are made.
"""

from fastapi.testclient import TestClient

from app.main import app

TOKEN = "s3cret-service-token"


def teardown_function():
    app.dependency_overrides.clear()


# --- env var SET: the header is required -------------------------------------------------

def test_missing_header_is_rejected_when_token_configured(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_SERVICE_TOKEN", TOKEN)
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    client = TestClient(app)

    resp = client.post("/echo", json={"prompt": "hi"})

    assert resp.status_code == 401


def test_wrong_header_is_rejected_when_token_configured(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_SERVICE_TOKEN", TOKEN)
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    client = TestClient(app)

    resp = client.post(
        "/echo", json={"prompt": "hi"}, headers={"X-Service-Token": "nope"}
    )

    assert resp.status_code == 401


def test_correct_header_is_accepted_when_token_configured(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_SERVICE_TOKEN", TOKEN)
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    client = TestClient(app)

    resp = client.post(
        "/echo", json={"prompt": "hi"}, headers={"X-Service-Token": TOKEN}
    )

    assert resp.status_code == 200
    assert resp.json() == {"output": "hi"}


def test_all_work_endpoints_are_gated(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_SERVICE_TOKEN", TOKEN)
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    client = TestClient(app)

    gated = {
        "/echo": {"prompt": "hi"},
        "/generate-fixtures": {
            "seed_examples": [{"input": "seed", "upstream_context": None, "expected_output": None}],
            "guidance": {"coverage_goals": None, "edge_cases": None, "constraints": None},
            "count": 1,
        },
        "/execute-prompt": {"prompt": "p", "model": "m", "input": "i", "upstream_context": None},
        "/judge": {"rubric": "r", "input": "i", "output": "o", "model": "claude-opus-4-8"},
    }
    for path, body in gated.items():
        assert client.post(path, json=body).status_code == 401, path
        ok = client.post(path, json=body, headers={"X-Service-Token": TOKEN})
        assert ok.status_code == 200, path


# --- health / version stay open for probes -----------------------------------------------

def test_probe_endpoints_stay_open_when_token_configured(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_SERVICE_TOKEN", TOKEN)
    client = TestClient(app)

    assert client.get("/health").status_code == 200
    assert client.get("/version").status_code == 200


# --- env var UNSET: back-compat, endpoints open ------------------------------------------

def test_endpoints_open_when_token_unset(monkeypatch):
    monkeypatch.delenv("EVAL_RUNNER_SERVICE_TOKEN", raising=False)
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    client = TestClient(app)

    resp = client.post("/echo", json={"prompt": "hi"})

    assert resp.status_code == 200
    assert resp.json() == {"output": "hi"}
