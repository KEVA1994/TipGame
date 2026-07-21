using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TipGame.Domain.Entities
{
    [Table("EmailRequests")]
    public class EmailRequest : BaseModel
    {
        [PrimaryKey("Id")]
        public long Id { get; set; }

        [Column("CompetitionId")]
        public int CompetitionId { get; set; }

        [Column("Kind")]
        public string Kind { get; set; } = "";

        [Column("RequestedBy")]
        public int RequestedBy { get; set; }

        [Column("RequestedAt")]
        public DateTime RequestedAt { get; set; }

        [Column("ProcessedAt")]
        public DateTime? ProcessedAt { get; set; }
    }
}
