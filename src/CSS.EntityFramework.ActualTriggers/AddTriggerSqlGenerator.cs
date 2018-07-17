
#if NETSTANDARD
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
#endif

#if NETFULL
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Builders;
using System.Data.Entity.Migrations.Infrastructure;
using System.Data.Entity.Migrations.Model;
using System.Data.Entity.SqlServer;
#endif
using System;
using System.Collections.Generic;
using System.Text;

namespace CSS.EntityFramework.ActualTriggers
{
    public class AddTriggerSqlGenerator :
#if NETSTANDARD
        SqlServerMigrationsSqlGenerator
#endif
#if NETFULL
            SqlServerMigrationSqlGenerator 
#endif
    {
#if NETSTANDARD
        public AddTriggerSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IMigrationsAnnotationProvider migrationsAnnotations) : base(dependencies, migrationsAnnotations)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (migrationsAnnotations == null)
            {
                throw new ArgumentNullException(nameof(migrationsAnnotations));
            }
        }

        protected override void Generate(MigrationOperation migrationOperation, IModel model, MigrationCommandListBuilder builder)
        {
            var operation = migrationOperation as AddTriggerOperation;
            if (operation == null) return;
            builder.AppendLine(operation.AddTriggerSql);
        }
#endif

#if NETFULL
        protected override void Generate(MigrationOperation migrationOperation)
        {
            var operation = migrationOperation as AddTriggerOperation;
            if (operation == null) return;

            using (var writer = Writer())
            {
                writer.WriteLine(operation.AddTriggerSql);
                Statement(writer);
            }
        }
#endif
    }
}
