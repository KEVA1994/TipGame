using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TipGame.Domain.Entities
{
    [Table("CompetitionMembers")]
    public class CompetitionMember : BaseModel
    {
        [PrimaryKey("Id")]
        public int Id { get; set; }

        [Column("CompetitionId")]
        public int CompetitionId { get; set; }

        [Column("UserId")]
        public int UserId { get; set; }

        [Column("Role")]
        public string Role { get; set; } = "player";
    }
}
