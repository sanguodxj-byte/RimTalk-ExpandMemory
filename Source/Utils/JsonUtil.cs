using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RimTalk.Memory.Utils;

/// <summary>
/// JSON 工具
/// </summary>
public static class JsonUtil
{
    public static string SerializeToJson<T>(T obj)
    {
        // Create a memory stream for serialization
        using var stream = new MemoryStream();
        // Create a DataContractJsonSerializer
        var serializer = new DataContractJsonSerializer(typeof(T));

        // Serialize the ApiRequest object
        serializer.WriteObject(stream, obj);

        // Convert the memory stream to a string
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static T DeserializeFromJson<T>(string json)
    {
        if (!TryDeserializeFromJson<T>(json, out var result, out var ex))
            throw ex;

        return result;
    }

    public static bool TryDeserializeFromJson<T>(string json, out T result, out Exception exception)
    {
        result = default;
        exception = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var serializer = new DataContractJsonSerializer(typeof(T));
            result = (T)serializer.ReadObject(stream);
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }
}
