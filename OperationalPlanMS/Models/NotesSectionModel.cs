using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Views.Shared.Partials
{
    public class NotesSectionModel
    {
        public string Controller { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string EntityIdFieldName { get; set; } = "id";
        public string NoteFieldName { get; set; } = "notes";
        public string AddAction { get; set; } = "AddNote";
        public string EditAction { get; set; } = "EditNote";
        public string DeleteAction { get; set; } = "DeleteNote";
        public List<ProgressUpdate> Notes { get; set; } = new();
        public bool IsAdmin { get; set; }
        public bool CanEdit { get; set; }
    }
}
