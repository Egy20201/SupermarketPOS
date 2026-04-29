namespace SupermarketPOS.Core.Entities
{
    public class UserBranchPermission
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BranchId { get; set; }
        public int PermissionId { get; set; }
        public virtual User User { get; set; }
        public virtual Branch Branch { get; set; }
        public virtual Permission Permission { get; set; }
    }
}