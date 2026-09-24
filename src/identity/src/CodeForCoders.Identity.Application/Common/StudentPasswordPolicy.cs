namespace CodeForCoders.Identity.Application.Common;

public static class StudentPasswordPolicy
{
    public static bool IsSatisfiedBy(string password)
        => password.Length >= 8
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(character => !char.IsWhiteSpace(character)
                && (char.IsSymbol(character) || char.IsPunctuation(character)));
}
