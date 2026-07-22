using System.Text;

namespace RimTalk.Memory.AI
{
    public class Payload
    {
        public string URL { get; set; }
        public string Model { get; set; }
        public string Request { get; set; }
        public string Response { get; set; }
        public int? TokenCount { get; set; }
        public string ErrorMessage { get; set; }

        // 严格校验，仅显式设置为 true 才表示成功
        public bool IsValid { get; set; } = false;

        public Payload() { }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RIMTALK-EXPANDMEMORY AI REPORT ===");
            sb.AppendLine($"URL:      {URL}");
            sb.AppendLine($"Model:    {Model}");
            sb.AppendLine($"Tokens:   {TokenCount}");
            if (!IsValid)
                sb.AppendLine($"Error:    {ErrorMessage}");
            sb.AppendLine();
            sb.AppendLine("--- REQUEST PAYLOAD ---");
            sb.AppendLine(Request);
            sb.AppendLine();
            sb.AppendLine("--- RESPONSE PAYLOAD ---");
            sb.AppendLine(Response);
            sb.AppendLine("==========================================");
            return sb.ToString();
        }
    }
}