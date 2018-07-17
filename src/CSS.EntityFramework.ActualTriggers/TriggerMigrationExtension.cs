#if NETFULL
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.ComponentModel.DataAnnotations;

namespace CSS.EntityFramework.ActualTriggers
{
#if NETFULL
    public static class MigrationExtensions
    {
        // add custom and logging triggers to DB tables of classes so attributed
        public static void AddTriggers(this DbMigration migration, object anonymousArguments = null)
        {
            //#if DEBUG
            //            if (!System.Diagnostics.Debugger.IsAttached)
            //                System.Diagnostics.Debugger.Launch();
            //#endif

            // get all the types that have been attributed with the InsertTriggerCustomAttribute
            List<Type> types = (from a in AppDomain.CurrentDomain.GetAssemblies()
                                from t in a.GetTypes()
                                where t.IsDefined(typeof(InsertTriggerCustomAttribute), false)
                                select t as Type).ToList();
            // loop those types and create the triggers
            foreach (var triggerType in types)
            {
                // NOTE: triggerType is the type of object that we are aplying a trigger to
                var insertTriggerAttr = triggerType.GetCustomAttributes(typeof(InsertTriggerCustomAttribute), false).FirstOrDefault() as InsertTriggerCustomAttribute;
                if (insertTriggerAttr != null)
                {
                    var triggerSql = GenerateInsertTrigger(triggerType, insertTriggerAttr);
                    ((IDbMigration)migration).AddOperation(new AddTriggerOperation(triggerSql, anonymousArguments));
                }
            }
            // get all the types that have been attributed with the UpdateTriggerCustomAttribute
            types = (from a in AppDomain.CurrentDomain.GetAssemblies()
                     from t in a.GetTypes()
                     where t.IsDefined(typeof(UpdateTriggerCustomAttribute), false)
                     select t as Type).ToList();
            // loop those types and create the triggers
            foreach (var triggerType in types)
            {
                // NOTE: triggerType is the type of object that we are aplying a trigger to
                var updateTriggerAttr = triggerType.GetCustomAttributes(typeof(UpdateTriggerCustomAttribute), false).FirstOrDefault() as UpdateTriggerCustomAttribute;
                if (updateTriggerAttr != null)
                {
                    var triggerSql = GenerateUpdateTrigger(triggerType, updateTriggerAttr);
                    ((IDbMigration)migration).AddOperation(new AddTriggerOperation(triggerSql, anonymousArguments));
                }
            }
            // get all the types that have been attributed with the DeleteTriggerCustomAttribute
            types = (from a in AppDomain.CurrentDomain.GetAssemblies()
                     from t in a.GetTypes()
                     where t.IsDefined(typeof(DeleteTriggerCustomAttribute), false)
                     select t as Type).ToList();
            // loop those types and create the triggers
            foreach (var triggerType in types)
            {
                // NOTE: triggerType is the type of object that we are aplying a trigger to
                var deleteTriggerAttr = triggerType.GetCustomAttributes(typeof(DeleteTriggerCustomAttribute), false).FirstOrDefault() as DeleteTriggerCustomAttribute;
                if (deleteTriggerAttr != null)
                {
                    var triggerSql = GenerateDeleteTrigger(triggerType, deleteTriggerAttr);
                    ((IDbMigration)migration).AddOperation(new AddTriggerOperation(triggerSql, anonymousArguments));
                }
            }
        }

