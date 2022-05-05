using Microsoft.AspNetCore.SignalR;

namespace LoraGateway.Host.Hubs
{
    public class FuotaHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}