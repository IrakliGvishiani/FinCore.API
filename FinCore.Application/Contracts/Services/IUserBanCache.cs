namespace FinCore.Application.Contracts.Services
{
    /// <summary>
    /// Short-lived Redis marker used to reject still-valid access tokens of banned users.
    /// The marker only needs to outlive the longest access token, because banned users
    /// cannot obtain new tokens (login and refresh both check IsAdminLocked).
    /// </summary>
    public interface IUserBanCache
    {
        /// <summary>Sets the banned marker. Redis failures are logged, not thrown.</summary>
        Task MarkBannedAsync(string userId);

        /// <summary>Removes the banned marker. Redis failures are logged, not thrown.</summary>
        Task ClearAsync(string userId);

        /// <summary>True if the marker exists. Fails open: returns false and logs if Redis is unavailable.</summary>
        Task<bool> IsBannedAsync(string userId);
    }
}
