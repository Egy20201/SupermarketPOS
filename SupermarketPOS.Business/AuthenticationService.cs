using SupermarketPOS.Core.Entities;
using SupermarketPOS.Core.Security;
using SupermarketPOS.Data;
using System;
using System.Linq;

namespace SupermarketPOS.Business
{
    /// <summary>
    /// Handles user authentication using PasswordHasher and database lookup.
    /// 
    /// DbContext is created via injected factory function to enable:
    /// - Unit testing with mock contexts
    /// - Centralized connection string management
    /// - Lifetime control by the DI container, not the service
    /// </summary>
    public class AuthenticationService
    {
        private readonly Func<AppDbContext> _contextFactory;

        /// <summary>
        /// Initializes the authentication service with a DbContext factory.
        /// The factory is called once per Authenticate() invocation,
        /// ensuring short-lived DbContext instances.
        /// </summary>
        public AuthenticationService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory
                ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        /// <summary>
        /// Authenticates a user by username and password.
        /// Returns null if authentication fails.
        /// Uses ONLY PasswordHasher.VerifyPassword() — no plaintext fallback.
        /// </summary>
        public User Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            // Factory creates a NEW context each time — ensures short-lived DbContext
            using (var db = _contextFactory())
            {
                var user = db.Users
                    .AsNoTracking()
                    .FirstOrDefault(x =>
                        x.Username == username && x.IsActive);

                if (user == null)
                    return null;

                // Only accept properly hashed passwords via BCrypt
                if (PasswordHasher.VerifyPassword(password, user.Password))
                    return user;

                // Security alert: non-hashed password detected
                if (!PasswordHasher.IsHashed(user.Password))
                {
                    Logger.Error(string.Format(
                        "SECURITY ALERT: User '{0}' has a non-hashed password. Account requires admin reset.",
                        username));
                }

                return null;
            }
        }
    }
}