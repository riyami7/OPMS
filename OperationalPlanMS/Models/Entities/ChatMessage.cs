using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// A single message in a chat conversation.
    /// </summary>
    public class ChatMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }

        /// <summary>
        /// "user" or "assistant"
        /// </summary>
        [MaxLength(20)]
        public string Role { get; set; } = "user";

        /// <summary>
        /// Message content (text)
        /// </summary>
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ConversationId")]
        public virtual ChatConversation Conversation { get; set; } = null!;
    }
}
