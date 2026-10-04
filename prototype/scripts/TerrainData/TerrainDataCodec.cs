using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Yudian.Terrain;

/// <summary>
/// terrain-patch-r1 冻结的解析/序列化/校验入口。所有非法输入（含 null/空参数、越界索引）
/// 统一抛 ArgumentException，消息带 JSON 字段路径或参数/索引名；拒绝时不产生半合法对象。
/// </summary>
public static class TerrainDataCodec
{
    private const int MaxJsonChars = 4194304;
    private const long MaxVersion = 9007199254740991;
    private const int MaxRowsOrColumns = 257;
    private const double MinSpacingM = 0.01;
    private const double MaxSpacingM = 100.0;
    private const double MinOriginM = -10000.0;
    private const double MaxOriginM = 10000.0;
    private const double MinHeightM = -1000.0;
    private const double MaxHeightM = 1000.0;
    private const string IdPattern = "[a-z][a-z0-9_-]{0,63}";

    public static TerrainSnapshot ParseSnapshot(string json)
    {
        using JsonDocument document = ParseStrict(json);
        return ReadSnapshot(document.RootElement, prefix: string.Empty);
    }

    public static TerrainPatch ParsePatch(string json)
    {
        using JsonDocument document = ParseStrict(json);
        return ReadPatch(document.RootElement);
    }

    public static string Serialize(TerrainSnapshot value)
    {
        if (value is null)
            throw new ArgumentException("value must not be null", nameof(value));
        var json = new StringBuilder(96 + value.HeightsM.Count * 12);
        AppendSnapshot(json, value);
        return json.ToString();
    }

    public static string Serialize(TerrainPatch value)
    {
        if (value is null)
            throw new ArgumentException("value must not be null", nameof(value));
        var json = new StringBuilder(64 + (value.Base.HeightsM.Count + value.HeightsM.Count) * 12);
        json.Append("{\"schema_version\":").Append(value.SchemaVersion);
        AppendIdField(json, "patch_id", value.PatchId);
        json.Append(",\"base\":");
        AppendSnapshot(json, value.Base);
        AppendHeights(json, value.HeightsM);
        json.Append('}');
        return json.ToString();
    }

    public static void ValidateAgainst(TerrainPatch patch, TerrainSnapshot current)
    {
        if (patch is null)
            throw new ArgumentException("patch must not be null", nameof(patch));
        if (current is null)
            throw new ArgumentException("current must not be null", nameof(current));

        TerrainSnapshot baseSnapshot = patch.Base;
        if (!string.Equals(baseSnapshot.RegionId, current.RegionId, StringComparison.Ordinal))
            throw new ArgumentException($"base.region_id mismatch: patch base '{baseSnapshot.RegionId}' vs current '{current.RegionId}'");
        if (baseSnapshot.Version != current.Version)
            throw new ArgumentException($"base.version mismatch: patch base {baseSnapshot.Version} vs current {current.Version}");
        if (baseSnapshot.Rows != current.Rows)
            throw new ArgumentException($"base.rows mismatch: patch base {baseSnapshot.Rows} vs current {current.Rows}");
        if (baseSnapshot.Columns != current.Columns)
            throw new ArgumentException($"base.columns mismatch: patch base {baseSnapshot.Columns} vs current {current.Columns}");
        if (baseSnapshot.OriginXM != current.OriginXM)
            throw new ArgumentException("base.origin_x_m mismatch vs current");
        if (baseSnapshot.OriginZM != current.OriginZM)
            throw new ArgumentException("base.origin_z_m mismatch vs current");
        if (baseSnapshot.SpacingM != current.SpacingM)
            throw new ArgumentException("base.spacing_m mismatch vs current");

        for (int index = 0; index < baseSnapshot.HeightsM.Count; index++)
        {
            if (baseSnapshot.HeightsM[index] != current.HeightsM[index])
                throw new ArgumentException($"base.heights_m[{index}] does not equal the current height at this index");
        }
    }

    private static void AppendSnapshot(StringBuilder json, TerrainSnapshot value)
    {
        json.Append("{\"schema_version\":").Append(value.SchemaVersion);
        AppendIdField(json, "region_id", value.RegionId);
        json.Append(",\"version\":").Append(value.Version);
        json.Append(",\"origin_x_m\":").Append(FormatNumber(value.OriginXM));
        json.Append(",\"origin_z_m\":").Append(FormatNumber(value.OriginZM));
        json.Append(",\"spacing_m\":").Append(FormatNumber(value.SpacingM));
        json.Append(",\"rows\":").Append(value.Rows);
        json.Append(",\"columns\":").Append(value.Columns);
        AppendHeights(json, value.HeightsM);
        json.Append('}');
    }

