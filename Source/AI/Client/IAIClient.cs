using System.Threading.Tasks;

namespace RimTalk.Memory.AI.Client
{
    public interface IAIClient
    {
        Task<Payload> GetChatCompletionAsync(string prompt);

        Task<bool> ValidateAsync();
    }
}