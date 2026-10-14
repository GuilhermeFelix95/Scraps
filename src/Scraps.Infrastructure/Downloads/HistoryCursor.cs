using System.Globalization;
using System.Text;

namespace Scraps.Infrastructure.Downloads;

// Cursor opaco da paginação: a posição (created_at, id) do último item lido. Fica em base64 para a UI não interpretá-lo.
internal static class HistoryCursor
{
    private const char Separator = '|';

    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var payload = $"{createdAt.ToString("O", CultureInfo.InvariantCulture)}{Separator}{id:D}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static (string CreatedAt, string Id) Decode(string cursor)
    {
        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Cursor de histórico inválido.", nameof(cursor), exception);
        }

        var parts = payload.Split(Separator);
        if (parts.Length != 2
            || !DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdAt)
            || !Guid.TryParse(parts[1], out var id))
        {
            throw new ArgumentException("Cursor de histórico inválido.", nameof(cursor));
        }

        return (createdAt.ToString("O", CultureInfo.InvariantCulture), id.ToString("D"));
    }
}
