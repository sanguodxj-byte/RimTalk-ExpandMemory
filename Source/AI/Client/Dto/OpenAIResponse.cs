using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RimTalk.Memory.AI.Client.Dto
{
    [DataContract]
    public class OpenAIResponse
    {
        [DataMember(Name = "choices")]
        public List<Choice> Choices { get; set; }

        [DataMember(Name = "usage")]
        public Usage Usage { get; set; }

        [DataMember(Name = "error")]
        public ErrorDetail Error { get; set; }
    }

    [DataContract]
    public class Choice
    {
        [DataMember(Name = "index", EmitDefaultValue = false)]
        public int Index { get; set; }

        [DataMember(Name = "message")]
        public Message Message { get; set; }

        [DataMember(Name = "finish_reason", EmitDefaultValue = false)]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class Usage
    {
        [DataMember(Name = "prompt_tokens", EmitDefaultValue = false)]
        public int PromptTokens { get; set; }

        [DataMember(Name = "completion_tokens", EmitDefaultValue = false)]
        public int CompletionTokens { get; set; }

        [DataMember(Name = "total_tokens", EmitDefaultValue = false)]
        public int TotalTokens { get; set; }
    }

    [DataContract]
    public class ErrorDetail
    {
        [DataMember(Name = "message", EmitDefaultValue = false)]
        public string Message { get; set; }

        [DataMember(Name = "code", EmitDefaultValue = false)]
        public string Code { get; set; }

        [DataMember(Name = "status", EmitDefaultValue = false)]
        public string Status { get; set; }

        [DataMember(Name = "type", EmitDefaultValue = false)]
        public string Type { get; set; }
    }

    [DataContract]
    public class ErrorResponse
    {
        [DataMember(Name = "error")]
        public ErrorDetail Error { get; set; }
    }
}