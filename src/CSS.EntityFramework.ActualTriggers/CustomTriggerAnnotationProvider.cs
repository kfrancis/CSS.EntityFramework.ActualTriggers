#if NETSTANDARD
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSS.EntityFramework.ActualTriggers
{
#if NETSTANDARD
    public class CustomTriggerAnnotationProvider : SqlServerMigrationsAnnotationProvider
    {
        public CustomTriggerAnnotationProvider(MigrationsAnnotationProviderDependencies dependencies)
            : base(dependencies)
        {
        }

        public override IEnumerable<IAnnotation> For(IProperty property)
        {
            var baseAnnotations = base.For(property);

            var annotation = property.FindAnnotation("CustomTrigger");

            return annotation == null
                ? baseAnnotations
                : baseAnnotations.Concat(new[] { annotation });
        }
    }
#endif
}
