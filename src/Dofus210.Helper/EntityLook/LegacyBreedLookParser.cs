using System.Globalization;

namespace Dofus210.Helper.EntityLook;

public static class LegacyBreedLookParser
{
    public static BreedLookDescriptor Parse(string? look)
    {
        if (!TryParse(look, out var descriptor))
        {
            return new BreedLookDescriptor(1, 0, 100);
        }

        return descriptor;
    }

    public static bool TryParse(string? look, out BreedLookDescriptor descriptor)
    {
        descriptor = new BreedLookDescriptor(1, 0, 100);

        if (string.IsNullOrWhiteSpace(look))
        {
            return false;
        }

        var trimmed = look.Trim();

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            trimmed = trimmed[1..^1];
        }

        var segments = trimmed.Split('|');

        if (segments.Length == 0 || !TryReadInt(segments[0], out var bonesId))
        {
            return false;
        }

        var primarySkinId = 0;

        if (segments.Length > 1 && !string.IsNullOrWhiteSpace(segments[1]))
        {
            var firstSkin = segments[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstSkin) && TryReadInt(firstSkin, out var parsedSkinId))
            {
                primarySkinId = parsedSkinId;
            }
        }

        var scalePercent = 100;

        if (segments.Length > 3 && !string.IsNullOrWhiteSpace(segments[3]))
        {
            var firstScale = segments[3]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstScale) && TryReadInt(firstScale, out var parsedScale))
            {
                scalePercent = parsedScale;
            }
        }

        descriptor = new BreedLookDescriptor(
            bonesId > 0 ? bonesId : 1,
            primarySkinId,
            scalePercent > 0 ? scalePercent : 100);

        return true;
    }

    private static bool TryReadInt(string value, out int parsed)
    {
        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out parsed);
    }
}
