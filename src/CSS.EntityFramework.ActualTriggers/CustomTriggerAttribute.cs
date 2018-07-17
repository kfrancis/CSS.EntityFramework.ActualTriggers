using System;

namespace CSS.EntityFramework.ActualTriggers
{
    /// <summary>
    /// The type of the trigger (before, after, or instead
    /// </summary>
    public enum TriggerWhen
    {
        /// <summary>
        /// The trigger is a "before" trigger. The trigger runs before data insert
        /// </summary>
        Before,
        /// <summary>
        /// The trigger is an "instead" trigger. The trigger runs instead of the insert statement
        /// </summary>
        Instead,
        /// <summary>
        /// The trigger is an "after" trigger. The trigger runs after data insert. This is the required 
        /// value if logging is enabled
        /// </summary>
        After
    }

    public abstract class TriggerCustomAttribute : Attribute
    {
        /// <summary>
        /// Attribute for creating a trigger on an entity table.
        /// NOTE: The name of the logging table (if a logEntityType is passed) is assumed to be the same as the name of the ligEntityType
        /// </summary>
        /// <param name="sqlScriptFileName">
        /// The name of a file containing custom SQL statements you want run over and above logging logic. These statements are inserted before the
        /// logging logic (if this is created) in the generated trigger. The file is expected to be found in the /Migrations/CustomTriggerSQL folder and
        /// should be included as an embedded resource.
        /// </param>
        /// <param name="logEntityType">The log table entity. If this is not set, then logging logic is not generated in the triggers</param>
        /// <param name="triggerSqlLocation">The type of trigger to create (Before, After, Instead)</param>
        /// <param name="loggedTableName">
        /// The name of the table to log. If not set, the name of the logged table is assumed to be the name of the class with 's' appended. Be aware that
        /// entity framework does proper pluralizing of table names, so this assumption will fail at times</param>
        /// <param name="triggerType">The type of the trigger (before, after, instead)</param>
        /// <param name="logSchema">The DB Schema of the log table (set to zzLog by default)</param>
        protected TriggerCustomAttribute(string sqlScriptFileName, Type logEntityType, string loggedTableName, TriggerWhen triggerRunsWhen, string logSchema)
        {
            SqlScriptFileName = sqlScriptFileName;
            LogEntityType = logEntityType;
            LoggedTableName = loggedTableName;
            TriggerRunsWhen = triggerRunsWhen;
            LogSchema = logSchema;
        }
        public string InsertMethodName { get; set; }
        public string SqlScriptFileName { get; set; }
        public Type LogEntityType { get; }
        public string LoggedTableName { get; set; }
        public TriggerWhen TriggerRunsWhen { get; }
        public string LogSchema { get; set; }

    }


    /// <summary>
    /// Attribute for creating an insert trigger on an entity table.
    /// NOTE: The name of the logging table (if a logEntityType is passed) is assumed to be the same as the name of the ligEntityType
    /// </summary>
    public class InsertTriggerCustomAttribute : TriggerCustomAttribute
    {
        public InsertTriggerCustomAttribute(string sqlScriptFileName = null, Type logEntityType = null, string loggedTableName = null, TriggerWhen triggerRunsWhen = TriggerWhen.After, string logSchema = "zzLog")
            : base(sqlScriptFileName, logEntityType, loggedTableName, triggerRunsWhen, logSchema)
        {
        }
    }

    /// <summary>
    /// Attribute for creating an update trigger on an entity table.
    /// NOTE: The name of the logging table (if a logEntityType is passed) is assumed to be the same as the name of the ligEntityType
    /// </summary>
    public class UpdateTriggerCustomAttribute : TriggerCustomAttribute
    {
        public UpdateTriggerCustomAttribute(string sqlScriptFileName = null, Type logEntityType = null, string loggedTableName = null, TriggerWhen triggerRunsWhen = TriggerWhen.After, string logSchema = "zzLog")
            : base(sqlScriptFileName, logEntityType, loggedTableName, triggerRunsWhen, logSchema)
        {
        }
    }

    /// <summary>
    /// Attribute for creating a delete trigger on an entity table.
    /// NOTE: The name of the logging table (if a logEntityType is passed) is assumed to be the same as the name of the ligEntityType
    /// </summary>
    public class DeleteTriggerCustomAttribute : TriggerCustomAttribute
    {
        public DeleteTriggerCustomAttribute(string sqlScriptFileName = null, Type logEntityType = null, string loggedTableName = null, TriggerWhen triggerRunsWhen = TriggerWhen.After, string logSchema = "zzLog")
            : base(sqlScriptFileName, logEntityType, loggedTableName, triggerRunsWhen, logSchema)
        {
        }
    }
}
