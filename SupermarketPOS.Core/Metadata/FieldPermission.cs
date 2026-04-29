namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Phase 2.5: Field-level permission rule.
    /// Controls read/write access to individual fields per role.
    /// Maps to the [FieldPermissions] table.
    /// 
    /// Absence of a rule = full access (whitelist-absent = allowed).
    /// When a rule exists, it explicitly grants or denies read/write.
    /// </summary>
    public class FieldPermission
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string FieldName { get; set; }
        public int RoleId { get; set; }
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; } = true;

        public EntityDefinition Entity { get; set; }
    }
}
