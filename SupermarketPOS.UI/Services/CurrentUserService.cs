using SupermarketPOS.Core.Entities;

namespace SupermarketPOS.UI.Services
{
    public interface ICurrentUserService
    {
        User CurrentUser { get; }
        int UserId { get; }
        int? BranchId { get; }
        string UserRole { get; }
        void SetUser(User user);
        void ClearUser();
    }

    public class CurrentUserService : ICurrentUserService
    {
        public User CurrentUser { get; private set; }

        public int UserId => CurrentUser?.Id ?? 0;
        public int? BranchId => CurrentUser?.BranchId;
        public string UserRole => CurrentUser?.Role;

        public void SetUser(User user)
        {
            CurrentUser = user;
        }

        public void ClearUser()
        {
            CurrentUser = null;
        }
    }
}
