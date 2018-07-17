using System;
using System.Collections.Generic;
using System.Text;

namespace CSS.EntityFramework.ActualTriggers
{
    public enum AuditAction
    {
        Insert = 1,
        Update = 2,
        Delete = 3
    }

    /// <summary>
    /// Implemented by the object representing an audit record
    /// </summary>
    public interface IAudited : IAuditedItem
    {
        int AuditId { get; set; }
        AuditAction AuditAction { get; set; }
        DateTime AuditDate { get; set; }
    }

    /// <summary>
    /// Implemented by an object being audited
    /// </summary>
    public interface IAuditedItem
    {
        string ModifyingUserId { get; set; }
        string ModifyingAction { get; set; }
        string Hash { get; }
    }
}
