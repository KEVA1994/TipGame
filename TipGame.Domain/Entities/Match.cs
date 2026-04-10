using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TipGame.Domain.Entities
{
    [Table("Matches")]
    public class Match : BaseModel
    {
        [PrimaryKey("Id")]
        public int Id { get; set; }

        [Column("ExternalId")]
        public int ExternalId { get; set; }

        [Column("HomeTeam")]
        public string HomeTeam { get; set; }

        [Column("AwayTeam")]
        public string AwayTeam { get; set; }

        [Column("HomeScore")]
        public int? HomeScore { get; set; }

        [Column("AwayScore")]
        public int? AwayScore { get; set; }

        [Column("KickoffTime")]
        public DateTime KickoffTime { get; set; }

        [Column("Status")]
        public string Status { get; set; }

        // Not mapped — populated manually for point calculation
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    }
}
