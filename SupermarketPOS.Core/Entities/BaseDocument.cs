using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    /// <summary>
    /// Base class for all workflow documents.
    /// Provides common fields: Number, Status, CreatedBy, BranchId.
    /// </summary>
    public abstract class BaseDocument
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Number { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

        public int UserId { get; set; }
        public virtual User User { get; set; }

        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string Notes { get; set; }

        /// <summary>
        /// Returns the display name of this document type.
        /// </summary>
        public abstract string DocumentType { get; }

        /// <summary>
        /// Validates the document before saving.
        /// Throws InvalidOperationException if invalid.
        /// </summary>
        public virtual void Validate()
        {
            if (string.IsNullOrWhiteSpace(Number))
                throw new InvalidOperationException($"{DocumentType} number is required");

            if (UserId <= 0)
                throw new InvalidOperationException($"{DocumentType} must have a user");
        }
    }
}