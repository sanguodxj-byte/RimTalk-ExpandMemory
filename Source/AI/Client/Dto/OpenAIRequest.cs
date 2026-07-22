using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RimTalk.Memory.AI.Client.Dto
{
    [DataContract]
    public class OpenAIRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }

        [DataMember(Name = "messages")]
        public List<Message> Messages { get; set; } = new();

        [DataMember(Name = "temperature", EmitDefaultValue = false)]
        public double? Temperature { get; set; }

        [DataMember(Name = "max_tokens", EmitDefaultValue = false)]
        public int? MaxTokens { get; set; }
    }

    [DataContract]
    public class Message
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }

        [DataMember(Name = "content")]
        public string Content { get; set; }
    }
}