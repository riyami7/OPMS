using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// Represents a chat conversation between a user and the AI assistant.
    /// </summary>
    public class ChatConversation
    {
        public int Id { get; set; }

        /// <summary>
        /// The user who owns this conversation
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Auto-generated title from first user message (first 100 chars)
        /// </summary>
        [MaxLength(200)]
        public string Title { get; set; } = "محادثة جديدة";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime LastMessageAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Soft delete
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        // Navigation
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
