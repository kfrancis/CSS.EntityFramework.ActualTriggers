using CSS.EntityFramework.ActualTriggers;
using System;

namespace ExampleModels
{
    public class File : IAuditedItem
    {
        public int Id { get; set; }
        public string Filename { get; set; }

        public string ModifyingUserId { get; set; }
        public string ModifyingAction { get; set; }
        public string Hash { get; set; }
    }
}
