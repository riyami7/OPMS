using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Views.Shared.Partials
{
    public class NotesSectionModel
    {
        public string Controller { get; set; } = string.Empty;
        public int EntityId { get; set; }

        /// <summary>
        /// Hidden field name for the entity ID in Add/Edit/Delete forms
        /// e.g. "id" for Initiatives, "id" for Projects
        /// </summary>
        public string EntityIdFieldName { get; set; } = "id";

        /// <summary>
        /// Hidden field name for the entity ID in Edit/Delete modal forms
        /// e.g. "initiativeId", "projectId"
        /// </summary>
        public string ModalEntityIdFieldName { get; set; } = "id";

        /// <summary>
        /// Textarea name in AddNote form
        /// e.g. "notes" for Initiatives, "note" for Projects
        /// </summary>
        public string AddNoteFieldName { get; set; } = "notes";

        /// <summary>
        /// Textarea name in EditNote form (always "notes")
        /// </summary>
        public string EditNoteFieldName { get; set; } = "notes";

        public string AddAction { get; set; } = "AddNote";
        public string EditAction { get; set; } = "EditNote";
        public string DeleteAction { get; set; } = "DeleteNote";
        public List<ProgressUpdate> Notes { get; set; } = new();
        public bool IsAdmin { get; set; }
        public bool CanEdit { get; set; }
    }
}
