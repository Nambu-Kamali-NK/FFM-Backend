using System.Text.RegularExpressions;

namespace FFM.Notification.Application.Services;

public interface ITemplateInterpolationEngine
{
    string Interpolate(string template, IDictionary<string, string> placeholders);
}

public class TemplateInterpolationEngine : ITemplateInterpolationEngine
{
    // Matches {{VariableName}} with alphanumeric and underscore characters
    private static readonly Regex PlaceholderRegex = new(@"\{\{([a-zA-Z0-9_]+)\}\}", RegexOptions.Compiled);

    public string Interpolate(string template, IDictionary<string, string> placeholders)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        if (placeholders == null || placeholders.Count == 0)
        {
            // If no data provided, replace all placeholders with empty string gracefully (BR-TMPL-07)
            return PlaceholderRegex.Replace(template, string.Empty);
        }

        // Case-insensitive dictionary lookup
        var lookup = new Dictionary<string, string>(placeholders, StringComparer.OrdinalIgnoreCase);

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (lookup.TryGetValue(key, out var val))
            {
                return val ?? string.Empty;
            }

            // Fallback for missing keys
            return string.Empty;
        });
    }
}

