using System.Text;

namespace CodeForCoders.Commerce.Domain.Entities;

public static class CourtesyTitleSearch
{
    private const string Accented = "áàâãäéèêëíìîïóòôõöúùûüç";
    private const string Plain = "aaaaaeeeeiiiiooooouuuuc";

    public static string Normalize(string title)
    {
        var result = new StringBuilder(title.Length);
        foreach (var character in title.ToLowerInvariant())
        {
            var index = Accented.IndexOf(character);
            result.Append(index < 0 ? character : Plain[index]);
        }
        return result.ToString();
    }
}
