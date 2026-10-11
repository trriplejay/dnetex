namespace SoftwareFactorySurvivors.Core;

/// <summary>Typing up to three initials (A-Z) for a high score.</summary>
public sealed class InitialsEntry
{
    public const int MaxLength = 3;

    public string Text { get; private set; } = "";

    public bool CanSubmit => Text.Length > 0;

    /// <summary>Adds a letter (lower case is upper-cased); anything else, or a fourth letter, is ignored.</summary>
    public void Type(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (letter is >= 'A' and <= 'Z' && Text.Length < MaxLength)
            Text += letter;
    }

    public void Backspace()
    {
        if (Text.Length > 0)
            Text = Text[..^1];
    }

    public static bool IsValid(string initials) =>
        initials.Length is > 0 and <= MaxLength && initials.All(c => c is >= 'A' and <= 'Z');
}
