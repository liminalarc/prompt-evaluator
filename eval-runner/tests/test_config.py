"""Provider config/credentials wiring (AC3): a provider is registered only when its API key
is present in the environment, and requesting an unconfigured provider fails clearly."""

import pytest

from app.main import get_provider_registry
from app.providers import UnknownProviderError


def test_registry_serves_only_providers_with_configured_credentials(monkeypatch):
    monkeypatch.delenv("EVAL_RUNNER_STUB", raising=False)
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-ant-test")
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)

    registry = get_provider_registry()

    # The default (Claude) provider is available.
    assert registry.for_model("claude-opus-4-8").name == "anthropic"
    # OpenAI has no key configured -> requesting a gpt model fails clearly.
    with pytest.raises(UnknownProviderError):
        registry.for_model("gpt-4o")


def test_registry_adds_openai_when_its_key_is_present(monkeypatch):
    monkeypatch.delenv("EVAL_RUNNER_STUB", raising=False)
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-ant-test")
    monkeypatch.setenv("OPENAI_API_KEY", "sk-openai-test")

    registry = get_provider_registry()

    assert registry.for_model("gpt-4o").name == "openai"


def test_missing_default_provider_credentials_fail_clearly(monkeypatch):
    monkeypatch.delenv("EVAL_RUNNER_STUB", raising=False)
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)

    registry = get_provider_registry()

    with pytest.raises(UnknownProviderError):
        registry.for_model("claude-opus-4-8")


def test_stub_mode_builds_no_registry(monkeypatch):
    monkeypatch.setenv("EVAL_RUNNER_STUB", "1")
    assert get_provider_registry() is None
