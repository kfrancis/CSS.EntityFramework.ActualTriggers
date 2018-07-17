#if NETSTANDARD
using Microsoft.EntityFrameworkCore.Migrations.Operations;
#endif
#if NETFULL
using System.Data.Entity.Migrations.Model;
#endif
using System;
using System.Collections.Generic;

using System.Diagnostics.Contracts;
using System.Text;

namespace CSS.EntityFramework.ActualTriggers
{
    public class AddTriggerOperation : MigrationOperation
    {
#if NETSTANDARD
        public AddTriggerOperation(string addTriggerSql)
        {
            Contract.Assert(!string.IsNullOrEmpty(addTriggerSql));
            AddTriggerSql = addTriggerSql;
        }
#endif
#if NETFULL
        public AddTriggerOperation(string addTriggerSql, object anonymousArguments) : base(anonymousArguments)
        {
            Contract.Assert(!string.IsNullOrEmpty(addTriggerSql));
            AddTriggerSql = addTriggerSql;
        }
#endif

        public override bool IsDestructiveChange => false;

        public string AddTriggerSql { get; }
    }
}
