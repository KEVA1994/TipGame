using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TipGame.Domain.Entities
{
    [Table("Competitions")]
    public class Competition : BaseModel
    {
        [PrimaryKey("Id")]
        public int Id { get; set; }

        [Column("Name")]
        public string Name { get; set; } = "";

        [Column("CompetitionCode")]
        public string CompetitionCode { get; set; } = "";

        [Column("DateFrom")]
        public DateTime? DateFrom { get; set; }

        [Column("DateTo")]
        public DateTime? DateTo { get; set; }

        [Column("Status")]
        public string Status { get; set; } = "draft";

        [Column("InviteToken")]
        public Guid InviteToken { get; set; }

        [Column("RemindersEnabled")]
        public bool RemindersEnabled { get; set; }

        [Column("InfoText")]
        public string? InfoText { get; set; }

        [Column("PrizesText")]
        public string? PrizesText { get; set; }
    }
}
