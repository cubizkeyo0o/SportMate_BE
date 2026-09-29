namespace SportMate.Application.Common;

internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> errors = [];

    public bool HasErrors => errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }

    public void ThrowIfAny()
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
