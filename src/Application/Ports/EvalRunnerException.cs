namespace Application.Ports;

/// <summary>
/// Thrown by the <see cref="IEvaluationRunner"/> adapter when the eval-runner returns a non-success
/// response — most importantly a request for a model whose provider has no configured credentials
/// (eval-runner replies <c>400 {"detail": "…not configured…"}</c>). The adapter surfaces that detail
/// here so a failed run fails <em>loudly</em> with the reason (B1/B2) rather than bubbling up as a
/// bare <see cref="System.Net.Http.HttpRequestException"/> → opaque 500.
/// </summary>
public sealed class EvalRunnerException(string message) : Exception(message);
