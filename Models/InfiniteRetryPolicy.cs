using Microsoft.AspNetCore.SignalR.Client;

namespace VendingMachineTest.Models
{
    public class InfiniteRetryPolicy : IRetryPolicy
    {
        private readonly TimeSpan[] _delays = new[]
        {
        TimeSpan.Zero,                 // Lần 1
        TimeSpan.FromSeconds(2),       // Lần 2
        TimeSpan.FromSeconds(10),      // Lần 3
        TimeSpan.FromSeconds(30)       // Lần 4
    };

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.PreviousRetryCount < _delays.Length)
            {
                return _delays[retryContext.PreviousRetryCount];
            }

            return TimeSpan.FromSeconds(30);
        }
    }
}
