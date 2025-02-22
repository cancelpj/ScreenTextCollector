using System;
using System.Linq;
using Newtonsoft.Json;

namespace MQTTnet.Extensions.ManagedClient.Persistence;

public class ArraySegmentConverter : JsonConverter<ArraySegment<byte>>
{
    public override void WriteJson(JsonWriter writer, ArraySegment<byte> value, JsonSerializer serializer)
    {
        // 将 ArraySegment<byte> 的数据部分作为 byte[] 序列化
        serializer.Serialize(writer, value.Array?.Skip(value.Offset).Take(value.Count).ToArray());
    }

    public override ArraySegment<byte> ReadJson(JsonReader reader, Type objectType, ArraySegment<byte> existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return default;

        // 反序列化为 byte[]
        byte[] bytes = serializer.Deserialize<byte[]>(reader);

        // 如果需要保持原始的 Offset 和 Count 信息，你可能需要额外的上下文或约定。
        // 在这个简单的例子中，我们将 Offset 设为 0，Count 为数组长度。
        return new ArraySegment<byte>(bytes);
    }
}