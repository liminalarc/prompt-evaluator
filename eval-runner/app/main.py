"""Prompt Evaluator eval-runner.

A thin, stateless FastAPI service the .NET Infrastructure layer calls over HTTP. In the
walking skeleton it only echoes the prompt; later specs add LLM-judge scoring and synthetic
fixture generation. It holds no domain authority and no persistence.
"""

import os

from fastapi import FastAPI
from pydantic import BaseModel

SERVICE_NAME = "eval-runner"
VERSION = "0.1.0"

app = FastAPI(title="Prompt Evaluator eval-runner", version=VERSION)


class EchoRequest(BaseModel):
    prompt: str


class EchoResponse(BaseModel):
    output: str


class VersionResponse(BaseModel):
    service: str
    version: str
    commit: str


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/version", response_model=VersionResponse)
def version() -> VersionResponse:
    # GIT_COMMIT is baked in at image build time; "dev" for local/per-process runs.
    return VersionResponse(
        service=SERVICE_NAME,
        version=VERSION,
        commit=os.environ.get("GIT_COMMIT", "dev"),
    )


@app.post("/echo", response_model=EchoResponse)
def echo(request: EchoRequest) -> EchoResponse:
    # Skeleton behaviour: prove the seam by echoing the prompt straight back.
    return EchoResponse(output=request.prompt)