    private static void AppendHeights(StringBuilder json, IReadOnlyList<double> heights)
    {
        json.Append(",\"heights_m\":[");
        for (int index = 0; index < heights.Count; index++)
        {
            if (index > 0)
                json.Append(',');
            json.Append(FormatNumber(heights[index]));
        }
        json.Append(']');
    }

    private static void AppendIdField(StringBuilder json, string name, string id)
    {
        json.Append(",\"").Append(name).Append("\":\"");
        foreach (char character in id)
        {
            if (character == '"' || character == '\\')
                json.Append('\\').Append(character);
            else if (character < ' ')
                json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            else
                json.Append(character);
        }
        json.Append('"');
    }

    // 构造入口已保证有限值；InvariantCulture 最短往返表示本身是合法 JSON 数字（含 -0 与指数形式）。
    private static string FormatNumber(double number) => number.ToString(CultureInfo.InvariantCulture);

    private static JsonDocument ParseStrict(string json)
    {
        if (json is null)
            throw new ArgumentException("json must not be null", nameof(json));
        if (json.Length == 0)
            throw new ArgumentException("json must not be empty", nameof(json));
        if (json.Length > MaxJsonChars)
            throw new ArgumentException($"json is {json.Length} UTF-16 characters, over the {MaxJsonChars} limit", nameof(json));
        try
        {
            return JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"json is not valid strict JSON: {exception.Message}", nameof(json), exception);
        }
    }

    private static TerrainSnapshot ReadSnapshot(JsonElement element, string prefix)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"{prefix}snapshot must be a JSON object");

        bool hasSchemaVersion = false, hasRegionId = false, hasVersion = false, hasOriginX = false;
        bool hasOriginZ = false, hasSpacing = false, hasRows = false, hasColumns = false, hasHeights = false;
        int schemaVersion = 0;
        string regionId = string.Empty;
        long version = 0;
        double originXM = 0, originZM = 0, spacingM = 0;
        int rows = 0, columns = 0;
        JsonElement heights = default;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string name = DecodePropertyName(property);
            if (!seen.Add(name))
                throw new ArgumentException($"{prefix}{name}: duplicate field");
            switch (name)
            {
                case "schema_version":
                    hasSchemaVersion = true;
                    if (ReadIntegerToken(property.Value, prefix + "schema_version") != 1)
                        throw new ArgumentException($"{prefix}schema_version must be the integer 1");
                    schemaVersion = 1;
                    break;
                case "region_id":
                    hasRegionId = true;
                    regionId = ReadId(property.Value, prefix + "region_id");
                    break;
                case "version":
                    hasVersion = true;
                    version = ReadIntegerToken(property.Value, prefix + "version");
                    if (version < 0 || version > MaxVersion)
                        throw new ArgumentException($"{prefix}version must be an integer in [0,{MaxVersion}]");
                    break;
                case "origin_x_m":
                    hasOriginX = true;
                    originXM = ReadRealInRange(property.Value, prefix + "origin_x_m", MinOriginM, MaxOriginM);
                    break;
                case "origin_z_m":
                    hasOriginZ = true;
                    originZM = ReadRealInRange(property.Value, prefix + "origin_z_m", MinOriginM, MaxOriginM);
                    break;
                case "spacing_m":
                    hasSpacing = true;
                    spacingM = ReadRealInRange(property.Value, prefix + "spacing_m", MinSpacingM, MaxSpacingM);
                    break;
                case "rows":
                    hasRows = true;
                    rows = ReadDimension(property.Value, prefix + "rows");
                    break;
                case "columns":
                    hasColumns = true;
                    columns = ReadDimension(property.Value, prefix + "columns");
                    break;
                case "heights_m":
                    hasHeights = true;
                    heights = property.Value;
                    break;
                default:
                    throw new ArgumentException($"{prefix}{name}: unknown field");
            }
        }

        Require(hasSchemaVersion, prefix + "schema_version");
        Require(hasRegionId, prefix + "region_id");
        Require(hasVersion, prefix + "version");
        Require(hasOriginX, prefix + "origin_x_m");
        Require(hasOriginZ, prefix + "origin_z_m");
        Require(hasSpacing, prefix + "spacing_m");
        Require(hasRows, prefix + "rows");
        Require(hasColumns, prefix + "columns");
        Require(hasHeights, prefix + "heights_m");

        double[] heightsM = ReadHeights(heights, prefix + "heights_m", rows, columns);
        double endXM = originXM + (columns - 1) * spacingM;
        double endZM = originZM + (rows - 1) * spacingM;
        if (!double.IsFinite(endXM) || !double.IsFinite(endZM))
            throw new ArgumentException($"{prefix}origin_x_m/origin_z_m/spacing_m must yield finite end XZ");

        return new TerrainSnapshot(schemaVersion, regionId, version, originXM, originZM, spacingM, rows, columns, heightsM);
    }

    private static TerrainPatch ReadPatch(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("patch must be a JSON object");

        bool hasSchemaVersion = false, hasPatchId = false, hasBase = false, hasHeights = false;
        string patchId = string.Empty;
        JsonElement baseElement = default, heights = default;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string name = DecodePropertyName(property);
            if (!seen.Add(name))
                throw new ArgumentException($"{name}: duplicate field");
            switch (name)
            {
                case "schema_version":
                    hasSchemaVersion = true;
                    if (ReadIntegerToken(property.Value, "schema_version") != 1)
                        throw new ArgumentException("schema_version must be the integer 1");
                    break;
                case "patch_id":
                    hasPatchId = true;
                    patchId = ReadId(property.Value, "patch_id");
                    break;
                case "base":
                    hasBase = true;
                    baseElement = property.Value;
                    break;
                case "heights_m":
                    hasHeights = true;
                    heights = property.Value;
                    break;
                default:
                    throw new ArgumentException($"{name}: unknown field");
            }
        }

        Require(hasSchemaVersion, "schema_version");
        Require(hasPatchId, "patch_id");
        Require(hasBase, "base");
        Require(hasHeights, "heights_m");

        TerrainSnapshot baseSnapshot = ReadSnapshot(baseElement, "base.");
        double[] candidateHeights = ReadHeights(heights, "heights_m", baseSnapshot.Rows, baseSnapshot.Columns);

        // 外围锁定：与 base 采样高度按 double 数值严格相等（+0/-0 同值），不引入容差。
        for (int row = 0; row < baseSnapshot.Rows; row++)
        {
            for (int column = 0; column < baseSnapshot.Columns; column++)
            {
                bool onPerimeter = row == 0 || row == baseSnapshot.Rows - 1
                    || column == 0 || column == baseSnapshot.Columns - 1;
                int index = row * baseSnapshot.Columns + column;
                if (onPerimeter && candidateHeights[index] != baseSnapshot.HeightsM[index])
                    throw new ArgumentException(
                        $"heights_m[{index}] (row {row}, column {column}) is on the locked perimeter and must equal the base height at this index");
            }
        }

        return new TerrainPatch(1, patchId, baseSnapshot, candidateHeights);
    }

    private static void Require(bool present, string path)
    {
        if (!present)
            throw new ArgumentException($"{path} is required");
    }

    private static long ReadIntegerToken(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long integer))
            throw new ArgumentException($"{path} must be a decimal integer literal (no fraction or exponent)");
        return integer;
    }

    private static int ReadDimension(JsonElement value, string path)
    {
        long dimension = ReadIntegerToken(value, path);
        if (dimension < 2 || dimension > MaxRowsOrColumns)
            throw new ArgumentException($"{path} must be an integer in [2,{MaxRowsOrColumns}]");
        return (int)dimension;
    }

    private static double ReadRealInRange(JsonElement value, string path, double min, double max)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number)
            || !double.IsFinite(number) || number < min || number > max)
            throw new ArgumentException(
                $"{path} must be a finite JSON number in [{min.ToString(CultureInfo.InvariantCulture)},{max.ToString(CultureInfo.InvariantCulture)}]");
        return number;
    }

    private static string ReadId(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"{path} must be a JSON string matching {IdPattern}");
        string id;
        try
        {
            id = value.GetString()!;
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException($"{path} contains an escaped string that is not valid UTF-16 (isolated surrogate)", "json", exception);
        }
        if (!IsAsciiId(id))
            throw new ArgumentException($"{path} must be a JSON string matching {IdPattern}");
        return id;
    }

    // JSON 转义中的孤立代理项会让 JsonProperty.Name 抛 InvalidOperationException，
    // 绕过合同统一异常；在字段名解码边界统一转为带 json 参数路径的 ArgumentException。
    private static string DecodePropertyName(JsonProperty property)
    {
        try
        {
            return property.Name;
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException("json contains a field name that is not valid UTF-16 (isolated surrogate)", "json", exception);
        }
    }

    private static bool IsAsciiId(string id)
    {
        if (id.Length == 0 || id.Length > 64)
            return false;
        if (id[0] < 'a' || id[0] > 'z')
            return false;
        for (int index = 1; index < id.Length; index++)
        {
            char character = id[index];
            if ((character < 'a' || character > 'z') && (character < '0' || character > '9')
                && character != '_' && character != '-')
                return false;
        }
        return true;
    }

    private static double[] ReadHeights(JsonElement value, string path, int rows, int columns)
    {
        int expected = rows * columns;
        if (value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException($"{path} must be a JSON array of {expected} finite numbers in [-1000,1000]");
        if (value.GetArrayLength() != expected)
            throw new ArgumentException($"{path} must contain exactly {expected} items, found {value.GetArrayLength()}");
        var heights = new double[expected];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            heights[index] = ReadRealInRange(item, $"{path}[{index}]", MinHeightM, MaxHeightM);
            index++;
        }
        return heights;
    }
}
