namespace Domain;

/// <summary>
/// A single execution of a prompt through the evaluation path. In the skeleton the output
/// is a trivial echo; later specs attach the prompt version, dataset, and scores.
/// </summary>
public sealed class EvalRun
{
    public Guid Id { get; private set; }
    public string Prompt { get; private set; }
    public string Output { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private EvalRun(Guid id, string prompt, string output, DateTimeOffset createdAt)
    {
        Id = id;
        Prompt = prompt;
        Output = output;
        CreatedAt = createdAt;
    }

    // Required by EF Core materialization; not for application use.
    private EvalRun()
    {
        Prompt = string.Empty;
        Output = string.Empty;
    }

    public static EvalRun Create(string prompt, string output, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be blank.", nameof(prompt));
        ArgumentNullException.ThrowIfNull(output);

        return new EvalRun(Guid.NewGuid(), prompt, output, createdAt);
    }
}