        /// <summary>
        /// Generates the insert trigger for a type
        /// </summary>
        /// <param name="triggerType">The type that the trigger is being created for</param>
        /// <param name="insertTriggerAttr">The parameters passed to the attribute</param>
        /// <returns>The SQL to define the insert trigger</returns>
        private static string GenerateInsertTrigger(Type triggerType, InsertTriggerCustomAttribute insertTriggerAttr)
        {
            // set the logged table name
            var loggedTableName = !string.IsNullOrWhiteSpace(insertTriggerAttr.LoggedTableName) ? insertTriggerAttr.LoggedTableName : triggerType.Name + "s";

            // check that the type implements IAuditedItem if a logging Entity type was passed
            if (insertTriggerAttr.LogEntityType != null && !typeof(IAuditedItem).IsAssignableFrom(triggerType))
                throw new ArgumentException(string.Format("Type {0} must implement the IAuditedItem interface in order for logging to be implemented", triggerType.Name), "triggerType");

            // check that the logging type implements IAudited if a logging Entity type was passed
            if (insertTriggerAttr.LogEntityType != null && !typeof(IAudited).IsAssignableFrom(insertTriggerAttr.LogEntityType))
                throw new ArgumentException(string.Format("Type {0} must implement the IAudited interface in order for logging to be implemented", insertTriggerAttr.LogEntityType), "insertTriggerAttr");

            // string builder to create the trigger
            var triggerSQL = new StringBuilder();

            // drop the trigger if it already exists

            triggerSQL.AppendFormat("\nIF(OBJECT_ID(N'[dbo].[TRIG_{0}_MIGRATION_INSERT_1]') IS NOT NULL) BEGIN\nDROP TRIGGER [dbo].[TRIG_{0}_MIGRATION_INSERT_1];\nEND;\n\n", loggedTableName);

            // create basic trigger
            triggerSQL.AppendFormat("EXEC('CREATE TRIGGER [dbo].[TRIG_{0}_MIGRATION_INSERT_1] ON [dbo].[{0}] {1} INSERT\nAS\nBEGIN\n\n", loggedTableName, insertTriggerAttr.TriggerRunsWhen);

            // is custom SQL is specified, insert that first
            if (!string.IsNullOrWhiteSpace(insertTriggerAttr.SqlScriptFileName))
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                var resourceName = string.Format("WSIB.Core.Migrations.CustomTriggerSQL.{0}", insertTriggerAttr.SqlScriptFileName);
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    // make sure we found what we were looking for
                    if (stream == null)
                        throw new Exception(string.Format("Cannot find resource WSIB.Core.Migrations.CustomTriggerSQL.{0}", insertTriggerAttr.SqlScriptFileName));

                    // gram the SQL and insert it
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var customSQL = reader.ReadToEnd();
                        triggerSQL.AppendFormat("-- Custom SQL Code from {0}\n{1}\n\n", insertTriggerAttr.SqlScriptFileName, customSQL);
                    }
                }
            }

            // if we are creating logging logic, create a comma delimited list of log fields here - we must also check the the LogEntityType has field for
            // each field being logged
            if (insertTriggerAttr.LogEntityType != null)
            {
                // get property info for IAuditedItem
                var auditedItemTypeProps = typeof(IAuditedItem).GetProperties();

                // this will hold all of the fields that we transfer to the log table
                var triggerFields = new StringBuilder();
                // this will hold a list of fields that we use to calculate the hash
                var hashFields = new StringBuilder();

                // get the fields we will be logging
                var fields = triggerType.GetProperties().Where(x => x.PropertyType.IsPrimitive || x.PropertyType.IsEnum || x.PropertyType.IsValueType || x.PropertyType.Equals(typeof(string))).ToList();
                // we also need to track which field is the id field
                var idField = string.Empty;
                // now loop through the fields
                int hashCounter = 0;
                foreach (var field in fields.Where(f => f.CanWrite))
                {
                    // is this the Id field
                    if (Attribute.IsDefined(field, typeof(KeyAttribute))) { idField = field.Name; }

                    // attempt to access the field in the logging type
                    var loggingField = insertTriggerAttr.LogEntityType.GetProperty(field.Name);

                    // check to see if the log table contains this field
                    if (loggingField == null)
                        throw new Exception(string.Format("Type {0} does not contain a field with name {1}.", insertTriggerAttr.LogEntityType.Name, field.Name));

                    // check that the type of data stored is also correct
                    if (!loggingField.PropertyType.Equals(field.PropertyType))
                    {
                        // the logging table can also contain a nullable version of the type
                        var underlyingType = Nullable.GetUnderlyingType(loggingField.PropertyType);
                        if (underlyingType == null || !underlyingType.Equals(field.PropertyType))
                            throw new Exception(string.Format("The field {0} in the logging class {1} is not the same type as field {0} in the logged class.", field.Name, insertTriggerAttr.LogEntityType.Name));
                    }
                    // ok, add the field to the list of fields to be logged
                    triggerFields.AppendFormat("{1}t.[{0}]", field.Name, triggerFields.Length > 0 ? "," : string.Empty);

                    // also add to the hash fields unless the field is in the IAuditedItem interface or it is the key field
                    if (field.Name != idField && !auditedItemTypeProps.Any(p => p.Name == field.Name))
                    {
                        hashCounter += 1;
                        hashFields.AppendFormat("{1}t.[{0}]", field.Name, hashFields.Length > 0 ? "," : string.Empty);
                    }
                }
                // append the trigger logic to the trigger
                triggerSQL.Append("-- Generated Logging Logic\n-- 1. Calculate the HASH value\n");
                var concat = hashCounter == 1 ? hashFields.ToString() : $"CONCAT({hashFields})";
                triggerSQL.AppendFormat("UPDATE [dbo].[{0}]\nSET [Hash] = CONVERT(VARCHAR(32), HASHBYTES(''MD5'', {1}))\nFROM [dbo].[{0}] t\nINNER JOIN Inserted i ON i.[{2}] = t.[{2}]\n\n",
                                         loggedTableName, concat, idField);
                triggerSQL.Append("-- 2. Copy fields to log table\n");
                triggerSQL.AppendFormat("INSERT INTO [{0}].[{1}]([AuditAction],[AuditDate],{2})\nSELECT 1,GETDATE(),{3}\nFROM [dbo].[{4}] t\nINNER JOIN Inserted i ON i.[{5}] = t.[{5}]\n\n",
                                        insertTriggerAttr.LogSchema, insertTriggerAttr.LogEntityType.Name, triggerFields.ToString().Replace("t.", string.Empty),
                                        triggerFields, loggedTableName, idField);
            }
            triggerSQL.Append("END')");
            return triggerSQL.ToString();
        }


        /// <summary>
        /// Generates the update trigger for a type
        /// </summary>
        /// <param name="triggerType">The type that the trigger is being created for</param>
        /// <param name="updateTriggerAttr">The parameters passed to the attribute</param>
        /// <returns>The SQL to define the insert trigger</returns>
        private static string GenerateUpdateTrigger(Type triggerType, UpdateTriggerCustomAttribute updateTriggerAttr)
        {
            // set the logged table name
            var loggedTableName = !string.IsNullOrWhiteSpace(updateTriggerAttr.LoggedTableName) ? updateTriggerAttr.LoggedTableName : triggerType.Name + "s";

            // check that the type implements IAuditedItem if a logging Entity type was passed
            if (updateTriggerAttr.LogEntityType != null && !typeof(IAuditedItem).IsAssignableFrom(triggerType))
                throw new ArgumentException(string.Format("Type {0} must implement the IAuditedItem interface in order for logging to be implemented", triggerType.Name), "triggerType");

            // check that the logging type implements IAudited if a logging Entity type was passed
            if (updateTriggerAttr.LogEntityType != null && !typeof(IAudited).IsAssignableFrom(updateTriggerAttr.LogEntityType))
                throw new ArgumentException(string.Format("Type {0} must implement the IAudited interface in order for logging to be implemented", updateTriggerAttr.LogEntityType), "insertTriggerAttr");

            // string builder to create the trigger
            var triggerSQL = new StringBuilder();

            // drop the trigger if it already exists
            triggerSQL.AppendFormat("\nIF(OBJECT_ID(N'[dbo].[TRIG_{0}_MIGRATION_UPDATE_1]') IS NOT NULL) BEGIN\nDROP TRIGGER [dbo].[TRIG_{0}_MIGRATION_UPDATE_1];\nEND;\n\n", loggedTableName);

            // create basic trigger
            triggerSQL.AppendFormat("EXEC('CREATE TRIGGER [dbo].[TRIG_{0}_MIGRATION_UPDATE_1] ON [dbo].[{0}] {1} UPDATE\nAS\nBEGIN\n\n", loggedTableName, updateTriggerAttr.TriggerRunsWhen);

            // only done if trigger is not being called from another trigger
            triggerSQL.Append("IF TRIGGER_NESTLEVEL() <= 1\nBEGIN");

            // is custom SQL is specified, insert that first
            if (!string.IsNullOrWhiteSpace(updateTriggerAttr.SqlScriptFileName))
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                var resourceName = string.Format("WSIB.Core.Migrations.CustomTriggerSQL.{0}", updateTriggerAttr.SqlScriptFileName);
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    // make sure we found what we were looking for
                    if (stream == null)
                        throw new Exception(string.Format("Cannot find resource WSIB.Core.Migrations.CustomTriggerSQL.{0}", updateTriggerAttr.SqlScriptFileName));

                    // gram the SQL and insert it
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var customSQL = reader.ReadToEnd();
                        triggerSQL.AppendFormat("\t-- Custom SQL Code from {0}\n\t{1}\n\n", updateTriggerAttr.SqlScriptFileName, customSQL);
                    }
                }
            }

            // if we are creating logging logic, create a comma delimited list of log fields here - we must also check the the LogEntityType has field for
            // each field being logged
            if (updateTriggerAttr.LogEntityType != null)
            {
                // get property info for IAuditedItem
                var auditedItemTypeProps = typeof(IAuditedItem).GetProperties();

                // this will hold all of the fields that we transfer to the log table
                var triggerFields = new StringBuilder();
                // this will hold a list of fields that we use to calculate the hash
                var hashFields = new StringBuilder();

                // get the fields we will be logging
                var fields = triggerType.GetProperties().Where(x => x.PropertyType.IsPrimitive || x.PropertyType.IsEnum || x.PropertyType.IsValueType || x.PropertyType.Equals(typeof(string))).ToList();
                // we also need to track which field is the id field
                var idField = string.Empty;
                // now loop through the fields
                int hashCounter = 0;
                foreach (var field in fields.Where(f => f.CanWrite))
                {
                    // is this the Id field
                    if (Attribute.IsDefined(field, typeof(KeyAttribute))) { idField = field.Name; }

                    // attempt to access the field in the logging type
                    var loggingField = updateTriggerAttr.LogEntityType.GetProperty(field.Name);

                    // check to see if the log table contains this field
                    if (loggingField == null)
                        throw new Exception(string.Format("Type {0} does not contain a field with name {1}.", updateTriggerAttr.LogEntityType.Name, field.Name));

                    // check that the type of data stored is also correct
                    if (!loggingField.PropertyType.Equals(field.PropertyType))
                    {
                        // the logging table can also contain a nullable version of the type
                        var underlyingType = Nullable.GetUnderlyingType(loggingField.PropertyType);
                        if (underlyingType == null || !underlyingType.Equals(field.PropertyType))
                            throw new Exception(string.Format("The field {0} in the logging class {1} is not the same type as field {0} in the logged class.", field.Name, updateTriggerAttr.LogEntityType.Name));
                    }
                    // ok, add the field to the list of fields to be logged
                    triggerFields.AppendFormat("{1}t.[{0}]", field.Name, triggerFields.Length > 0 ? "," : string.Empty);

                    // also add to the hash fields unless the field is in the IAuditedItem interface or it is the key field
                    if (field.Name != idField && !auditedItemTypeProps.Any(p => p.Name == field.Name))
                    {
                        hashCounter += 1;
                        hashFields.AppendFormat("{1}t.[{0}]", field.Name, hashFields.Length > 0 ? "," : string.Empty);
                    }
                }
                // append the trigger logic to the trigger
                var concat = hashCounter == 1 ? hashFields.ToString() : $"CONCAT({hashFields})";
                triggerSQL.Append("\n\t-- Generated Logging Logic\n\t-- 1. Calculate the HASH value\n");
                triggerSQL.AppendFormat("\tUPDATE [dbo].[{0}]\n\tSET [Hash] = CONVERT(VARCHAR(32), HASHBYTES(''MD5'', {1}))\n\tFROM [dbo].[{0}] t\n\tINNER JOIN Inserted i ON i.[{2}] = t.[{2}]\n\n",
                                         loggedTableName, concat, idField);
                triggerSQL.Append("\t-- 2. Copy fields to log table if hash has changed\n");
                triggerSQL.AppendFormat("\tINSERT INTO [{0}].[{1}]([AuditAction],[AuditDate],{2})\n\tSELECT 2,GETDATE(),{3}\n\tFROM [dbo].[{4}] t\n\tINNER JOIN Deleted d ON d.[{5}] = t.[{5}] AND d.[Hash] <> t.[Hash]\n",
                                        updateTriggerAttr.LogSchema, updateTriggerAttr.LogEntityType.Name, triggerFields.ToString().Replace("t.", string.Empty),
                                        triggerFields, loggedTableName, idField);
                triggerSQL.Append("END\n\n");
            }
            triggerSQL.Append("END')");
            return triggerSQL.ToString();
        }

        /// <summary>
        /// Generates the delete trigger for a type
        /// </summary>
        /// <param name="triggerType">The type that the trigger is being created for</param>
        /// <param name="deleteTriggerAttr">The parameters passed to the attribute</param>
        /// <returns>The SQL to define the insert trigger</returns>
        private static string GenerateDeleteTrigger(Type triggerType, DeleteTriggerCustomAttribute deleteTriggerAttr)
        {
            // set the logged table name
            var loggedTableName = !string.IsNullOrWhiteSpace(deleteTriggerAttr.LoggedTableName) ? deleteTriggerAttr.LoggedTableName : triggerType.Name + "s";

            // check that the type implements IAuditedItem if a logging Entity type was passed
            if (deleteTriggerAttr.LogEntityType != null && !typeof(IAuditedItem).IsAssignableFrom(triggerType))
                throw new ArgumentException(string.Format("Type {0} must implement the IAuditedItem interface in order for logging to be implemented", triggerType.Name), "triggerType");

            // check that the logging type implements IAudited if a logging Entity type was passed
            if (deleteTriggerAttr.LogEntityType != null && !typeof(IAudited).IsAssignableFrom(deleteTriggerAttr.LogEntityType))
                throw new ArgumentException(string.Format("Type {0} must implement the IAudited interface in order for logging to be implemented", deleteTriggerAttr.LogEntityType), "deleteTriggerAttr");

            // string builder to create the trigger
            var triggerSQL = new StringBuilder();

            // drop the trigger if it already exists
            triggerSQL.AppendFormat("\nIF(OBJECT_ID(N'[dbo].[TRIG_{0}_MIGRATION_DELETE_1]') IS NOT NULL) BEGIN\nDROP TRIGGER [dbo].[TRIG_{0}_MIGRATION_DELETE_1];\nEND;\n\n", loggedTableName);

            // create basic trigger
            triggerSQL.AppendFormat("EXEC('CREATE TRIGGER [dbo].[TRIG_{0}_MIGRATION_DELETE_1] ON [dbo].[{0}] {1} DELETE\nAS\nBEGIN\n\n", loggedTableName, deleteTriggerAttr.TriggerRunsWhen);

            // is custom SQL is specified, insert that first
            if (!string.IsNullOrWhiteSpace(deleteTriggerAttr.SqlScriptFileName))
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                var resourceName = string.Format("WSIB.Core.Migrations.CustomTriggerSQL.{0}", deleteTriggerAttr.SqlScriptFileName);
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    // make sure we found what we were looking for
                    if (stream == null)
                        throw new Exception(string.Format("Cannot find resource WSIB.Core.Migrations.CustomTriggerSQL.{0}", deleteTriggerAttr.SqlScriptFileName));

                    // gram the SQL and insert it
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var customSQL = reader.ReadToEnd();
                        triggerSQL.AppendFormat("-- Custom SQL Code from {0}\n{1}\n\n", deleteTriggerAttr.SqlScriptFileName, customSQL);
                    }
                }
            }

            // if we are creating logging logic, create a comma delimited list of log fields here - we must also check the the LogEntityType has field for
            // each field being logged
            if (deleteTriggerAttr.LogEntityType != null)
            {
                // get property info for IAuditedItem
                var auditedItemTypeProps = typeof(string).GetProperties();

                // this will hold all of the fields that we transfer to the log table
                var triggerFields = new StringBuilder();
                // this will hold a list of fields that we use to calculate the hash
                var hashFields = new StringBuilder();

                var fields = triggerType.GetProperties().Where(x => x.PropertyType.IsPrimitive || x.PropertyType.IsEnum || x.PropertyType.IsValueType || x.PropertyType.Equals(typeof(string))).ToList();
                foreach (var field in fields.Where(f => f.CanWrite))
                {
                    // attempt to access the field in the logging type
                    var loggingField = deleteTriggerAttr.LogEntityType.GetProperty(field.Name);

                    // check to see if the log table contains this field
                    if (loggingField == null)
                        throw new Exception(string.Format("Type {0} does not contain a field with name {1}.", deleteTriggerAttr.LogEntityType.Name, field.Name));

                    // check that the type of data stored is also correct
                    if (!loggingField.PropertyType.Equals(field.PropertyType))
                    {
                        // the logging table can also contain a nullable version of the type
                        var underlyingType = Nullable.GetUnderlyingType(loggingField.PropertyType);
                        if (underlyingType == null || !underlyingType.Equals(field.PropertyType))
                            throw new Exception(string.Format("The field {0} in the logging class {1} is not the same type as field {0} in the logged class.", field.Name, deleteTriggerAttr.LogEntityType.Name));
                    }
                    // ok, add the field to the list of fields to be logged
                    triggerFields.AppendFormat("{1}d.[{0}]", field.Name, triggerFields.Length > 0 ? "," : string.Empty);
                }
                // append the trigger logic to the trigger
                triggerSQL.Append("-- Generated Logging Logic\n-- 1. Copy fields from deleted to log\n");
                triggerSQL.AppendFormat("INSERT INTO [{0}].[{1}]([AuditAction],[AuditDate],{2})\nSELECT 3,GETDATE(),{3}\nFROM Deleted d\n\n",
                                        deleteTriggerAttr.LogSchema, deleteTriggerAttr.LogEntityType.Name, triggerFields.ToString().Replace("d.", string.Empty),
                                        triggerFields, loggedTableName);
            }
            triggerSQL.Append("END')");
            return triggerSQL.ToString();

        }



        public static void RemoveTriggers(this DbMigration migration)
        {
            var types = migration.GetType().Assembly.GetTypes().Where(t => typeof(InsertTriggerCustomAttribute).IsAssignableFrom(t));
            foreach (var triggerType in types)
            {
                string tt = "";
            }
        }
    }
#endif
}
