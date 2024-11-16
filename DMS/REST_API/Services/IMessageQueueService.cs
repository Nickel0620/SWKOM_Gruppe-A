namespace REST_API.Services
{
    public interface IMessageQueueService
    {
        void SendToQueue(string message);
    }
}