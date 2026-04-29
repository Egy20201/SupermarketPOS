using SupermarketPOS.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string FullName { get; set; }
    public string Role { get; set; }
    public int? RoleId { get; set; }
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual Role RoleEntity { get; set; }
    public virtual Branch Branch { get; set; }
}