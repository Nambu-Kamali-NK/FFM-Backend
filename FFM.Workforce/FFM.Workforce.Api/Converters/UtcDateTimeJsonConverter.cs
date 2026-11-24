using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFM.Workforce.Api.Converters
{
    public class UtcDateTimeJsonConverter : JsonConverter<DateTime>
    {
        private const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                    {
                        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    }
                }
            }
            return reader.GetDateTime().ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            writer.WriteStringValue(utc.ToString(Iso8601Format, CultureInfo.InvariantCulture));
        }
    }

    public class NullableUtcDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        private const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    return null;
                }

                if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                {
                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
            }

            return reader.GetDateTime().ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            var utc = value.Value.Kind == DateTimeKind.Utc ? value.Value : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
            writer.WriteStringValue(utc.ToString(Iso8601Format, CultureInfo.InvariantCulture));
        }
    }
}
